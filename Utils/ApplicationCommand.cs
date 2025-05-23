﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using static MusicBeePlugin.Plugin;
using static MusicBeePlugin.Utils.ApplicationCommand;

namespace MusicBeePlugin.Utils
{
    public enum ApplicationCommand
    {
        None,
        RatingSelectedFiles0,
        RatingSelectedFiles05,
        RatingSelectedFiles1,
        RatingSelectedFiles15,
        RatingSelectedFiles2,
        RatingSelectedFiles25,
        RatingSelectedFiles3,
        RatingSelectedFiles35,
        RatingSelectedFiles4,
        RatingSelectedFiles45,
        RatingSelectedFiles5,
        RatingNowPlaying0,
        RatingNowPlaying05,
        RatingNowPlaying1,
        RatingNowPlaying15,
        RatingNowPlaying2,
        RatingNowPlaying25,
        RatingNowPlaying3,
        RatingNowPlaying35,
        RatingNowPlaying4,
        RatingNowPlaying45,
        RatingNowPlaying5,
        EditSelectAll,
        EditClickedColumn,
        EditProperties,
        EditPropertiesPlaying,
        EditReopen,
        EditSave,
        EditSaveGotoNext,
        EditUndo,
        EditOpenTagInspector,
        EditCustomSearch,
        EditHighlightingRules,
        EditPreferences,
        EditTimestamp,
        EditConfigureCustomTags,
        EditCut,
        EditCopy,
        EditCopyAll,
        EditPaste,
        EditCropSelectedFiles,
        FileScanNewMedia,
        GeneralReloadHard,
        ToolsRescan,
        FileOpenUrl,
        GeneralResetFilters,
        GeneralGoBack,
        GeneralGoForward,
        GeneralGotoSearch,
        GeneralGotoNowPlayingTrack,
        GeneralLocateInCurrentNowPlayingTrack,
        GeneralLocateSelectedTrack,
        FileOpenWindowsExplorer,
        GeneralLocateInComputerSelectedTrack,
        GeneralReload,
        GeneralRefreshSmartPlaylist,
        GeneralRestart,
        GeneralExitApplication,
        GeneralActivateAutoShutdown,
        GeneralToggleSearchScope,
        MultiMediaNext,
        MultiMediaPlayPause,
        MultiMediaPrevious,
        MultiMediaStop,
        NowPlayingBookmark,
        NowPlayingListClear,
        NowPlayingListClearBefore,
        PlaybackNextTrack,
        PlaybackJumpToRandom,
        PlaybackNextAlbum,
        PlaybackPlayPause,
        PlaybackPreviousTrack,
        PlaybackPreviousAlbum,
        PlaybackSkipBack,
        PlaybackSkipForward,
        PlaybackMediumSkipBack,
        PlaybackMediumSkipForward,
        PlaybackLargeSkipBack,
        PlaybackLargeSkipForward,
        PlaybackStop,
        PlaybackStopAfterCurrent,
        PlaybackPlayNow,
        PlaybackPlayAlbumNow,
        PlaybackPlayNext,
        PlaybackPlayLast,
        PlaybackToggleSkip,
        PlaybackStartAutoDj,
        PlaybackPlayAllShuffled,
        PlaybackPlayAllInPanelShuffled,
        GeneralToggleShuffle,
        GeneralToggleRepeatMode,
        PlaybackToggleReplayGain,
        PlaybackReplayGainAlbum,
        PlaybackReplayGainSmart,
        PlaybackReplayGainTrack,
        PlaybackVolumeMute,
        PlaybackVolumeDown,
        PlaybackVolumeUp,
        PlaybackTempoDecrease,
        PlaybackTempoIncrease,
        PlaybackTempoReset,
        PlaybackTempoAssignPreset,
        PlaybackTempoUsePreset,
        GeneralToggleEqualiser,
        GeneralToggleDsp,
        RatingSelectedFilesTickDown,
        RatingSelectedFilesTickUp,
        RatingSelectedFilesNone,
        RatingSelectedFilesToggleLove,
        RatingNowPlayingTickDown,
        RatingNowPlayingTickUp,
        RatingNowPlayingNone,
        RatingNowPlayingToggleLove,
        SendToCommandsStart,
        SendToAutoDj,
        SendToClipboard,
        SendToClipboardNowPlaying,
        SendToExternalService,
        SendToExternalService2,
        SendToExternalService3,
        SendToExternalService4,
        SendToExternalService5,
        SendToExternalService6,
        SendToExternalService7,
        SendToExternalService8,
        SendToExternalServiceNowPlaying,
        SendToLibrary,
        SendToInbox,
        SendToOrganisedFolder,
        SendToOrganisedFolderCopy,
        SendToActiveDevice,
        SendToActivePlaylist,
        SendPlayingToActivePlaylist,
        SendToAudioBooks,
        SendToPodcasts,
        SendToVideo,
        SendToNewPlaylist,
        SendToExternalAudioEditor,
        SendToFolderMove,
        SendToFolderCopy,
        SendToReplaceFileSelectSource,
        SendToReplaceFileSelectTarget,
        SendToPlaylistAdd,
        SendToPlaylistRemove,
        SendToCommandsEnd,
        FileNewTab,
        FileCloseTab,
        FileNewPlaylistInTab,
        ToolsTagSearchAndReplace,
        ToolsFindArtwork,
        ToolsAlbumArtworkManager,
        ToolsArtistThumbManager,
        ToolsAutoNumber,
        ToolsAutoOrganise,
        ToolsAutoTagAlbum,
        ToolsAutoTagAll,
        ToolsAutoTagMissingTags,
        ToolsAutoTagMissingPictures,
        ToolsAutoTagInfer,
        ToolsConvertFormat,
        ToolsLocateMissingFiles,
        ToolsAnalyseVolume,
        ToolsUndoLevelVolume,
        ToolsRipCd,
        ToolsSyncPlayCountLastFm,
        WebDownloadNow,
        WebOpenLink0SelectedFile,
        WebOpenLink1SelectedFile,
        WebOpenLink2SelectedFile,
        WebOpenLink3SelectedFile,
        WebOpenLink4SelectedFile,
        WebOpenLink5SelectedFile,
        WebOpenLink6SelectedFile,
        WebOpenLink7SelectedFile,
        WebOpenLink8SelectedFile,
        WebOpenLink9SelectedFile,
        WebOpenLink10SelectedFile,
        WebOpenLink11SelectedFile,
        WebOpenLink12SelectedFile,
        WebOpenLink13SelectedFile,
        WebOpenLink14SelectedFile,
        ViewApplicationMinimise,
        GeneralShowMainPanel,
        ViewLayoutArtwork,
        ViewLayoutArtists,
        ViewLayoutAlbumAndTracks,
        ViewLayoutJukebox,
        ViewLayoutTrackDetails,
        ViewEqualiser,
        ViewResetTrackBrowser,
        ViewResetThumbBrowser,
        ViewLyricsFloating,
        ViewNowPlayingNotification,
        ViewNowPlayingTrackFinder,
        ViewToggleShowHeaderBar,
        ViewToggleLockDown,
        ViewToggleLeftPanel,
        ViewToggleLeftMainPanel,
        ViewToggleRightMainPanel,
        ViewToggleRightPanel,
        ViewToggleShowUpcomingTracks,
        ViewToggleVisualiser,
        ViewVisualiserToggleFullScreen,
        ViewFilesToEdit,
        ViewToggleMiniPlayer,
        ViewToggleMicroPlayer,
        ViewJumpList,
        ViewDecreaseLyricsFont,
        ViewIncreaseLyricsFont,
        ViewShowFullSizeArtwork,
        ViewPauseArtworkRotation,
        ViewToggleNowPlayingBar,
        SetMainPanelLayout,
        GeneralSetFilter,
        GeneralToggleScrobbling,
        RatingNowPlayingToggleBan,
        RatingSelectedFilesToggleBan,
        GeneralLocateFileInPlaylist,
        GeneralFindSimilarArtist,
        GeneralLocateInComputerNowPlayingTrack,
        LibraryCreateRadioLink,
        LibraryCreatePodcastSubscription,
        WebSearchPodcasts,
        ViewApplicationActivate,
        DeviceSafelyRemove,
        ViewSkinsSelect,
        ViewSkinsToggleBorder,
        FileCreateLibrary,
        FileNewPlaylistFolder,
        FileNewPlaylist,
        FileNewAutoPlaylist,
        FileNewRadioPlaylist,
        FileNewMusicFolder,
        EditPlaylist,
        PlaylistExport,
        DeviceSynchronise,
        DeviceCopyPlaylist,
        GeneralShowTrackInfo,
        GeneralSelectVolume,
        GeneralShowMiniPlayer,
        PlaylistSave,
        PlaylistClear,
        PlaylistShuffle,
        PlaylistRestoreOrder,
        PlaylistUpdateOrder,
        ToolsRemoveDuplicates,
        ToolsRemoveDeadLinks,
        ToolsManageDuplicates,
        ToolsBurnCd,
        ToolsTagCapitalise,
        ToolsTagResetPlayCount,
        ToolsTagResetSkipCount,
        ToolsSyncLastFmLovedTracks,
        ToolsAutoTagMissingLyrics,
        DeviceCopySelectedFiles,
        EditPasteArtistPicture,
        EditPasteAlbumPicture,
        EditRemoveTags,
        PlaybackVolumeGoto,
        ViewToggleVerticalTagEditor,
        ViewCollapseNodes,
        GeneralGotoTab1,
        GeneralGotoTab2,
        GeneralGotoTab3,
        GeneralGotoTab4,
        GeneralGotoTab5,
        GeneralGotoTab6,
        GeneralGotoTab7,
        GeneralGotoTab8,
        GeneralGotoTab9,
        ViewShowFilter = 32768
    }

