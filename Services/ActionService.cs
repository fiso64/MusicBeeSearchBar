using MusicBeePlugin.Config;
using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using static MusicBeePlugin.Plugin;


namespace MusicBeePlugin.Services
{
    public class ActionService
    {
        private SearchActionsConfig actionsConfig;

        public ActionService(SearchActionsConfig actionsConfig) 
        {
            this.actionsConfig = actionsConfig;
        }

        public bool RunAction(string searchBoxText, SearchResult result, KeyEventArgs keyEvent)
        {
            ActionConfig actionCfg;

            if (result.Type == ResultType.Artist)
                actionCfg = actionsConfig.ArtistAction;
            else if (result.Type == ResultType.Album)
                actionCfg = actionsConfig.AlbumAction;
            else if (result.Type == ResultType.Song)
                actionCfg = actionsConfig.SongAction;
            else if (result.Type == ResultType.Playlist)
                actionCfg = actionsConfig.PlaylistAction;
            else return true;

            BaseActionData action;
            if (keyEvent.Control && keyEvent.Shift)
                action = actionCfg.CtrlShift;
            else if (keyEvent.Control)
                action = actionCfg.Ctrl;
            else if (keyEvent.Shift)
                action = actionCfg.Shift;
            else
                action = actionCfg.Default;

            if (action == null)
                return false;

            if (action is OpenFilterInTabActionData filterAction)
            {
                OpenFilter(result, filterAction);
            }
            else if (action is SearchInTabActionData searchAction)
            {
                Search(searchBoxText, result, searchAction);
            }
            else if (action is PlayActionData playAction)
            {
                Play(result, playAction);
            }
            else if (action is QueueNextActionData queueNextAction)
            {
                QueueNext(result, queueNextAction);
            }
            else if (action is QueueLastActionData queueLastAction)
            {
                QueueLast(result, queueLastAction);
            }

            action._actionExecuted = true;

            if (action.FocusMainPanelAfterAction)
                MusicBeeHelpers.FocusMainPanel();

            return true;
        }

        private void RestoreOrFocus()
        {
            if (!WinApiHelpers.IsWindowFocused(mbApi.MB_GetWindowHandle()))
                MusicBeeHelpers.MinimiseRestoreOrFocus();
        }

        private void GotoTab(TabChoice tab)
        {
            var command = ApplicationCommand.None;

            if (tab == TabChoice.CurrentTab)
            {
                command = ApplicationCommand.None;
            }
            else if (tab == TabChoice.NewTab)
            {
                command = ApplicationCommand.FileNewTab;
            }
            else
            {
                char lastChar = tab.ToString().Last();
                if (char.IsDigit(lastChar))
                {
                    string commandString = $"GeneralGotoTab{lastChar}";

                    if (Enum.TryParse(commandString, out ApplicationCommand parsedCommand))
                        command = parsedCommand;
                }
            }

            MusicBeeHelpers.InvokeCommand(command);
        }

        private void Search(string searchBoxText, SearchResult result, SearchInTabActionData action)
        {
            string getSortArtistIfNotHonorific(string artist, string sortArtist)
            {
                if (string.IsNullOrEmpty(sortArtist))
                    return artist;

                var parts = sortArtist.Split(new string[] { ", " }, StringSplitOptions.None);
                if (parts.Length < 2)
                    return sortArtist;

                var potentialHonorific = parts[parts.Length - 1];
                var name = string.Join(", ", parts.Take(parts.Length - 1));

                var reconstructed = $"{potentialHonorific} {name}";
                return reconstructed.Equals(artist, StringComparison.OrdinalIgnoreCase) ? artist : sortArtist;
            }

            RestoreOrFocus();
            GotoTab(action.TabChoice);

            if (action.ToggleSearchEntireLibraryBeforeSearch)
                MusicBeeHelpers.InvokeCommand(ApplicationCommand.GeneralToggleSearchScope);

            string query;
            if (action.UseSearchBarText)
            {
                query = searchBoxText;
            }
            else
            {
                if (result is ArtistResult artistResult)
                {
                    string artist = action.UseSortArtist ? getSortArtistIfNotHonorific(artistResult.Artist, artistResult.SortArtist) : artistResult.Artist;
                    query = (action.SearchAddPrefix ? "A:" : "") + artist;
                }
                else if (result is AlbumResult albumResult)
                {
                    string albumArtist = action.UseSortArtist ? getSortArtistIfNotHonorific(albumResult.AlbumArtist, albumResult.SortAlbumArtist) : albumResult.AlbumArtist;
                    albumArtist = action.SearchAddPrefix ? $"AA:{albumArtist}" : albumArtist;
                    query = albumArtist + " " + (action.SearchAddPrefix ? "AL:" : "") + albumResult.Album;
                }
                else if (result is SongResult songResult)
                {
                    string artist = action.UseSortArtist ? getSortArtistIfNotHonorific(songResult.Artist, songResult.SortArtist) : songResult.Artist;
                    artist = action.SearchAddPrefix ? $"A:{artist}" : artist;
                    query = artist + " " + (action.SearchAddPrefix ? "T:" : "") + songResult.TrackTitle;
                }
                else
                {
                    query = result.DisplayTitle;
                }
            }

            if (!action.UseLeftSidebar)
            {
                var searchBox = MusicBeeHelpers.FocusSearchBox();
                WinApiHelpers.SetEditText(searchBox, query);
                WinApiHelpers.SendEnterKey(searchBox);

                if (!action._actionExecuted)
                {
                    _ = Task.Delay(200).ContinueWith(_ => 
                    {
                        WinApiHelpers.SendEnterKey(searchBox);
                    });
                }

                if (action.ClearSearchBarTextAfterSearch)
                {
                    _ = Task.Delay(action._actionExecuted ? 50 : 250).ContinueWith(_ => 
                    {
                        WinApiHelpers.SetEditText(searchBox, "");
                    });
                }
            }
            else
            {
                MusicBeeHelpers.FocusLeftSidebar();

                Thread.Sleep(50);

                WinApiHelpers.SendKey(Keys.Home);

                SendKeys.SendWait("="); // type a character to reveal the search box

                int count = 0;
                IntPtr searchBar = IntPtr.Zero;

                while (count++ < 20)
                {
                    Thread.Sleep(50);
                    searchBar = WinApiHelpers.GetFocus();
                    if (WinApiHelpers.IsEdit(searchBar))
                        break;
                }

                if (searchBar != IntPtr.Zero)
                {
                    WinApiHelpers.SetEditText(searchBar, query);

                    if (action.ClearSearchBarTextAfterSearch)
                    {
                        Thread.Sleep(50);
                        WinApiHelpers.SendKey(Keys.Escape);
                    }
                }
            }

            if (action.ToggleSearchEntireLibraryBeforeSearch)
                MusicBeeHelpers.InvokeCommand(ApplicationCommand.GeneralToggleSearchScope);
        }

