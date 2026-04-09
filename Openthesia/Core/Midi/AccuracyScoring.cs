using Melanchall.DryWetMidi.Interaction;
using System.Collections.Concurrent;

namespace Openthesia.Core.Midi;

public static class AccuracyScoring
{
    public struct NoteAttempt
    {
        public int NoteNumber;
        public long ExpectedTime;
        public bool Hit;
        public float TimingError; // Difference in seconds
    }

    private static ConcurrentBag<NoteAttempt> _attempts = new();
    private static HashSet<long> _expectedNoteTimes = new();
    private static int _totalPossibleNotes = 0;
    private static int _notesHit = 0;

    public static bool IsTracking { get; private set; }

    public static void StartSession()
    {
        _attempts = new ConcurrentBag<NoteAttempt>();
        _expectedNoteTimes.Clear();
        _totalPossibleNotes = MidiFileData.Notes?.Count() ?? 0;
        _notesHit = 0;
        IsTracking = true;
    }

    public static void StopSession()
    {
        IsTracking = false;
    }

    public static void RecordNote(int noteNumber, long expectedTime, bool hit, float timingError = 0)
    {
        if (!IsTracking) return;

        _attempts.Add(new NoteAttempt 
        { 
            NoteNumber = noteNumber, 
            ExpectedTime = expectedTime, 
            Hit = hit, 
            TimingError = timingError 
        });

        if (hit) Interlocked.Increment(ref _notesHit);
    }

    public static float GetAccuracyPercentage()
    {
        if (_totalPossibleNotes == 0) return 100f;
        return (_notesHit / (float)_totalPossibleNotes) * 100f;
    }

    public static IEnumerable<NoteAttempt> GetAttempts() => _attempts;
    public static int TotalNotes => _totalPossibleNotes;
    public static int NotesHit => _notesHit;
}
