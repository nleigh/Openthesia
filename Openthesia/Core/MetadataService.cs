using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Newtonsoft.Json.Linq;
using Syroot.Windows.IO;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Openthesia.Core;

namespace Openthesia.Core.Midi;

public static class MetadataService
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly string _cacheDir = Path.Combine(KnownFolders.RoamingAppData.Path, "Openthesia", "Cache", "Thumbnails");
    private static readonly System.Collections.Concurrent.ConcurrentQueue<(string filePath, bool force)> _fetchQueue = new();
    private static bool _isProcessing = false;
    private static readonly SemaphoreSlim _apiThrottle = new SemaphoreSlim(3, 3); // Allow 3 concurrent fetches

    static MetadataService()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Openthesia/1.0");
        if (!Directory.Exists(_cacheDir))
            Directory.CreateDirectory(_cacheDir);
    }

    public static void QueueMetadataFetch(string filePath, bool force = false)
    {
        var songState = GameStateManager.GetSongState(filePath);
        if (songState.MetadataFetched && !force) return;

        if (force)
        {
            songState.MetadataFetched = false;
        }
        
        _fetchQueue.Enqueue((filePath, force));
        if (!_isProcessing)
        {
            _isProcessing = true;
            Task.Run(ProcessQueueAsync);
        }
    }

    private static async Task ProcessQueueAsync()
    {
        var tasks = new List<Task>();
        while (_fetchQueue.TryDequeue(out var item))
        {
            await _apiThrottle.WaitAsync();
            var task = Task.Run(async () =>
            {
                try
                {
                    await FetchMetadataAsync(item.filePath);
                }
                finally
                {
                    _apiThrottle.Release();
                    await Task.Delay(200); // Small delay per-worker to avoid API hammering
                }
            });
            tasks.Add(task);
        }
        await Task.WhenAll(tasks);
        _isProcessing = false;
    }

    public static async Task FetchMetadataAsync(string filePath)
    {
        var songState = GameStateManager.GetSongState(filePath);
        if (songState.MetadataFetched) return;

        bool stateChanged = false;
        string rawName = Path.GetFileNameWithoutExtension(filePath);
        string artist = null, title = rawName;

        if (rawName.Contains("-"))
        {
            var parts = rawName.Split(new[] { '-' }, 2);
            artist = parts[0].Trim();
            title = parts[1].Trim();
        }

        // 1. Midi Extraction
        try
        {
            var midiFile = MidiFile.Read(filePath);
            var tempoMap = midiFile.GetTempoMap();
            var duration = midiFile.GetDuration<MetricTimeSpan>();
            songState.LengthSeconds = duration.TotalSeconds;

            var tempoChange = tempoMap.GetTempoChanges().FirstOrDefault();
            if (tempoChange != null)
            {
                songState.Bpm = (int)Math.Round(tempoChange.Value.BeatsPerMinute);
            }

            var keySignatureEvent = midiFile.GetTrackChunks()
                .SelectMany(c => c.Events)
                .OfType<KeySignatureEvent>()
                .FirstOrDefault();

            if (keySignatureEvent != null)
            {
                string keyStr = keySignatureEvent.Key.ToString();
                string scaleStr = keySignatureEvent.Scale == 0 ? "Major" : "Minor";
                songState.KeySignature = $"{keyStr} {scaleStr}";
            }

            // Difficulty analysis
            songState.Difficulty = ComputeDifficulty(midiFile, tempoMap, duration);

            stateChanged = true;
        }
        catch { }

        // 2. iTunes API Fetch
        try
        {
            string query = Uri.EscapeDataString(artist == null ? title : $"{artist} {title}");
            string apiUrl = $"https://itunes.apple.com/search?term={query}&entity=song&limit=1";
            var response = await _httpClient.GetStringAsync(apiUrl);
            var json = JObject.Parse(response);

            if (json["resultCount"]?.Value<int>() > 0)
            {
                var result = json["results"][0];
                songState.Artist = result["artistName"]?.ToString();
                songState.Title = result["trackName"]?.ToString() ?? title;
                songState.Album = result["collectionName"]?.ToString();

                if (DateTime.TryParse(result["releaseDate"]?.ToString(), out var releaseDate))
                {
                    songState.Year = releaseDate.Year;
                }

                string artworkUrl = result["artworkUrl100"]?.ToString();
                if (!string.IsNullOrEmpty(artworkUrl))
                {
                    // Convert 100x100 to 600x600 for better quality
                    artworkUrl = artworkUrl.Replace("100x100bb", "600x600bb");
                    try
                    {
                        var imgBytes = await _httpClient.GetByteArrayAsync(artworkUrl);
                        string fileName = $"{Guid.NewGuid()}.jpg";
                        string imgPath = Path.Combine(_cacheDir, fileName);
                        File.WriteAllBytes(imgPath, imgBytes);
                        songState.ThumbnailPath = imgPath;
                    }
                    catch { } // handle network failure for img
                }
                stateChanged = true;
            }
            else
            {
                if (songState.Artist == null) songState.Artist = artist;
                if (songState.Title == null) songState.Title = title;
                stateChanged = true;
            }
        }
        catch 
        {
            if (songState.Artist == null) songState.Artist = artist;
            if (songState.Title == null) songState.Title = title;
            stateChanged = true;
        }

        songState.MetadataFetched = true;
        if (stateChanged)
        {
            GameStateManager.SaveState();
        }
    }

    private static float ComputeDifficulty(MidiFile midiFile, TempoMap tempoMap, MetricTimeSpan duration)
    {
        try
        {
            var notes = midiFile.GetNotes().ToList();
            if (notes.Count == 0 || duration.TotalSeconds < 1) return 0f;

            double durationSec = duration.TotalSeconds;

            // 1. Note density (notes per second): 0-2 easy, 2-6 medium, 6-12 hard, 12+ expert
            float noteDensity = (float)(notes.Count / durationSec);
            float densityScore = Math.Clamp(noteDensity / 12f, 0f, 1f);

            // 2. Tempo: faster = harder
            float bpm = 120f;
            var tempoChange = tempoMap.GetTempoChanges().FirstOrDefault();
            if (tempoChange != null) bpm = (float)tempoChange.Value.BeatsPerMinute;
            float tempoScore = Math.Clamp((bpm - 60f) / 140f, 0f, 1f); // 60-200 range

            // 3. Polyphony: max simultaneous notes
            int maxPolyphony = 1;
            var sortedNotes = notes.OrderBy(n => n.Time).ToList();
            for (int i = 0; i < sortedNotes.Count; i++)
            {
                int simultaneous = 1;
                for (int j = i + 1; j < sortedNotes.Count && j < i + 20; j++)
                {
                    if (sortedNotes[j].Time <= sortedNotes[i].Time + 10) simultaneous++;
                    else break;
                }
                if (simultaneous > maxPolyphony) maxPolyphony = simultaneous;
            }
            float polyphonyScore = Math.Clamp((maxPolyphony - 1) / 9f, 0f, 1f); // 1-10 range

            // 4. Hand spread (range of note numbers used)
            int minNote = notes.Min(n => (int)n.NoteNumber);
            int maxNote = notes.Max(n => (int)n.NoteNumber);
            int spread = maxNote - minNote;
            float spreadScore = Math.Clamp((spread - 12) / 60f, 0f, 1f); // 1 octave to 6 octaves

            // Weighted combination
            float rawScore = densityScore * 0.35f + tempoScore * 0.20f + polyphonyScore * 0.25f + spreadScore * 0.20f;
            return (float)Math.Round(rawScore * 5f, 1); // Scale to 0-5 stars
        }
        catch
        {
            return 0f;
        }
    }
}
