import express from "express";
import cors from "cors";
import bodyParser from "body-parser";
import songsRouter from "./routes/songs/route";
import songByIdRouter from "./routes/songs/[id]/route";

const app = express();

// Middleware
app.use(cors());
app.use(bodyParser.json());

// Routes
app.use("/api/songs", songsRouter); // Handles /api/songs
app.use("/api/songs", songByIdRouter); // Handles /api/songs/:id

app.get("/health-check", (req, res) => {
  res.status(200).send("OK");
});

// Start the server
const PORT = 5000;
app.listen(PORT, () => {
  console.log(`Server is running on port ${PORT}`);
});