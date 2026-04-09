using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using System.Timers;
using System;
using System.Linq;
using Openthesia.Core;
using Openthesia.Settings;

namespace Openthesia.Core.Midi
{
    public static class PreviewManager
    {
        private static Playback _previewPlayback;
        private static string _currentFile;
        private static System.Timers.Timer _previewTimer;

        public static void StartPreview(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || _currentFile == filePath) return;
            
            // Don't preview if something is already playing in the main player
            if (MidiPlayer.IsTimerRunning) return;

            StopPreview();

            try
            {
                _currentFile = filePath;
                var midiFile = MidiFile.Read(filePath);
                
                // If using external MIDI device, use that. Otherwise use software synth via events.
                if (DevicesManager.ODevice != null)
                {
                    _previewPlayback = midiFile.GetPlayback(DevicesManager.ODevice);
                }
                else
                {
                    _previewPlayback = midiFile.GetPlayback();
                    _previewPlayback.EventPlayed += (s, e) =>
                    {
                        if (e.Event is NoteOnEvent noteOn)
                            MidiPlayer.SoundFontEngine?.PlayNote(noteOn.Channel, noteOn.NoteNumber, noteOn.Velocity);
                        else if (e.Event is NoteOffEvent noteOff)
                            MidiPlayer.SoundFontEngine?.StopNote(noteOff.Channel, noteOff.NoteNumber);
                    };
                }

                // Seek to ~30% or a densest part (simplified: just 25%)
                var duration = midiFile.GetDuration<MetricTimeSpan>();
                var startTime = new MetricTimeSpan((long)(duration.TotalMicroseconds * 0.25));
                _previewPlayback.MoveToTime(startTime);
                
                _previewPlayback.Start();
                
                _previewTimer = new System.Timers.Timer(6000); // 6s preview
                _previewTimer.Elapsed += (s, e) => StopPreview();
                _previewTimer.AutoReset = false;
                _previewTimer.Start();
            }
            catch (Exception)
            {
                _currentFile = null;
            }
        }

        public static void StopPreview()
        {
            if (_previewPlayback != null)
            {
                _previewPlayback.Stop();
                _previewPlayback.Dispose();
                _previewPlayback = null;
                
                // Kill all notes in case any were left hanging
                MidiPlayer.SoundFontEngine?.StopAllNote(0);
            }
            _currentFile = null;
            if (_previewTimer != null)
            {
                _previewTimer.Stop();
                _previewTimer.Dispose();
                _previewTimer = null;
            }
        }
    }
}