        private void OpenFilter(SearchResult result, OpenFilterInTabActionData action)
        {
            if (result.Type == ResultType.Playlist)
            {
                return;
            }

            RestoreOrFocus();
            GotoTab(action.TabChoice);

            if (action.GoBackBeforeOpenFilter)
                MusicBeeHelpers.InvokeCommand(ApplicationCommand.GeneralGoBack);

            MetaDataType field1 = 0, field2 = 0;
            string value1 = null, value2 = null;

            if (result is ArtistResult artistResult)
            {
                field1 = action.UseSortArtist ? MetaDataType.SortArtist : MetaDataType.Artist;
                value1 = action.UseSortArtist && !string.IsNullOrEmpty(artistResult.SortArtist) ? artistResult.SortArtist : artistResult.Artist;
            }
            else if (result is AlbumResult albumResult)
            {
                field1 = MetaDataType.Album;
                value1 = albumResult.Album;
                field2 = action.UseSortArtist ? MetaDataType.SortAlbumArtist : MetaDataType.AlbumArtist;
                value2 = action.UseSortArtist && !string.IsNullOrEmpty(albumResult.SortAlbumArtist) ? albumResult.SortAlbumArtist : albumResult.AlbumArtist;
            }
            else if (result is SongResult songResult)
            {
                field1 = MetaDataType.TrackTitle;
                value1 = songResult.TrackTitle;
                field2 = action.UseSortArtist ? MetaDataType.SortArtist : MetaDataType.Artist;
                value2 = action.UseSortArtist && !string.IsNullOrEmpty(songResult.SortArtist) ? songResult.SortArtist : songResult.Artist;
            }

            if (value2 == null)
            {
                field2 = field1;
                value2 = value1;
            }

            mbApi.MB_OpenFilterInTab(field1, ComparisonType.Is, value1, field2, ComparisonType.Is, value2);
        }

        private void Play(SearchResult result, PlayActionData action)
        {
            mbApi.NowPlayingList_Clear();
            var files = GetItemFiles(result);

            if (files.Length == 0)
                return;

            if (action.ShufflePlay)
                files = files.OrderBy(x => Guid.NewGuid()).ToArray();

            if (files.Length > 1)
            {
                mbApi.NowPlayingList_PlayNow(files[0]);
                mbApi.NowPlayingList_QueueFilesNext(files.Skip(1).ToArray());
            }
            else
            {
                mbApi.NowPlayingList_PlayNow(files[0]);
            }
        }

        private void QueueNext(SearchResult result, QueueNextActionData action)
        {
            var files = GetItemFiles(result);
            if (action.ShufflePlay)
                files = files.OrderBy(x => Guid.NewGuid()).ToArray();
            
            if (action.ClearQueueBeforeAdd)
                mbApi.NowPlayingList_Clear();
            
            mbApi.NowPlayingList_QueueFilesNext(files);

            if (action.ClearQueueBeforeAdd && mbApi.Player_GetPlayState() == PlayState.Playing)
                mbApi.Player_PlayPause();
        }

        private void QueueLast(SearchResult result, QueueLastActionData action)
        {
            var files = GetItemFiles(result);
            if (action.ShufflePlay)
                files = files.OrderBy(x => Guid.NewGuid()).ToArray();
            mbApi.NowPlayingList_QueueFilesLast(files);
        }

        private string[] GetItemFiles(SearchResult result)
        {
            if (result.Type == ResultType.Album || result.Type == ResultType.Artist)
            {
                string query;

                if (result is ArtistResult artistResult)
                {
                    query = MusicBeeHelpers.ConstructLibraryQuery(
                        (MetaDataType.Artist, ComparisonType.Is, artistResult.Artist)
                    );
                }
                else if (result is AlbumResult albumResult)
                {
                    query = MusicBeeHelpers.ConstructLibraryQuery(
                        (MetaDataType.Album, ComparisonType.Is, albumResult.Album),
                        (MetaDataType.AlbumArtist, ComparisonType.Is, albumResult.AlbumArtist)
                    );
                }
                else query = "";

                mbApi.Library_QueryFilesEx(query, out string[] files);
                return files;
            }
            else if (result is PlaylistResult playlistResult)
            {
                mbApi.Playlist_QueryFilesEx(playlistResult.PlaylistPath, out string[] files);
                return files;
            }
            else if (result is SongResult songResult)
            {
                return new string[] { songResult.Filepath };
            }
            else
            {
                return new string[] { };
            }
        }
    }
}
