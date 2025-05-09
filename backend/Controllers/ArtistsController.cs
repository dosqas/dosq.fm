using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Utils;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArtistsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly LoggingService _loggingService;

    public ArtistsController(AppDbContext context, LoggingService loggingService)
    {
        _context = context;
        _loggingService = loggingService;
    }

    // GET: /artists
    [HttpGet]
    public async Task<IActionResult> GetArtists(
        [FromQuery] int? minSongs = null,
        [FromQuery] int? maxSongs = null)
    {
        try
        {
            // Fetch all artists from the database
            var artists = await _context.Artists
                .Select(a => new
                {
                    a.ArtistId,
                    a.Name,
                    SongCount = _context.Songs.Count(s => s.Artist.ArtistId == a.ArtistId)
                })
                .ToListAsync();

            // Apply filtering and sorting in memory
            artists = ArtistUtils.FilterAndSortArtists(
                artists,
                minSongs,
                maxSongs,
                a => a.SongCount // Pass a lambda to extract the SongCount
            );

            // Return the result
            if (artists == null || artists.Count == 0)
            {
                return Ok(new List<Artist>()); // Return an empty list with 200 OK
            }

            return Ok(artists);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching artists: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
        finally 
        {
            // Log the action for auditing purposes
            var userId = UserUtils.GetAuthenticatedUserId(User);
            await _loggingService.LogAction(userId, LogEntry.ActionType.READ, LogEntry.EntityType.Artist);
        }
    }

    // GET: /artists/limited
    [HttpGet("limited")]
    public async Task<IActionResult> GetLimitedArtists(
        [FromQuery] int limit = 15,
        [FromQuery] int page = 1,
        [FromQuery] int? minSongs = null,
        [FromQuery] int? maxSongs = null,
        [FromQuery] string? containsString = null)
    {
        try
        {
            if (limit <= 0 || page <= 0)
            {
                return BadRequest("Invalid limit or page parameter");
            }

            // Get the authenticated user's ID
            var userId = UserUtils.GetAuthenticatedUserId(User);

            // Fetch artists based on the user's listened songs
            var artists = await _context.Songs
                .Where(s => s.UserId == userId) // Filter songs by the current user
                .GroupBy(s => s.Artist) // Group by artist
                .Select(g => new
                {
                    ArtistId = g.Key.ArtistId,
                    Name = g.Key.Name,
                    SongCount = g.Count() // Count the number of songs listened to by the user for each artist
                })
                .ToListAsync();

            // Apply filtering for containsString
            if (!string.IsNullOrEmpty(containsString))
            {
                artists = artists.Where(a => a.Name.Contains(containsString, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Apply filtering and sorting
            artists = ArtistUtils.FilterAndSortArtists(
                artists,
                minSongs,
                maxSongs,
                a => a.SongCount // Pass a lambda to extract the SongCount
            );

            // Pagination
            var total = artists.Count;
            var paginatedArtists = artists
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToList();

            if (paginatedArtists == null || paginatedArtists.Count == 0)
            {
                return Ok(new List<object>()); // Return an empty list with 200 OK
            }

            // Check if there are more artists to load
            var hasMore = page * limit < total;

            return Ok(new { paginatedArtists, hasMore });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching limited artists: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
        finally
        {
            // Log the action for auditing purposes
            var userId = UserUtils.GetAuthenticatedUserId(User);
            await _loggingService.LogAction(userId, LogEntry.ActionType.READ, LogEntry.EntityType.Artist);
        }
    }
    
    // GET: /artists/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetArtistById(int id)
    {
        try
        {
            var artist = await _context.Artists
                .Where(a => a.ArtistId == id)
                .Select(a => new
                {
                    a.ArtistId,
                    a.Name,
                    SongCount = _context.Songs.Count(s => s.Artist.ArtistId == a.ArtistId) // Count songs for the artist
                })
                .FirstOrDefaultAsync();
            if (artist == null)
            {
                return NotFound(new { error = "Artist not found" });
            }

            return Ok(artist);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching artist: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
        finally 
        {
            // Log the action for auditing purposes
            var userId = UserUtils.GetAuthenticatedUserId(User);
            await _loggingService.LogAction(userId, LogEntry.ActionType.READ, LogEntry.EntityType.Artist);
        }
    }


    // POST: /artists
    [HttpPost]
    public async Task<IActionResult> AddArtist([FromBody] Artist newArtist)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newArtist.Name))
            {
                return BadRequest(new { error = "Artist name is required" });
            }

            _context.Artists.Add(newArtist);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetArtistById), new { id = newArtist.ArtistId }, newArtist);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error adding artist: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
        finally 
        {
            // Log the action for auditing purposes
            var userId = UserUtils.GetAuthenticatedUserId(User);
            await _loggingService.LogAction(userId, LogEntry.ActionType.CREATE, LogEntry.EntityType.Artist);
        }
    }

    // PATCH: /artists/{id}
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateArtist(int id, [FromBody] Artist partialUpdate)
    {
        try
        {
            var artist = await _context.Artists.FirstOrDefaultAsync(a => a.ArtistId == id);
            if (artist == null)
            {
                return NotFound(new { error = "Artist not found" });
            }

            if (!string.IsNullOrEmpty(partialUpdate.Name)) artist.Name = partialUpdate.Name;

            _context.Artists.Update(artist);
            await _context.SaveChangesAsync();

            return Ok(artist);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error updating artist: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
        finally 
        {
            // Log the action for auditing purposes
            var userId = UserUtils.GetAuthenticatedUserId(User);
            await _loggingService.LogAction(userId, LogEntry.ActionType.UPDATE, LogEntry.EntityType.Artist);
        }
    }

    // DELETE: /artists/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteArtist(int id)
    {
        try
        {
            // Find the artist by ID
            var artist = await _context.Artists.FirstOrDefaultAsync(a => a.ArtistId == id);
            if (artist == null)
            {
                return NotFound(new { error = "Artist not found" });
            }

            // Delete the artist (cascade delete will handle associated songs)
            _context.Artists.Remove(artist);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Artist and associated songs deleted successfully" });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error deleting artist: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
        finally 
        {
            // Log the action for auditing purposes
            var userId = UserUtils.GetAuthenticatedUserId(User);
            await _loggingService.LogAction(userId, LogEntry.ActionType.DELETE, LogEntry.EntityType.Artist);
        }
    }
}