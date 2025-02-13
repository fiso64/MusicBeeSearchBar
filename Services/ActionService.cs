﻿using MusicBeePlugin.Config;
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
                string artistValue = action.UseSortArtist ? result.Values.SortArtist : result.Values.Artist;

                if (result.Type == ResultType.Artist)
                    query = (action.SearchAddPrefix ? "A:" : "") + artistValue;
                else if (result.Type == ResultType.Album)
                    query = (action.SearchAddPrefix ? "AL:" : "") + result.Values.Album;
                else
                    query = (action.SearchAddPrefix ? "T:" : "") + result.Values.TrackTitle;
            }

            if (!action.UseLeftSidebar)
            {
                var searchBox = MusicBeeHelpers.FocusSearchBox();
                WinApiHelpers.SetEditText(searchBox, query);
                WinApiHelpers.SendEnterKey(searchBox);

                if (!action._actionExecuted)
                {
                    Thread.Sleep(100);
                    WinApiHelpers.SendEnterKey(searchBox);
                }

                if (action.ClearSearchBarTextAfterSearch)
                {
                    Thread.Sleep(50);
                    WinApiHelpers.SetEditText(searchBox, "");
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

            MetaDataType field1, field2 = 0;
            string value1, value2 = null;
            string artistValue = action.UseSortArtist ? result.Values.SortArtist : result.Values.Artist;

            if (result.Type == ResultType.Artist)
            {
                field1 = action.UseSortArtist ? MetaDataType.SortArtist : MetaDataType.Artist;
                value1 = artistValue;
            }
            else if (result.Type == ResultType.Album)
            {
                field1 = MetaDataType.Album;
                value1 = result.Values.Album;
                field2 = action.UseSortArtist ? MetaDataType.SortAlbumArtist : MetaDataType.AlbumArtist;
                value2 = action.UseSortArtist ? result.Values.SortAlbumArtist : result.Values.AlbumArtist;
            }
            else
            {
                field1 = MetaDataType.TrackTitle;
                value1 = result.Values.TrackTitle;
                field2 = action.UseSortArtist ? MetaDataType.SortArtist : MetaDataType.Artist;
                value2 = artistValue;
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

                if (result.Type == ResultType.Artist)
                {
                    query = MusicBeeHelpers.ConstructLibraryQuery(
                        (MetaDataType.Artist, ComparisonType.Is, result.Values.Artist)
                    );
                }
                else
                {
                    query = MusicBeeHelpers.ConstructLibraryQuery(
                        (MetaDataType.Album, ComparisonType.Is, result.Values.Album),
                        (MetaDataType.AlbumArtist, ComparisonType.Is, result.Values.AlbumArtist)
                    );
                }

                mbApi.Library_QueryFilesEx(query, out string[] files);
                return files;
            }
            else if (result.Type == ResultType.Playlist)
            {
                mbApi.Playlist_QueryFilesEx(result.Values.Filepath, out string[] files);
                return files;
            }
            else
            {
                return new string[] { result.Values.Filepath };
            }
        }
    }
}