    public static class ApplicationCommandHelper
    {
        // This is might be incomplete
        private static readonly Dictionary<ApplicationCommand, string> CommandDisplayNames = new Dictionary<ApplicationCommand, string>()
        {
            { FileNewMusicFolder, "New Folder" },
            { ViewShowFullSizeArtwork, "Show Artwork" },
            { EditPasteAlbumPicture, "Paste Artwork" },
            { GeneralReload, "Refresh" },
            { ToolsAutoTagAlbum, "Auto-Tag by Album" },
            { EditHighlightingRules, "Set Highlighting Rules" },
            { PlaybackPlayAllInPanelShuffled, "Play Shuffled" },
            { LibraryCreatePodcastSubscription, "New Subscription" },
            { WebSearchPodcasts, "Podcast Directory" },
            { PlaybackPlayAllShuffled, "Play Library Shuffled" },
            { LibraryCreateRadioLink, "New Station" },
            { DeviceSynchronise, "Synchronise Device" },
            { ToolsRipCd, "Rip CD" },
            { None, "New Folder" },
            { PlaylistShuffle, "Shuffle List" },
            { PlaylistRestoreOrder, "Restore Manual Order" },
            { PlaylistUpdateOrder, "Update Play Order" },
            { FileScanNewMedia, "Scan Folders for New Files..." },
            { FileCreateLibrary, "Create New Library..." },
            { FileNewPlaylistFolder, "New Playlist Folder" },
            { FileNewPlaylist, "New Playlist" },
            { FileNewAutoPlaylist, "New Auto-Playlist" },
            { FileNewRadioPlaylist, "New Playlist Mixer" },
            { FileOpenUrl, "Open Stream..." },
            { GeneralExitApplication, "Exit" },
            { EditSelectAll, "Select All" },
            { EditCut, "Cut" },
            { EditCopy, "Copy Tags" },
            { EditPaste, "Paste" },
            { EditPreferences, "Edit Preferences" },
            { ViewToggleMiniPlayer, "Mini Player" },
            { ViewToggleMicroPlayer, "Compact Player" },
            { ViewToggleVisualiser, "Start Visualiser" },
            { ViewJumpList, "Now Playing Assistant" },
            { PlaybackPreviousTrack, "Previous Track" },
            { PlaybackPlayPause, "Play/Pause" },
            { PlaybackStop, "Stop" },
            { PlaybackNextTrack, "Next Track" },
            { PlaybackStopAfterCurrent, "Stop after Current" },
            { GeneralToggleEqualiser, "Equaliser" },
            { GeneralToggleDsp, "DSP Effects" },
            { GeneralToggleRepeatMode, "Repeat" },
            { PlaybackReplayGainSmart, "Smart Gain" },
            { PlaybackReplayGainTrack, "Track Gain Only" },
            { PlaybackReplayGainAlbum, "Album Gain Only" },
            { ToolsAutoNumber, "Renumber Tracks" },
            { ToolsAutoTagAll, "Identify Track and Update Tags" },
            { ToolsAutoTagMissingPictures, "Update Missing Artwork" },
            { ToolsAutoTagMissingLyrics, "Update Missing Lyrics" },
            { ToolsAutoTagInfer, "Infer and Update Tags from Filename" },
            { EditRemoveTags, "Remove Tags" },
            { ToolsTagCapitalise, "Capitalise" },
            { ToolsTagSearchAndReplace, "Search and Replace" },
            { ToolsTagResetPlayCount, "Reset Play Count" },
            { ToolsTagResetSkipCount, "Reset Skip Count" },
            { ToolsSyncPlayCountLastFm, "Sync Play Count from Last.fm" },
            { ToolsSyncLastFmLovedTracks, "Sync Last.fm Loved Tracks" },
            { ToolsFindArtwork, "Downloader..." },
            { ToolsAutoOrganise, "Organise Files" },
            { ToolsLocateMissingFiles, "Locate Missing Files" },
            { ToolsConvertFormat, "Convert Format" },
            { ToolsManageDuplicates, "Manage Duplicates" },
            { ToolsBurnCd, "Burn Disc" },
            { ToolsAnalyseVolume, "Analyse Volume" },
            { ToolsUndoLevelVolume, "Restore Original Volume" },
        };

