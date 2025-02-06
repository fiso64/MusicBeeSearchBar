using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using static MusicBeePlugin.Plugin;


namespace MusicBeePlugin.Config
{
    public abstract class BaseActionData
    {
        public bool FocusMainPanelAfterAction = false;
        internal bool _actionExecuted = false;
    }

    public class PlayActionData : BaseActionData
    {
        public bool ShufflePlay = false;
    }

    public class QueueNextActionData : BaseActionData
    {
        public bool ShufflePlay = false;
        public bool ClearQueueBeforeAdd = false;
    }

    public class QueueLastActionData : BaseActionData
    {
        public bool ShufflePlay = false;
    }

    public class SearchInTabActionData : BaseActionData
    {
        public ApplicationCommand TabChoice = 0;
        public bool UseSortArtist = false;
        public bool SearchAddPrefix = false;
        public bool ClearSearchBarTextAfterSearch = false;
        public bool UseSearchBarText = false;
        public bool ToggleSearchEntireLibraryBeforeSearch = false;
    }

    public class OpenFilterInTabActionData : BaseActionData
    {
        public ApplicationCommand TabChoice = 0;
        public bool GoBackBeforeOpenFilter = true;
        public bool UseSortArtist = false;
    }

    public class ActionConfig
    {
        public BaseActionData Default;
        public BaseActionData Shift;
        public BaseActionData Ctrl;
        public BaseActionData CtrlShift;
    }

    public class SearchActionsConfig
    {
        public ActionConfig ArtistAction;
        public ActionConfig AlbumAction;
        public ActionConfig SongAction;

        public static SearchActionsConfig GetDefault()
        {
            var config = new SearchActionsConfig();

            // TODO: Implement default

            return config;
        }
    }

    public class SearchUIConfig
    {
        public bool GroupResultsByType = true;
        public double OverlayOpacity = 0.4;
        public int MaxResultsVisible = 6;
        public Color TextColor = Color.White;
        public Color BaseColor = Color.FromArgb(30, 30, 30);
        public Color ResultHighlightColor = Color.FromArgb(60, 60, 60);
        public Size InitialSize = new Size(500, 40);

        public static SearchUIConfig GetDefault(MusicBeeApiInterface mbApi)
        {
            var config = new SearchUIConfig();
            
            // TODO: Use musicbee skin color

            return config;
        }
    }
}
