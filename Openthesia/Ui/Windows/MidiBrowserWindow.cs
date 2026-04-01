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

    public MidiBrowserWindow()
    {
        _id = Enums.Windows.MidiBrowser.ToString();
        _active = false;
    }

    private void PlaySong(string file, SongState songState)
    {
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
            ImGui.InputTextWithHint($"Search {FontAwesome6.MagnifyingGlass}", "Search midi file...", ref _searchBuffer, 1000);
            ImGui.EndChild();
        }
    }

    private void RenderBrowser()
    {
        Drawings.RenderMatrixBackground();

        // browser theme
        ImGui.PushStyleColor(ImGuiCol.ChildBg, ThemeManager.MainBgCol * 0.8f);
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
                ImGui.Spacing();
                RenderSearchBar();
                ImGui.Separator();

                var availRegion = ImGui.GetContentRegionAvail();
                if (ImGui.BeginChild("Midi file list", new Vector2(availRegion.X - 45f, availRegion.Y)))
                {
                    if (ImGui.BeginTable("File Table", 11, ImGuiTableFlags.PadOuterX | ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY, ImGui.GetContentRegionAvail()))
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
                        ImGui.TableSetupColumn("Plays", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("Fav", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableHeadersRow();

                        unsafe
                        {
                            ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();
                            if (sortSpecs.NativePtr != null)
                            {
                                if (sortSpecs.SpecsCount > 0)
                                {
                                    _sortColumnIndex = sortSpecs.Specs.ColumnIndex;
                                    _sortDirection = sortSpecs.Specs.SortDirection == ImGuiSortDirection.Descending ? -1 : 1;
                                }
                            }
                        }

                        List<string> midiFiles = new();
                        foreach (var midiPath in MidiPathsManager.MidiPaths)
                        {
                            var files = Directory.GetFiles(midiPath, "*.mid");
                            midiFiles.AddRange(files);
                        }
                        var sortedFiles = SortFiles(midiFiles);
                        foreach (var file in sortedFiles)
                        {
                            if (!Path.GetFileName(file).ToLower().Contains(_searchBuffer.ToLower()) && _searchBuffer != string.Empty)
                                continue;

                            SongState songState = GameStateManager.GetSongState(file);
                            
                            // Queue metadata fetch in background
                            MetadataService.QueueMetadataFetch(file);

                            if (_favoritesOnly && !songState.IsFavorite)
                                continue;

                            bool shouldScrollHere = false;
                            if (_scrollToLetter.HasValue)
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
                                ImGui.SetScrollHereY(0.0f);
                                _scrollToLetter = null;
                            }
                            
                            // Play Column
                            ImGui.TableSetColumnIndex(0);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 15f);
                            ImGui.PushStyleColor(ImGuiCol.Text, ThemeManager.RightHandCol);
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

                            // Plays Column
                            ImGui.TableSetColumnIndex(9);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 15f);
                            ImGui.Text(songState.PlayCount.ToString());

                            // Fav Column
                            ImGui.TableSetColumnIndex(10);
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

                ImGui.SameLine();
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
            9 => _sortDirection == 1 ? midiFiles.OrderBy(p => GameStateManager.GetSongState(p).PlayCount).ToList() : midiFiles.OrderByDescending(p => GameStateManager.GetSongState(p).PlayCount).ToList(), // Plays
            10 => _sortDirection == 1 ? midiFiles.OrderBy(p => GameStateManager.GetSongState(p).IsFavorite).ToList() : midiFiles.OrderByDescending(p => GameStateManager.GetSongState(p).IsFavorite).ToList(), // Fav
            _ => midiFiles
        };
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
}
