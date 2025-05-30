using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Utils;
using backend.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SongsController(AppDbContext context, Services.WebSocketManager webSocketManager, LoggingService loggingService) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly Services.WebSocketManager _webSocketManager = webSocketManager;

    private readonly LoggingService _loggingService = loggingService;

    // GET: /songs
    [HttpGet]
    [Authorize] 
    public async Task<IActionResult> GetSongs(
        [FromQuery] string? from,
        [FromQuery] string? rangetype)
    {
        var userId = 0;
        try
        {
            userId = UserUtils.GetAuthenticatedUserId(User);

            // Fetch all songs for the authenticated user
            var songs = await _context.Songs
                .Where(s => s.UserId == userId)
                .Select(s => new SongDto
                {
                    SongId = s.SongId,
                    Title = s.Title,
                    Album = s.Album,
                    DateListened = s.DateListened.ToString("o"), // Format as ISO 8601 string
                    Artist = new ArtistDto
                    {
                        ArtistId = s.Artist.ArtistId, // Fetch ArtistId from the Artist entity
                        Name = s.Artist.Name // Fetch ArtistName from the Artist entity
                    }
                })
                .ToListAsync();

            // Apply filtering and sorting
            var filteredSongs = SongUtils.FilterAndSortSongsAsync(songs, from, rangetype);

            return Ok(filteredSongs);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching songs: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
        finally
        {
            // Log the action for auditing purposes
            try
            {
                await _loggingService.LogAction(userId, LogEntry.ActionType.READ, LogEntry.EntityType.Song);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error logging action: {ex.Message}");
            }
        }
    }

    // GET: /songs/limited
    [HttpGet("limited")]
    [Authorize]
    public async Task<IActionResult> GetLimitedSongs(
        [FromQuery] int limit = 15,
        [FromQuery] int page = 1,
        [FromQuery] string? from = null,
        [FromQuery] string? rangetype = null,
        [FromQuery] string? containsString = null)
    {
        var userId = 0;
        try
        {
            if (limit <= 0 || page <= 0)
            {
                return BadRequest("Invalid limit or page parameter");
            }

            userId = UserUtils.GetAuthenticatedUserId(User);

            // Fetch songs for the authenticated user with pagination
            var songsQuery = _context.Songs
                .Where(s => s.UserId == userId);

            // Apply filtering for containsString
            if (!string.IsNullOrEmpty(containsString))
            {
                songsQuery = songsQuery.Where(s =>
                    s.Title.Contains(containsString) ||
                    s.Album.Contains(containsString) ||
                    s.Artist.Name.Contains(containsString));
            }

            songsQuery = songsQuery.OrderByDescending(s => s.DateListened);

            var total = await songsQuery.CountAsync();
            var paginatedSongs = await songsQuery
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(s => new SongDto
                {
                    SongId = s.SongId,
                    Title = s.Title,
                    Album = s.Album,
                    DateListened = s.DateListened.ToString("o"), // Format as ISO 8601 string
                    Artist = new ArtistDto
                    {
                        ArtistId = s.Artist.ArtistId,
                        Name = s.Artist.Name
                    }
                })
                .ToListAsync();

            // Apply filtering and sorting
            var filteredSongs = await SongUtils.FilterAndSortSongsAsync(paginatedSongs, from, rangetype);

            // Check if there are more songs to load
            var hasMore = page * limit < total;

            return Ok(new { songs = filteredSongs, hasMore });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching limited songs: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
        finally
        {
            // Log the action for auditing purposes
            try
            {
                await _loggingService.LogAction(userId, LogEntry.ActionType.READ, LogEntry.EntityType.Song);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error logging action: {ex.Message}");
            }
        }
    }

    // GET: /songs/{id}
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetSongById(int id)
    {
        var userId = 0;
        try
        {
            userId = UserUtils.GetAuthenticatedUserId(User);

            // Find the song by ID for the authenticated user
            var song = await _context.Songs
                .FirstOrDefaultAsync(s => s.SongId == id && s.UserId == userId);

            if (song == null)
            {
                return NotFound(new { error = "Song not found" });
            }

            return Ok(song);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching song by ID: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
        finally 
        {
            // Log the action for auditing purposes
            await _loggingService.LogAction(userId, LogEntry.ActionType.READ, LogEntry.EntityType.Song);
        }
    }

    // POST: /songs
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AddSong([FromBody] Song song)
    {
        var userId = 0;
        try
        {
            userId = UserUtils.GetAuthenticatedUserId(User);

            // Associate the song with the authenticated user
            song.UserId = userId;

            // Check if the artist already exists
            if (song.Artist != null && song.Artist.ArtistId > 0)
            {
                var existingArtist = await _context.Artists
                    .FirstOrDefaultAsync(a => a.ArtistId == song.Artist.ArtistId);

                if (existingArtist != null)
                {
                    // Use the existing artist
                    song.Artist = existingArtist;
                }
                else
                {
                    return BadRequest(new { error = "Artist not found." });
                }
            }
            else
            {
                // If no artistId is provided, return an error
                return BadRequest(new { error = "ArtistId is required and must reference an existing artist." });
            }

            // Validate the incoming song data
            var validationError = SongUtils.ValidateSong(song);
            if (!string.IsNullOrEmpty(validationError))
            {
                return BadRequest(new { error = validationError });
            }

            // Save the new song to the database
            _context.Songs.Add(song);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSongById), new { id = song.SongId }, song);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error adding song: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
        finally
        {
            // Log the action for auditing purposes
            await _loggingService.LogAction(userId, LogEntry.ActionType.CREATE, LogEntry.EntityType.Song);
        }
    }

    // POST: /songs/start-auto-generation
    [HttpPost("start-auto-generation")]
    [Authorize] // Ensure the endpoint is protected
    public async Task<IActionResult> StartAutoGeneration()
    {
        try
        {
            Console.WriteLine("StartAutoGeneration endpoint hit.");

            // Get the authenticated user's ID
            var userId = UserUtils.GetAuthenticatedUserId(User);
            Console.WriteLine($"Authenticated user ID: {userId}");

            // Start auto-generation for the authenticated user
            Console.WriteLine($"Starting auto-generation for user {userId}...");
            await _webSocketManager.StartAutoGenerationAsync(userId);

            Console.WriteLine($"Auto-generation started successfully for user {userId}.");
            return Ok(new { message = "Auto-generation started for user", userId });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error starting auto-generation: {ex.Message}");
            return StatusCode(500, new { error = "Failed to start auto-generation" });
        }
    }

    [HttpPost("stop-auto-generation")]
    [Authorize] // Ensure the endpoint is protected
    public IActionResult StopAutoGeneration()
    {
        try
        {
            Console.WriteLine("StopAutoGeneration endpoint hit.");

            // Get the authenticated user's ID
            var userId = UserUtils.GetAuthenticatedUserId(User);
            Console.WriteLine($"Authenticated user ID: {userId}");

            // Stop auto-generation for the authenticated user
            Console.WriteLine($"Stopping auto-generation for user {userId}...");
            _webSocketManager.StopAutoGeneration(userId);

            Console.WriteLine($"Auto-generation stopped successfully for user {userId}.");
            return Ok(new { message = "Auto-generation stopped for user", userId });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error stopping auto-generation: {ex.Message}");
            return StatusCode(500, new { error = "Failed to stop auto-generation" });
        }
    }

    // PATCH: Update a song by ID
    [HttpPatch("{id}")]
    [Authorize] 
    public async Task<IActionResult> UpdateSong(int id, [FromBody] Song partialUpdate)
    {
        var userId = 0;
        try
        {
            userId = UserUtils.GetAuthenticatedUserId(User);

            // Find the song by ID for the authenticated user
            var song = await _context.Songs
                .FirstOrDefaultAsync(s => s.SongId == id && s.UserId == userId);

            if (song == null)
            {
                return NotFound(new { error = "Song not found" });
            }

            // Update the song with the partial data
            if (!string.IsNullOrEmpty(partialUpdate.Title)) song.Title = partialUpdate.Title;
            if (!string.IsNullOrEmpty(partialUpdate.Album)) song.Album = partialUpdate.Album;
            if (partialUpdate.DateListened != default) song.DateListened = partialUpdate.DateListened;
            if (partialUpdate.Artist.ArtistId != 0)
            {
                // Find the artist by ID
                var artist = await _context.Artists.FirstOrDefaultAsync(a => a.ArtistId == partialUpdate.Artist.ArtistId);
                if (artist == null)
                {
                    return BadRequest(new { error = "Invalid ArtistId. Artist not found." });
                }
                song.Artist = artist; // Assign the Artist entity
            }

            // Validate the updated song
            var validationError = SongUtils.ValidateSong(song);
            if (!string.IsNullOrEmpty(validationError))
            {
                return BadRequest(new { error = validationError });
            }

            // Save the updated song to the database
            _context.Songs.Update(song);
            await _context.SaveChangesAsync();

            return Ok(song);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error updating song: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
        finally 
        {
            // Log the action for auditing purposes
            await _loggingService.LogAction(userId, LogEntry.ActionType.UPDATE, LogEntry.EntityType.Song);
        }
    }

    // DELETE: Delete a song by ID
    [HttpDelete("{id}")]
    [Authorize] 
    public async Task<IActionResult> DeleteSong(int id)
    {
        var userId = 0;
        try
        {
            userId = UserUtils.GetAuthenticatedUserId(User);

            // Find the song by ID for the authenticated user
            var song = await _context.Songs.FirstOrDefaultAsync(s => s.SongId == id && s.UserId == userId);

            if (song == null)
            {
                return NotFound(new { error = "Song not found" });
            }

            // Delete the song
            _context.Songs.Remove(song);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Deleted successfully" });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error deleting song: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
        finally 
        {
            // Log the action for auditing purposes
            await _loggingService.LogAction(userId, LogEntry.ActionType.DELETE, LogEntry.EntityType.Song);
        }
    }
}