        private static List<(ApplicationCommand, string)> BuiltinCommandsResultsCache = null;

        public static IReadOnlyList<(ApplicationCommand command, string displayName)> GetValidBuiltinCommands(MusicBeeApiInterface mbApi)
        {
            if (BuiltinCommandsResultsCache != null)
                return BuiltinCommandsResultsCache;

            BuiltinCommandsResultsCache = new List<(ApplicationCommand, string)>();
            var commands = (ApplicationCommand[])Enum.GetValues(typeof(ApplicationCommand));

            var configPath = Path.Combine(mbApi.Setting_GetPersistentStoragePath(), "MusicBee3Settings.ini");
            string configFileContent = null;

            if (File.Exists(configPath))
            {
                try
                {
                    configFileContent = File.ReadAllText(configPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to read MusicBee configuration file: {ex}");
                }
            }

            foreach (var command in commands)
            {
                if (GetDisplayNameIfValid(command, out string name, configFileContent))
                {
                    BuiltinCommandsResultsCache.Add((command, name));
                }
            }

            return BuiltinCommandsResultsCache;
        }

        private static bool GetDisplayNameIfValid(ApplicationCommand command, out string name, string configFileContent)
        {
            name = null;
            if (command == None) return false;

            if (!string.IsNullOrEmpty(configFileContent))
            {
                if (SendToExternalService <= command && command <= SendToExternalService8)
                {
                    int serviceIndex = (int)command - (int)SendToExternalService + 1;
                    string tagName = serviceIndex == 1 ? "SystemExternalToolName" : $"SystemExternalTool{serviceIndex}Name";
                    var regex = new System.Text.RegularExpressions.Regex($"<{tagName}>(.*?)</{tagName}>");
                    var match = regex.Match(configFileContent);
                    if (match.Success && match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                    {
                        name = $"Send To: {match.Groups[1].Value.Trim()}";
                        return true;
                    }
                    return false;
                }

                if (WebOpenLink0SelectedFile <= command && command <= WebOpenLink14SelectedFile)
                {
                    int linkIndex = (int)command - (int)WebOpenLink0SelectedFile;
                    string tagName = $"FormNowPlayingCustom{linkIndex}LinkName";
                    var regex = new System.Text.RegularExpressions.Regex($"<{tagName}>(.*?)</{tagName}>");
                    var match = regex.Match(configFileContent);
                    if (match.Success && match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                    {
                        name = $"Web Link: {match.Groups[1].Value.Trim()}";
                        return true;
                    }
                    return false;
                }
            }

            if (!CommandDisplayNames.TryGetValue(command, out name))
            {
                name = FormatCommandName(command.ToString());
            }

            if (string.IsNullOrEmpty(name)) return false;

            return true;
        }

        private static string FormatCommandName(string enumName)
        {
            if (string.IsNullOrEmpty(enumName)) return string.Empty;

            var sb = new StringBuilder();
            sb.Append(enumName[0]);

            for (int i = 1; i < enumName.Length; i++)
            {
                if (char.IsUpper(enumName[i]) && !char.IsUpper(enumName[i - 1]))
                {
                    sb.Append(' ');
                }
                else if (char.IsUpper(enumName[i]) && i + 1 < enumName.Length && char.IsLower(enumName[i + 1]) && char.IsUpper(enumName[i - 1]))
                {
                    sb.Append(' ');
                }
                sb.Append(enumName[i]);
            }
            return sb.Replace("Goto", "Go to").ToString();
        }
    }
}
