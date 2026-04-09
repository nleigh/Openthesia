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
    public double? LengthSeconds { get; set; }
    public string KeySignature { get; set; }
    public string Artist { get; set; }
    public string Title { get; set; }
    public string Album { get; set; }
    public int? Year { get; set; }
    public int? Bpm { get; set; }
    public float? Difficulty { get; set; } // 0.0 to 5.0 star rating
    public string ThumbnailPath { get; set; }
    public bool MetadataFetched { get; set; }
}

public class Playlist
{
    public string Name { get; set; }
    public List<string> FilePaths { get; set; } = new();
}

public class GameStateData
{
    public List<SongState> Songs { get; set; } = new();
    public List<Playlist> Playlists { get; set; } = new();
}

public static class GameStateManager
{
    public static string StatePath = Path.Combine(KnownFolders.RoamingAppData.Path, "Openthesia", "GameState.json");
    public static GameStateData State { get; private set; } = new();

    // O(1) lookup index keyed by normalized file path
    private static Dictionary<string, SongState> _index = new();

    // Auto-save timer to prevent data loss on crash
    private static System.Threading.Timer _autoSaveTimer;

    public static void Initialize()
    {
        LoadState();
        _autoSaveTimer = new System.Threading.Timer(_ => SaveState(), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
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
        RebuildIndex();
    }

    private static void RebuildIndex()
    {
        _index.Clear();
        foreach (var song in State.Songs)
        {
            if (song.FilePath != null)
            {
                _index[song.FilePath.ToLowerInvariant()] = song;
            }
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
        if (_index.TryGetValue(normalizedPath, out var song))
            return song;

        song = new SongState { FilePath = filePath, PlayCount = 0, IsFavorite = false };
        State.Songs.Add(song);
        _index[normalizedPath] = song;
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

    public static void ClearAllMetadata()
    {
        State.Songs.Clear();
        _index.Clear();
    }
}
