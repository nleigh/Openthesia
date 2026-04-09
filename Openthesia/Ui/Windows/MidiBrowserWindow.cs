using IconFonts;
using ImGuiNET;
using Openthesia.Core;
using Openthesia.Core.Midi;
using Openthesia.Settings;
using Openthesia.Ui.Helpers;
using System.Numerics;

namespace Openthesia.Ui.Windows;

public class MidiBrowserWindow : ImGuiWindow
{
    private string _searchBuffer = string.Empty;
    private int _sortColumnIndex = 0;
    private int _sortDirection = 1;
    private bool _favoritesOnly = false;
    private char? _scrollToLetter = null;
    private string _selectedFile = string.Empty;
    private bool _shouldScrollToSelected = false;

    // Cached file list with FileSystemWatcher for instant updates
    private List<string> _cachedMidiFiles = new();
    private volatile bool _fileListDirty = true;
    private List<FileSystemWatcher> _watchers = new();

    private bool _showPlaylists = false;
    private string _newPlaylistName = string.Empty;
    private string _hoveredFile = string.Empty;
    private float _hoverTime = 0f;

    public MidiBrowserWindow()
    {
        _id = Enums.Windows.MidiBrowser.ToString();
        _active = false;
        SetupFileWatchers();
    }

    private void SetupFileWatchers()
    {
        foreach (var watcher in _watchers) watcher.Dispose();
        _watchers.Clear();

        foreach (var midiPath in MidiPathsManager.MidiPaths)
        {
            if (!Directory.Exists(midiPath)) continue;
            var watcher = new FileSystemWatcher(midiPath, "*.mid")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            watcher.Created += (_, _) => _fileListDirty = true;
            watcher.Deleted += (_, _) => _fileListDirty = true;
            watcher.Renamed += (_, _) => _fileListDirty = true;
            _watchers.Add(watcher);
        }
    }

    private void PlaySong(string file, SongState songState)
    {
        PreviewManager.StopPreview(); // Stop any pending preview
        GameStateManager.IncrementPlayCount(file);
        MidiFileHandler.LoadMidiFile(file);
        // we start and stop the playback so we can change the time before playing the song,
        // else falling notes and keypresses are mismatched
        MidiPlayer.Playback.Start();
        MidiPlayer.Playback.Stop();
        WindowsManager.SetWindow(Enums.Windows.ModeSelection);
    }

