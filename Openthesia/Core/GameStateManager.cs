using Newtonsoft.Json;
using Syroot.Windows.IO;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Openthesia.Core;

public class SongState
{
    public string FilePath { get; set; }
    public int PlayCount { get; set; }
    public bool IsFavorite { get; set; }
}

public class GameStateData
{
    public List<SongState> Songs { get; set; } = new();
}

public static class GameStateManager
{
    public static string StatePath = Path.Combine(KnownFolders.RoamingAppData.Path, "Openthesia", "GameState.json");
    public static GameStateData State { get; private set; } = new();

    public static void Initialize()
    {
        LoadState();
    }

    public static void LoadState()
    {
        if (File.Exists(StatePath))
        {
            try
            {
                string json = File.ReadAllText(StatePath);
                State = JsonConvert.DeserializeObject<GameStateData>(json) ?? new GameStateData();
            }
            catch
            {
                State = new GameStateData();
            }
        }
        else
        {
            State = new GameStateData();
            SaveState();
        }
    }

    public static void SaveState()
    {
        try
        {
            string json = JsonConvert.SerializeObject(State, Formatting.Indented);
            File.WriteAllText(StatePath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    public static SongState GetSongState(string filePath)
    {
        var normalizedPath = filePath.ToLowerInvariant();
        var song = State.Songs.FirstOrDefault(s => s.FilePath.ToLowerInvariant() == normalizedPath);
        if (song == null)
        {
            song = new SongState { FilePath = filePath, PlayCount = 0, IsFavorite = false };
            State.Songs.Add(song);
        }
        return song;
    }

    public static void IncrementPlayCount(string filePath)
    {
        var song = GetSongState(filePath);
        song.PlayCount++;
        SaveState();
    }

    public static void SetFavorite(string filePath, bool isFavorite)
    {
        var song = GetSongState(filePath);
        song.IsFavorite = isFavorite;
        SaveState();
    }
}
