# Openthesia Features - Google Antigravity Edition

This fork contains a suite of experimental features and performance optimizations added to Openthesia to create a more powerful, interactive, and visually stunning piano learning experience.

## 🎹 Learning & Performance Features

### 🏅 Note Accuracy Scoring
*   **Real-time Tracking**: Tracks every note hit or miss during MIDI playback.
*   **Scoring UI**: Live score display (Multiplier, Streak, Accuracy Percentage).
*   **Results Heatmap**: Post-performance breakdown showing where you excelled and where you struggled.
*   **MIDI Hand Separation**: Ability to mute left or right hand tracks to practice one hand at a time.

### 🔁 A-B Section Looping
*   Set markers to loop specific segments of a song for targeted practice.
*   Easily repeatable sections for mastering difficult passages.

### ⏱️ Advanced Tempo Control
*   **Dynamic Slider**: Adjust playback speed from 25% up to 200%.
*   **Shortcuts**: Use keyboard keys (numeric +/-) for quick adjustments during play.

### ⭐ Song Difficulty Rating
*   Automated rating system (1-5 stars) based on note density, tempo, hand spread, and polyphony.
*   Helps players find songs that match their skill level.

### ⏲️ Playback Countdown
*   A stylized 3-2-1 countdown before playback begins, ensuring you're ready to start.
*   Pulsing color animations synced to the arrival of the first note.

---

## 🎨 UI/UX Enhancements

### 🌙 Global Theme Engine
*   **Dark/Light Mode**: Seamlessly switch between themes for optimal viewing in any environment.
*   **Fade Transitions**: Smooth, animated transitions between different application windows.

### 💡 Interactive Keyboard Tooltips
*   Hover or press keys to see real-time note names and octave indicators.
*   Improves musical theory comprehension while playing.

### 🔍 Enhanced MIDI Browser
*   **Alphabet Scrollbar**: Quick navigation through large MIDI collections.
*   **Song Preview on Hover**: Hover over a song to hear a short audio preview without starting playback.
*   **Metadata Integration**: Rich display of artist, album, and year alongside the file list.
*   **Sorting & Search**: Advanced sorting by any metadata field and high-performance search filtering.

### 📜 Visual Layout Improvements
*   **Measure / Bar Lines**: Synced to the song's tempo map for better timing visualization.
*   **Pinned Headers**: Table headers stay locked at the top when scrolling through lists.
*   **Playlists & Queue**: Queue up multiple songs or create playlists with auto-advance functionality.

---

### ⚙️ Performance & Core Optimizations

*   **FileSystemWatcher**: The library now detects and adds new MIDI files instantly without needing a manual refresh.
*   **Parallel Metadata Loading**: Uses `SemaphoreSlim` to fetch song metadata in parallel, drastically reducing wait times for large libraries.
*   **LRU Texture Caching**: Intelligent memory management for song thumbnails and UI assets, capping memory usage.
*   **Auto-Save System**: Periodically saves your game state (favorites, play counts, settings) every 60 seconds.
*   **Memory Safety**: Refactored various UI components to ensure disposables are handled correctly and memory leaks are minimized.

---
*Developed with ❤️ by the Google Antigravity AI Team.*
