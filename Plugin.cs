using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using MusicBeePlugin.UI;
using MusicBeePlugin.Utils;
using MusicBeePlugin.Config;
using MusicBeePlugin.Services;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        public const bool LAZY_LOAD = false;

        public static MusicBeeApiInterface mbApi;

        private PluginInfo about = new PluginInfo();
        
        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApi = new MusicBeeApiInterface();
            mbApi.Initialise(apiInterfacePtr);
            about.PluginInfoVersion = PluginInfoVersion;
            about.Name = "Modern Search Bar";
            about.Description = "Adds a customizable modern search bar";
            about.Author = "fiso64";
            about.TargetApplication = "";
            about.Type = PluginType.General;
            about.VersionMajor = 1;
            about.VersionMinor = 0;
            about.Revision = 1;
            about.MinInterfaceVersion = MinInterfaceVersion;
            about.MinApiRevision = MinApiRevision;
            about.ReceiveNotifications = ReceiveNotificationFlags.StartupOnly;
            about.ConfigurationPanelHeight = 0; 
            return about;
        }

        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            switch (type)
            {
                case NotificationType.PluginStartup:
                    Startup();
                    break;
            }
        }

        public void Startup()
        {
            if (!LAZY_LOAD)
                MusicBeeHelpers.LoadMethods();

            mbApi.MB_RegisterCommand("Modern Search Bar: Show Search Bar", (a, b) => ShowSearchBar());
            mbApi.MB_RegisterCommand("Modern Search Bar: Show Search Bar (2)", (a, b) => ShowSearchBar());
            mbApi.MB_RegisterCommand("Modern Search Bar: Artist Search", (a, b) => ShowSearchBar("a: "));
            mbApi.MB_RegisterCommand("Modern Search Bar: Album Search", (a, b) => ShowSearchBar("l: "));
            mbApi.MB_RegisterCommand("Modern Search Bar: Song Search", (a, b) => ShowSearchBar("s: "));
        }

        public static void ShowSearchBar(string defaultText = null)
        {
            var actionConfig = new SearchActionsConfig()
            {
                ArtistAction = new ActionConfig()
                {
                    Default = new SearchInTabActionData()
                    {
                        TabChoice = ApplicationCommand.GeneralGotoTab4,
                        FocusMainPanelAfterAction = true,
                        UseSortArtist = true,
                    },
                    Ctrl = new PlayActionData()
                    {
                        FocusMainPanelAfterAction = true,
                    },
                    Shift = new QueueNextActionData()
                    {
                        FocusMainPanelAfterAction = true,
                    },
                    CtrlShift = new QueueLastActionData()
                    {
                        FocusMainPanelAfterAction = true,
                    }
                },

                AlbumAction = new ActionConfig()
                {
                    Default = new OpenFilterInTabActionData()
                    {
                        TabChoice = ApplicationCommand.GeneralGotoTab3
                    },
                    Ctrl = new PlayActionData()
                    {
                        FocusMainPanelAfterAction = true,
                    },
                    Shift = new QueueNextActionData()
                    {
                        FocusMainPanelAfterAction = true,
                    },
                    CtrlShift = new QueueLastActionData()
                    {
                        FocusMainPanelAfterAction = true,
                    }
                },

                SongAction = new ActionConfig()
                {
                    Default = new PlayActionData()
                    {
                        FocusMainPanelAfterAction = true,
                    },
                    Ctrl = new PlayActionData()
                    {
                        FocusMainPanelAfterAction = true,
                    },
                    Shift = new QueueNextActionData()
                    {
                        FocusMainPanelAfterAction = true,
                    },
                    CtrlShift = new QueueLastActionData()
                    {
                        FocusMainPanelAfterAction = true,
                    }
                }
            };

            var mbHwnd = mbApi.MB_GetWindowHandle();
            var mbControl = Control.FromHandle(mbHwnd);
            SynchronizationContext mainContext = SynchronizationContext.Current;

            var uiConfig = SearchUIConfig.GetDefault(mbApi);

            var actionService = new ActionService(actionConfig);

            Thread searchBarThread = new Thread(() =>
            {
                var searchBarForm = new SearchBar(mbControl, mainContext, mbApi, actionService.RunAction, uiConfig, defaultText);
                Application.Run(searchBarForm);
            });

            searchBarThread.SetApartmentState(ApartmentState.STA);
            searchBarThread.Start();
        }

        public bool Configure(IntPtr panelHandle)
        {
            return true;
        }
    }
}