    private void RenderSearchBar()
    {
        if (ImGui.BeginChild("Searchbar container", new(_io.DisplaySize.X / 1.2f, 50)))
        {
            string favIcon = _favoritesOnly ? FontAwesome6.HeartCircleCheck : FontAwesome6.Heart;
            if (_favoritesOnly)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiTheme.HtmlToVec4("#EF4444"));
            }
            if (ImGui.Button(favIcon))
            {
                _favoritesOnly = !_favoritesOnly;
            }
            if (_favoritesOnly)
            {
                ImGui.PopStyleColor();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Toggle Favorites Only");
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 50); // Leave space for trailing label if any
            ImGui.InputTextWithHint("##search_input", $"Search {FontAwesome6.MagnifyingGlass}...", ref _searchBuffer, 1000);
            ImGui.EndChild();
        }
    }

    private void RenderBrowser()
    {
        Drawings.RenderMatrixBackground();

        // browser theme
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.192f, 0.192f, 0.192f, 1f) * 0.8f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, ImGuiUtils.FixedSize(new Vector2(10)));

        using (AutoFont font22 = new(FontController.GetFontOfSize(22)))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 2f);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10f);
            ImGui.SetNextWindowPos(new Vector2((_io.DisplaySize.X - _io.DisplaySize.X / 1.2f) / 2, ImGuiUtils.FixedSize(new Vector2(120)).Y));
            Vector2 containerSize = _io.DisplaySize / 1.2f;
            if (ImGui.BeginChild("Midi browser container", containerSize, ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Border))
            {
                ImGui.PopStyleVar(2);

                ImGui.Text($"{FontAwesome6.Folder} MIDI File Browser");
                ImGui.SameLine(ImGui.GetWindowWidth() - 350);
                if (ImGui.Button($"{FontAwesome6.ListUl} Playlists", new Vector2(120, 30)))
                {
                    _showPlaylists = !_showPlaylists;
                }
                ImGui.SameLine(ImGui.GetWindowWidth() - 220);
                if (ImGui.Button($"{FontAwesome6.ArrowsRotate} Refresh Metadata", new Vector2(180, 30)))
                {
                    // Force file list refresh and re-setup watchers
                    _fileListDirty = true;
                    SetupFileWatchers();

                    foreach (var midiPath in MidiPathsManager.MidiPaths)
                    {
                        if (Directory.Exists(midiPath))
                        {
                            var files = Directory.GetFiles(midiPath, "*.mid");
                            foreach(var file in files)
                            {
                                MetadataService.QueueMetadataFetch(file);
                            }
                        }
                    }
                }

                ImGui.Spacing();
                RenderSearchBar();
                ImGui.Separator();

                var availRegion = ImGui.GetContentRegionAvail();
                float playlistWidth = _showPlaylists ? 280f : 0f;
                float detailWidth = !string.IsNullOrEmpty(_selectedFile) ? availRegion.X * 0.3f : 0f;

                if (_showPlaylists)
                {
                    if (ImGui.BeginChild("Playlist Sidebar", new Vector2(playlistWidth, availRegion.Y), ImGuiChildFlags.Border))
                    {
                        RenderPlaylistSidebar();
                    }
                    ImGui.EndChild();
                    ImGui.SameLine();
                }

                // Alphabet Scroll Sidebar (Moved to the left of the table)
                if (ImGui.BeginChild("Alphabet Scroll", new Vector2(40f, availRegion.Y)))
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.2f));
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));
                    ImGui.PushFont(FontController.Font16_Icon16);
                    
                    float childHeight = ImGui.GetContentRegionAvail().Y;
                    var letters = "#ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                    float btnHeight = childHeight / letters.Length;
                    
                    foreach(var letter in letters)
                    {
                        if (ImGui.Button(letter.ToString(), new Vector2(40f, btnHeight)))
                        {
                            _scrollToLetter = letter;
                        }
                    }
                    ImGui.PopFont();
                    ImGui.PopStyleVar();
                    ImGui.PopStyleColor(3);
                    ImGui.EndChild();
                }
                ImGui.SameLine();

                if (ImGui.BeginChild("Midi file list", new Vector2(availRegion.X - playlistWidth - detailWidth - 50f, availRegion.Y)))
                {
                    if (ImGui.BeginTable("File Table", 12, ImGuiTableFlags.PadOuterX | ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY, ImGui.GetContentRegionAvail()))
                    {
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.WidthFixed, 30f);
                        ImGui.TableSetupColumn("Art", ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.WidthFixed, 54f);
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultSort);
                        ImGui.TableSetupColumn("Artist", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Album", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Length", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("BPM", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("Year", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("Key", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("Difficulty", ImGuiTableColumnFlags.WidthFixed, 80f);
                        ImGui.TableSetupColumn("Plays", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("Fav", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupScrollFreeze(0, 1); // Freeze the top row (headers)
                        ImGui.TableHeadersRow();

                        unsafe
                        {
                            ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();
                            if (sortSpecs.NativePtr != null)
                            {
                                if (sortSpecs.SpecsCount > 0)
                                {
                                    if (sortSpecs.SpecsDirty)
                                    {
                                        _sortColumnIndex = sortSpecs.Specs.ColumnIndex;
                                        _sortDirection = sortSpecs.Specs.SortDirection == ImGuiSortDirection.Descending ? -1 : 1;
                                        
                                        if (!string.IsNullOrEmpty(_selectedFile))
                                        {
                                            _shouldScrollToSelected = true;
                                            _scrollToLetter = null;
                                        }
                                        
                                        sortSpecs.SpecsDirty = false;
                                    }
                                }
                            }
                        }

                        // Refresh cached file list when FileSystemWatcher detects changes
                        if (_fileListDirty)
                        {
                            _cachedMidiFiles.Clear();
                            foreach (var midiPath in MidiPathsManager.MidiPaths)
                            {
                                if (Directory.Exists(midiPath))
                                {
                                    var files = Directory.GetFiles(midiPath, "*.mid");
                                    _cachedMidiFiles.AddRange(files);
                                }
                            }
                            _fileListDirty = false;
                        }

                        var sortedFiles = SortFiles(_cachedMidiFiles);
                        foreach (var file in sortedFiles)
                        {
                            string fileName = Path.GetFileName(file);
                            if (_searchBuffer != string.Empty)
                            {
                                string search = _searchBuffer.ToLower();
                                SongState searchState = GameStateManager.GetSongState(file);
                                bool matchesSearch = fileName.ToLower().Contains(search)
                                    || (searchState.Artist != null && searchState.Artist.ToLower().Contains(search))
                                    || (searchState.Title != null && searchState.Title.ToLower().Contains(search))
                                    || (searchState.Album != null && searchState.Album.ToLower().Contains(search));
                                if (!matchesSearch) continue;
                            }

                            SongState songState = GameStateManager.GetSongState(file);
                            
                            // Queue metadata fetch in background (only if not already fetched)
                            MetadataService.QueueMetadataFetch(file);

                            if (_favoritesOnly && !songState.IsFavorite)
                                continue;

                            bool shouldScrollHere = false;
                            
                            if (_shouldScrollToSelected && file == _selectedFile)
                            {
                                shouldScrollHere = true;
                                _shouldScrollToSelected = false;
                            }
                            else if (_scrollToLetter.HasValue)
                            {
                                string compareStr = "";
                                if (_sortColumnIndex == 1) compareStr = Path.GetFileName(file);
                                else if (_sortColumnIndex == 2) compareStr = songState.Artist ?? "";
                                else if (_sortColumnIndex == 3) compareStr = songState.Album ?? "";
                                else compareStr = Path.GetFileName(file); // fallback to Name

                                if (compareStr.Length > 0)
                                {
                                    char firstChar = char.ToUpperInvariant(compareStr[0]);
                                    if (_scrollToLetter == '#')
                                    {
                                        if (!char.IsLetter(firstChar)) shouldScrollHere = true;
                                    }
                                    else if (firstChar == _scrollToLetter.Value)
                                    {
                                        shouldScrollHere = true;
                                    }
                                }
                            }

                            ImGui.TableNextRow(ImGuiTableRowFlags.None, 54f);
                            if (shouldScrollHere)
                            {
                                // Focus specifically on this item
                                ImGui.SetScrollHereY(0.5f);
                                _scrollToLetter = null;
                                _shouldScrollToSelected = false;
                            }
                            
                            // Play Column
                            ImGui.TableSetColumnIndex(0);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 15f);
                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.529f, 0.784f, 0.325f, 1f));
                            if (ImGui.Button($"{FontAwesome6.Play}##play_{file}"))
                            {
                                PlaySong(file, songState);
                            }
                            ImGui.PopStyleColor();

                            // Art Column
                            ImGui.TableSetColumnIndex(1);
                            nint texPtr = TextureCache.GetTexture(songState.ThumbnailPath);
                            if (texPtr != IntPtr.Zero)
                                ImGui.Image(texPtr, new Vector2(50, 50));
                            else
                            {
                                ImGui.PushFont(FontController.Font16_Icon16);
                                ImGui.Text($"{FontAwesome6.Image}");
                                ImGui.PopFont();
                            }

                            // Name Column
                            ImGui.TableSetColumnIndex(2);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 15f); // center vertically against large image
                            if (ImGui.Selectable(Path.GetFileName(file) + "##" + file, _selectedFile == file, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick))
                            {
                                _selectedFile = file;
                            }
                            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            {
                                PlaySong(file, songState);
                            }

                            // Preview on hover (1.5s delay)
                            if (ImGui.IsItemHovered())
                            {
                                if (_hoveredFile != file)
                                {
                                    _hoveredFile = file;
                                    _hoverTime = 0f;
                                    PreviewManager.StopPreview();
                                }
                                else
                                {
                                    _hoverTime += ImGui.GetIO().DeltaTime;
                                    if (_hoverTime >= 1.5f && _hoverTime < 1.55f) 
                                    {
                                        PreviewManager.StartPreview(file);
                                    }
                                }
                            }
                            else if (_hoveredFile == file)
                            {
                                _hoveredFile = string.Empty;
                                _hoverTime = 0f;
                                PreviewManager.StopPreview();
                            }

                            if (ImGui.BeginPopupContextItem($"context_{file}"))
                            {
                                if (ImGui.MenuItem($"{FontAwesome6.ArrowsRotate} Refresh metadata"))
                                {
                                    MetadataService.QueueMetadataFetch(file, true);
                                }
                                
                                ImGui.Separator();
                                
                                if (ImGui.MenuItem($"{FontAwesome6.ListUl} Add to Queue"))
                                {
                                    SongQueueManager.AddToQueue(file);
                                }

                                if (ImGui.BeginMenu($"{FontAwesome6.Plus} Add to Playlist"))
                                {
                                    if (GameStateManager.State.Playlists.Count == 0)
                                    {
                                        ImGui.TextDisabled("No playlists found");
                                    }
                                    foreach (var playlist in GameStateManager.State.Playlists)
                                    {
                                        if (ImGui.MenuItem(playlist.Name))
                                        {
                                            if (!playlist.FilePaths.Contains(file))
                                            {
                                                playlist.FilePaths.Add(file);
                                                GameStateManager.SaveState();
                                            }
                                        }
                                    }
                                    ImGui.EndMenu();
                                }
                                ImGui.EndPopup();
                            }

                            // Artist Column
                            ImGui.TableSetColumnIndex(3);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 15f);
                            ImGui.Text(songState.Artist ?? "Unknown");

                            // Album Column
                            ImGui.TableSetColumnIndex(4);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 15f);
                            ImGui.Text(songState.Album ?? "-");

                            // Length Column
                            ImGui.TableSetColumnIndex(5);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 15f);
                            if (songState.LengthSeconds > 0)
                            {
                                TimeSpan t = TimeSpan.FromSeconds((int)songState.LengthSeconds);
                                ImGui.Text($"{t.Minutes:D2}:{t.Seconds:D2}");
                            }
                            else
                            {
                                ImGui.Text("-");
                            }

                            // BPM Column
                            ImGui.TableSetColumnIndex(6);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 15f);
                            ImGui.Text(songState.Bpm > 0 ? songState.Bpm.ToString() : "-");

                            // Year Column
                            ImGui.TableSetColumnIndex(7);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 15f);
                            ImGui.Text(songState.Year > 0 ? songState.Year.ToString() : "-");

                            // Key Column
                            ImGui.TableSetColumnIndex(8);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 15f);
                            ImGui.Text(songState.KeySignature ?? "-");

                            // Difficulty Column
                            ImGui.TableSetColumnIndex(9);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 15f);
                            if (songState.Difficulty.HasValue && songState.Difficulty > 0)
                            {
                                float diff = songState.Difficulty.Value;
                                // Color: green(easy) -> yellow(medium) -> red(hard)
                                Vector4 diffColor = diff <= 2f
                                    ? new Vector4(0.2f, 0.9f, 0.3f, 1f)
                                    : diff <= 3.5f
                                        ? new Vector4(1f, 0.8f, 0.1f, 1f)
                                        : new Vector4(1f, 0.3f, 0.2f, 1f);
                                ImGui.PushStyleColor(ImGuiCol.Text, diffColor);
                                int fullStars = (int)diff;
                                string stars = new string('★', fullStars) + (diff - fullStars >= 0.5f ? "½" : "");
                                ImGui.Text(stars);
                                ImGui.PopStyleColor();
                                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{diff:F1} / 5.0");
                            }
                            else
                            {
                                ImGui.Text("-");
                            }

                            // Plays Column
                            ImGui.TableSetColumnIndex(10);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 15f);
                            ImGui.Text(songState.PlayCount.ToString());

                            // Fav Column
                            ImGui.TableSetColumnIndex(11);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 15f);
                            if (songState.IsFavorite) ImGui.PushStyleColor(ImGuiCol.Text, ImGuiTheme.HtmlToVec4("#EF4444"));
                            if (ImGui.Button($"{(songState.IsFavorite ? FontAwesome6.HeartCircleCheck : FontAwesome6.Heart)}##{file}"))
                            {
                                GameStateManager.SetFavorite(file, !songState.IsFavorite);
                            }
                            if (songState.IsFavorite) ImGui.PopStyleColor();
                        }

                        ImGui.EndTable();
                    }
                    ImGui.EndChild();
                }

                if (!string.IsNullOrEmpty(_selectedFile))
                {
                    ImGui.SameLine();
                    RenderDetailPanel(detailWidth);
                }

                // Removed from here (moved to left)
                ImGui.EndChild();
            }

            ImGui.PopStyleColor(); // child bg
            ImGui.PopStyleVar(); // window padding
        }
    }

    private List<string> SortFiles(List<string> midiFiles)
    {
        return _sortColumnIndex switch
        {
            2 => _sortDirection == 1 ? midiFiles.OrderBy(p => Path.GetFileName(p)).ToList() : midiFiles.OrderByDescending(p => Path.GetFileName(p)).ToList(), // Name
            3 => _sortDirection == 1 ? midiFiles.OrderBy(p => GameStateManager.GetSongState(p).Artist).ToList() : midiFiles.OrderByDescending(p => GameStateManager.GetSongState(p).Artist).ToList(), // Artist
            4 => _sortDirection == 1 ? midiFiles.OrderBy(p => GameStateManager.GetSongState(p).Album).ToList() : midiFiles.OrderByDescending(p => GameStateManager.GetSongState(p).Album).ToList(), // Album
            5 => _sortDirection == 1 ? midiFiles.OrderBy(p => GameStateManager.GetSongState(p).LengthSeconds).ToList() : midiFiles.OrderByDescending(p => GameStateManager.GetSongState(p).LengthSeconds).ToList(), // Length
            6 => _sortDirection == 1 ? midiFiles.OrderBy(p => GameStateManager.GetSongState(p).Bpm).ToList() : midiFiles.OrderByDescending(p => GameStateManager.GetSongState(p).Bpm).ToList(), // BPM
            7 => _sortDirection == 1 ? midiFiles.OrderBy(p => GameStateManager.GetSongState(p).Year).ToList() : midiFiles.OrderByDescending(p => GameStateManager.GetSongState(p).Year).ToList(), // Year
            8 => _sortDirection == 1 ? midiFiles.OrderBy(p => GameStateManager.GetSongState(p).KeySignature).ToList() : midiFiles.OrderByDescending(p => GameStateManager.GetSongState(p).KeySignature).ToList(), // Key
            9 => _sortDirection == 1 ? midiFiles.OrderBy(p => GameStateManager.GetSongState(p).Difficulty).ToList() : midiFiles.OrderByDescending(p => GameStateManager.GetSongState(p).Difficulty).ToList(), // Difficulty
            10 => _sortDirection == 1 ? midiFiles.OrderBy(p => GameStateManager.GetSongState(p).PlayCount).ToList() : midiFiles.OrderByDescending(p => GameStateManager.GetSongState(p).PlayCount).ToList(), // Plays
            11 => _sortDirection == 1 ? midiFiles.OrderBy(p => GameStateManager.GetSongState(p).IsFavorite).ToList() : midiFiles.OrderByDescending(p => GameStateManager.GetSongState(p).IsFavorite).ToList(), // Fav
            _ => midiFiles
        };
    }

    private void RenderPlaylistSidebar()
    {
        using (AutoFont font18 = new(FontController.GetFontOfSize(18)))
        {
            ImGui.Text($"{FontAwesome6.ListUl} Playlists");
            ImGui.Separator();

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 40);
            ImGui.InputTextWithHint("##new_playlist", "New Playlist...", ref _newPlaylistName, 100);
            ImGui.SameLine();
            if (ImGui.Button($"{FontAwesome6.Plus}"))
            {
                if (!string.IsNullOrEmpty(_newPlaylistName))
                {
                    GameStateManager.State.Playlists.Add(new Playlist { Name = _newPlaylistName });
                    GameStateManager.SaveState();
                    _newPlaylistName = string.Empty;
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        foreach (var playlist in GameStateManager.State.Playlists.ToList())
        {
            bool open = ImGui.TreeNodeEx($"{playlist.Name} ({playlist.FilePaths.Count})###{playlist.Name}", ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.DefaultOpen);
            
            // Context menu for playlist
            if (ImGui.BeginPopupContextItem($"playlist_ctx_{playlist.Name}"))
            {
                if (ImGui.MenuItem($"{FontAwesome6.Play} Play Playlist"))
                {
                    if (playlist.FilePaths.Count > 0)
                    {
                        SongQueueManager.SetQueue(playlist.FilePaths, 0);
                        PlaySong(playlist.FilePaths[0], GameStateManager.GetSongState(playlist.FilePaths[0]));
                    }
                }
                if (ImGui.MenuItem($"{FontAwesome6.Trash} Delete Playlist"))
                {
                    GameStateManager.State.Playlists.Remove(playlist);
                    GameStateManager.SaveState();
                }
                ImGui.EndPopup();
            }

            if (open)
            {
                for (int i = 0; i < playlist.FilePaths.Count; i++)
                {
                    string filePath = playlist.FilePaths[i];
                    string fileName = Path.GetFileName(filePath);
                    
                    ImGui.PushID($"item_{playlist.Name}_{i}");
                    if (ImGui.Selectable($"{fileName}", false, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            SongQueueManager.SetQueue(playlist.FilePaths, i);
                            PlaySong(filePath, GameStateManager.GetSongState(filePath));
                        }
                    }
                    
                    if (ImGui.BeginPopupContextItem())
                    {
                        if (ImGui.MenuItem($"{FontAwesome6.Trash} Remove from Playlist"))
                        {
                            playlist.FilePaths.RemoveAt(i);
                            GameStateManager.SaveState();
                        }
                        ImGui.EndPopup();
                    }
                    ImGui.PopID();
                }
                ImGui.TreePop();
            }
        }
    }

    protected override void OnImGui()
    {
        using (AutoFont font16_icon16 = new(FontController.Font16_Icon16))
        {
            // back button
            ImGui.SetCursorScreenPos(ImGuiUtils.FixedSize(new Vector2(22, 50)));
            if (ImGui.Button(FontAwesome6.ArrowLeftLong, ImGuiUtils.FixedSize(new Vector2(100, 50))))
                WindowsManager.SetWindow(Enums.Windows.Home);

            // open file button
            ImGuiTheme.PushButton(ImGuiTheme.HtmlToVec4("#0EA5E9"), ImGuiTheme.HtmlToVec4("#096E9B"), ImGuiTheme.HtmlToVec4("#0EA5E9"));
            ImGui.SetCursorScreenPos(ImGuiUtils.FixedSize(new Vector2(132f, 50)));
            if (ImGui.Button($"Open file {FontAwesome6.FileImport}", ImGuiUtils.FixedSize(new Vector2(100, 50))))
            {
                if (MidiFileHandler.OpenMidiDialog())
                {
                    /* we start and stop the playback so we can change the time before playing the song,
                      else falling notes and keypresses are mismatched */
                    MidiPlayer.Playback.Start();
                    MidiPlayer.Playback.Stop();
                    WindowsManager.SetWindow(Enums.Windows.ModeSelection);
                }
            }
            ImGuiTheme.PopButton();

            RenderBrowser();
        }
    }

    private void DrawDetailRow(string label, string value)
    {
        ImGui.TextDisabled(label);
        ImGui.SameLine(100 * FontController.DSF);
        ImGui.TextWrapped(value);
    }

    private void RenderDetailPanel(float width)
    {
        // Make the detailed info scrollable if it doesn't fit
        if (ImGui.BeginChild("Detail Panel", new Vector2(width, ImGui.GetContentRegionAvail().Y), ImGuiChildFlags.Border, ImGuiWindowFlags.AlwaysVerticalScrollbar | ImGuiWindowFlags.HorizontalScrollbar))
        {
            SongState songState = GameStateManager.GetSongState(_selectedFile);
            string fileName = Path.GetFileName(_selectedFile);

            // Close button
            ImGui.SetCursorPos(new Vector2(width - 45, 5));
            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            if (ImGui.Button($"{FontAwesome6.Xmark}"))
            {
                _selectedFile = string.Empty;
            }
            ImGui.PopStyleColor();

            ImGui.Spacing();
            ImGui.Spacing();

            // Cover Art (Scalable to width)
            nint texPtr = TextureCache.GetTexture(songState.ThumbnailPath);
            if (texPtr != IntPtr.Zero)
            {
                float imgSize = Math.Min(width - 40, 250);
                ImGui.SetCursorPosX((width - imgSize) / 2);
                ImGui.Image(texPtr, new Vector2(imgSize, imgSize));
            }
            else
            {
                ImGui.SetCursorPosX((width - 50) / 2);
                ImGui.PushFont(FontController.Title);
                ImGui.Text($"{FontAwesome6.Image}");
                ImGui.PopFont();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            // Title & Artist Section (Scrollable or scaled)
            ImGui.PushFont(FontController.Title);
            string title = songState.Title ?? fileName;
            var titleSize = ImGui.CalcTextSize(title);
            
            // Limit font size if title is too enormous or wraps too much
            bool useSmallFont = title.Length > 25 || titleSize.X > (width * 1.5f);
            if (useSmallFont) ImGui.PopFont();
            if (useSmallFont) ImGui.PushFont(FontController.GetFontOfSize((int)(24 * FontController.DSF)));

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 10 * FontController.DSF)); // More vertical space
            
            titleSize = ImGui.CalcTextSize(title); // Recalculate based on active font
            if (titleSize.X > width - 10)
            {
                ImGui.TextWrapped(title);
            }
            else
            {
                ImGui.SetCursorPosX((width - titleSize.X) / 2);
                ImGui.Text(title);
            }
            ImGui.PopFont();

            // Artist
            if (!string.IsNullOrEmpty(songState.Artist))
            {
                ImGui.PushFont(FontController.GetFontOfSize((int)(18 * FontController.DSF)));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1f));
                var artistSize = ImGui.CalcTextSize(songState.Artist);
                ImGui.SetCursorPosX(Math.Max(0, (width - artistSize.X) / 2));
                ImGui.TextWrapped(songState.Artist);
                ImGui.PopStyleColor();
                ImGui.PopFont();
            }
            ImGui.PopStyleVar();

            ImGui.Spacing();
            ImGui.Spacing();

            // Play Button
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.529f, 0.784f, 0.325f, 1f) * 0.8f);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.529f, 0.784f, 0.325f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.529f, 0.784f, 0.325f, 1f) * 1.2f);
            ImGui.PushFont(FontController.GetFontOfSize((int)(30 * FontController.DSF)));
            
            string playBtnText = $"{FontAwesome6.Play} Play";
            var playBtnSize = ImGui.CalcTextSize(playBtnText);
            float btnWidth = Math.Max(playBtnSize.X + 60f * FontController.DSF, Math.Min(200f * FontController.DSF, width - 40f));
            float btnHeight = playBtnSize.Y + 30f * FontController.DSF;
            
            ImGui.SetCursorPosX(Math.Max(0, (width - btnWidth) / 2));
            if (ImGui.Button(playBtnText, new Vector2(btnWidth, btnHeight)))
            {
                PlaySong(_selectedFile, songState);
            }
            ImGui.PopFont();
            ImGui.PopStyleColor(3);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Details
            ImGui.PushFont(FontController.GetFontOfSize((int)(18 * FontController.DSF)));
            DrawDetailRow("Length:", songState.LengthSeconds > 0 ? $"{TimeSpan.FromSeconds(songState.LengthSeconds.Value):mm\\:ss}" : "-");
            DrawDetailRow("BPM:", songState.Bpm > 0 ? songState.Bpm.ToString() : "-");
            DrawDetailRow("Key:", songState.KeySignature ?? "-");
            DrawDetailRow("Album:", songState.Album ?? "-");

            // Difficulty Breakdown
            if (songState.Difficulty.HasValue && songState.Difficulty > 0)
            {
                ImGui.Spacing();
                ImGui.Spacing();
                float diff = songState.Difficulty.Value;
                Vector4 diffColor = diff <= 2f ? new Vector4(0.2f, 0.9f, 0.3f, 1f) : diff <= 3.5f ? new Vector4(1f, 0.8f, 0.1f, 1f) : new Vector4(1f, 0.3f, 0.2f, 1f);

                ImGui.TextDisabled("Difficulty Rating:");
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, diffColor);
                ImGui.ProgressBar(diff / 5f, new Vector2(-1, 20), $"{diff:F1} / 5.0");
                ImGui.PopStyleColor();
            }
            ImGui.PopFont();
        }
        ImGui.EndChild();
    }
}
