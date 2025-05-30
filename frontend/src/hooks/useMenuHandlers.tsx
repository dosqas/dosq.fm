import { useState } from "react";
import { Song } from "@entities/song";

export const useMenuHandlers = () => {
  const [isAddMenuOpen, setIsAddMenuOpen] = useState(false);
  const [isUpdateMenuOpen, setIsUpdateMenuOpen] = useState(false);
  const [openMenuId, setOpenMenuId] = useState<number | string | null>(null);
  const [selectedSong, setSelectedSong] = useState<Song | null>(null);

  const defaultFormData = {
    songId: 0,
    title: "",
    album: "",
    dateListened: "",
    timeListened: "",
    artist: { artistId: 0, name: "" },
    userId: 0,
    };

  const [formData, setFormData] = useState<Song>(defaultFormData);

  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const handleMenuToggle = (songId: number | string) => {
    setOpenMenuId((prevId) => (prevId === songId ? null : songId));
  };

  const handleMenuClose = () => {
    setOpenMenuId(null);
  };

  const handleOpenAddMenu = () => {
    setFormData(defaultFormData);
    setError(null);
    setSuccessMessage(null);
    setIsAddMenuOpen(true);
  };

  const handleCloseAddMenu = () => {
    setIsAddMenuOpen(false);
  };

  const handleOpenUpdateMenu = (song: Song) => {
    setSelectedSong(song);
    setFormData({
      songId: song.songId,
      title: song.title,
      album: song.album,
      dateListened: song.dateListened.split("T")[0],
      timeListened: song.dateListened.split("T")[1]?.slice(0, 5) || "",
      artist: { artistId: song.artist.artistId, name: song.artist.name },
      userId: song.userId,
    });
    setError(null);
    setSuccessMessage(null);
    setIsUpdateMenuOpen(true);
  };
  

  const handleCloseUpdateMenu = () => {
    setSelectedSong(null);
    setIsUpdateMenuOpen(false);
  };

  return {
    isAddMenuOpen,
    isUpdateMenuOpen,
    openMenuId,
    selectedSong,
    formData,
    setFormData,
    error,
    successMessage,
    handleMenuToggle,
    handleMenuClose,
    handleOpenAddMenu,
    handleCloseAddMenu,
    handleOpenUpdateMenu,
    handleCloseUpdateMenu,
    setError,
    setSuccessMessage,
  };
};