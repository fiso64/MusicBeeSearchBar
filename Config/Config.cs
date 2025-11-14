using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using static MusicBeePlugin.Plugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace MusicBeePlugin.Config
{
    public enum TabChoice
    {
        CurrentTab,
        NewTab,
        Tab1,
        Tab2,
        Tab3,
        Tab4,
        Tab5,
        Tab6,
        Tab7,
        Tab8,
        Tab9,
    }

    public abstract class BaseActionData
    {
        public bool FocusMainPanelAfterAction = false;

        [JsonIgnore]
        public bool _actionExecuted = false;
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
        [JsonConverter(typeof(StringEnumConverter))]
        public TabChoice TabChoice = 0; // makes it stay in current tab
        public bool UseSortArtist = false;
        public bool SearchAddPrefix = false;
        public bool ClearSearchBarTextAfterSearch = false;
        public bool UseSearchBarText = false;
        public bool ToggleSearchEntireLibraryBeforeSearch = false;
        public bool UseLeftSidebar = false;
    }

    public class OpenFilterInTabActionData : BaseActionData
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public TabChoice TabChoice = 0;
        public bool GoBackBeforeOpenFilter = true;
        public bool UseSortArtist = false;
    }

    public class ActionConfig
    {
        [JsonConverter(typeof(ActionDataJsonConverter))]
        public BaseActionData Default;
        [JsonConverter(typeof(ActionDataJsonConverter))]
        public BaseActionData Shift;
        [JsonConverter(typeof(ActionDataJsonConverter))]
        public BaseActionData Ctrl;
        [JsonConverter(typeof(ActionDataJsonConverter))]
        public BaseActionData CtrlShift;
    }

    public class SearchActionsConfig
    {
        public ActionConfig ArtistAction;
        public ActionConfig AlbumAction;
        public ActionConfig SongAction;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public ActionConfig PlaylistAction;

        public static SearchActionsConfig GetDefault()
        {
            var config = new SearchActionsConfig();
            
            config.ArtistAction = new ActionConfig
            {
                Default = new PlayActionData { ShufflePlay = true },
                Ctrl = new QueueLastActionData { ShufflePlay = true },
                Shift = new QueueNextActionData { ShufflePlay = true },
                CtrlShift = new QueueNextActionData { ShufflePlay = true }
            };

            config.AlbumAction = new ActionConfig
            {
                Default = new PlayActionData(),
                Ctrl = new QueueLastActionData(),
                Shift = new QueueNextActionData(),
                CtrlShift = new QueueNextActionData()
            };

            config.SongAction = new ActionConfig
            {
                Default = new PlayActionData(),
                Ctrl = new QueueLastActionData(),
                Shift = new QueueNextActionData(),
                CtrlShift = new QueueNextActionData()
            };

            config.PlaylistAction = new ActionConfig
            {
                Default = new PlayActionData(),
                Ctrl = new QueueLastActionData(),
                Shift = new QueueNextActionData { ShufflePlay = true },
                CtrlShift = new QueueNextActionData()
            };

            return config;
        }
    }

    public class SearchUIConfig // should be split into two configs, one for the search UI and one for the search behavior
    {
        public bool GroupResultsByType { get; set; } = true;
        public bool ShowTypeHeaders { get; set; } = true;
        public bool EnableContainsCheck { get; set; } = true;
        public bool ShowImages { get; set; } = true;
        public double OverlayOpacity { get; set; } = 0.6;
        public int MaxResultsVisible { get; set; } = 8;
        public int ArtistResultLimit { get; set; } = 5;
        public int AlbumResultLimit { get; set; } = 5;
        public int SongResultLimit { get; set; } = 10;
        public int ResultItemHeight { get; set; } = 56;
        public int PlaylistResultLimit { get; set; } = 100;
        public enum DefaultResultsChoice { Playing, Selected, None };
        public DefaultResultsChoice DefaultResults = DefaultResultsChoice.Playing;
        public Color TextColor { get; set; } = Color.White;
        public Color BaseColor { get; set; } = Color.FromArgb(20, 20, 20);
        public Color ResultHighlightColor { get; set; } = Color.FromArgb(45, 45, 45);
        public Size InitialSize { get; set; } = new Size(550, 40);

        public static SearchUIConfig GetDefault(MusicBeeApiInterface mbApi)
        {
            var config = new SearchUIConfig();
            
            // TODO: Use musicbee skin color

            return config;
        }
    }

    public class Config
    {
        public SearchActionsConfig SearchActions;
        public SearchUIConfig SearchUI;
        public bool FirstStartupComplete = false;

        public static Config GetDefault(MusicBeeApiInterface mbApi)
        {
            var config = new Config();
            config.SearchActions = SearchActionsConfig.GetDefault();
            config.SearchUI = SearchUIConfig.GetDefault(mbApi);
            return config;
        }

        public static Config LoadFromPath(string path, MusicBeeApiInterface mbApi)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    string jsonContent = System.IO.File.ReadAllText(path);
                    var settings = new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                    };
                    var defaultConfig = GetDefault(mbApi);
                    var loadedConfig = JsonConvert.DeserializeObject<Config>(jsonContent, settings);
                    
                    if (loadedConfig == null)
                        return defaultConfig;

                    // Merge SearchActions
                    if (loadedConfig.SearchActions == null)
                        loadedConfig.SearchActions = defaultConfig.SearchActions;
                    else
                    {
                        if (loadedConfig.SearchActions.ArtistAction == null)
                            loadedConfig.SearchActions.ArtistAction = defaultConfig.SearchActions.ArtistAction;
                        if (loadedConfig.SearchActions.AlbumAction == null)
                            loadedConfig.SearchActions.AlbumAction = defaultConfig.SearchActions.AlbumAction;
                        if (loadedConfig.SearchActions.SongAction == null)
                            loadedConfig.SearchActions.SongAction = defaultConfig.SearchActions.SongAction;
                        if (loadedConfig.SearchActions.PlaylistAction == null)
                            loadedConfig.SearchActions.PlaylistAction = defaultConfig.SearchActions.PlaylistAction;
                    }

                    // Merge SearchUI
                    if (loadedConfig.SearchUI == null)
                        loadedConfig.SearchUI = defaultConfig.SearchUI;

                    return loadedConfig;
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Error loading configuration file:\n{ex.Message}",
                    "Configuration Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
            return GetDefault(mbApi);
        }

        public static void SaveToPath(string path, Config config)
        {
            try
            {
                string directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                };
                string jsonString = JsonConvert.SerializeObject(config, settings);
                System.IO.File.WriteAllText(path, jsonString);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Error saving configuration file:\n{ex.Message}",
                    "Configuration Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
}
