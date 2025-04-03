import { Song } from "@shared/types/song";

export const sortSongs = (songs: Song[]) => {
  return songs.sort((a, b) => {
    const dateA = new Date(
      parseInt(a.year),
      parseInt(a.month) - 1,
      parseInt(a.day),
      parseInt(a.hour),
      parseInt(a.minute)
    );
    const dateB = new Date(
      parseInt(b.year),
      parseInt(b.month) - 1,
      parseInt(b.day),
      parseInt(b.hour),
      parseInt(b.minute)
    );
    return dateB.getTime() - dateA.getTime();
  });
};

export const filterSongs = (
  songs: Song[],
  from?: string | null,
  rangeType?: "all" | "year" | "1month" | "1day" | null
): Song[] => {
  const fromDate = from ? new Date(from) : null;

  return songs.filter((song) => {
    if (!fromDate) return true; 

    const songDate = new Date(
      `${song.year}-${song.month.padStart(2, "0")}-${song.day.padStart(2, "0")}`
    );

    if (rangeType === "year") {
      return songDate.getFullYear() === fromDate.getFullYear();
    } else if (rangeType === "1month") {
      return (
        songDate.getFullYear() === fromDate.getFullYear() &&
        songDate.getMonth() === fromDate.getMonth()
      );
    } else if (rangeType === "1day") {
      return (
        songDate.getFullYear() === fromDate.getFullYear() &&
        songDate.getMonth() === fromDate.getMonth() &&
        songDate.getDate() === fromDate.getDate()
      );
    }

    // Default behavior for "all"
    return songDate >= fromDate;
  });
};

export const groupSongs = (
  songs: Song[],
  rangeType: "all" | "year" | "1month" | "1day"
): { [key: string]: number } => {
  return songs.reduce((acc: { [key: string]: number }, song) => {
    let key = "";

    if (rangeType === "all") {
      key = song.year; // Group by year
    } else if (rangeType === "year") {
      key = song.month.padStart(2, "0"); // Group by month
    } else if (rangeType === "1month") {
      key = song.day.padStart(2, "0"); // Group by day
    } else if (rangeType === "1day") {
      key = `${song.hour}:${song.minute}`; // Group by hour (if needed)
    }

    acc[key] = (acc[key] || 0) + 1;
    return acc;
  }, {});
};