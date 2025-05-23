﻿using System;
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
using System.Text;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        public const bool LAZY_LOAD = false;

        public static MusicBeeApiInterface mbApi;
        private static Config.Config config;
        
        private PluginInfo about = new PluginInfo();

        private static readonly ManualResetEvent startupComplete = new ManualResetEvent(false);

        public Plugin()
        {
            // taken from https://github.com/sll552/DiscordBee/blob/master/DiscordBee.cs
            AppDomain.CurrentDomain.AssemblyResolve += (object _, ResolveEventArgs args) =>
            {
                string assemblyFile = args.Name.Contains(",")
                    ? args.Name.Substring(0, args.Name.IndexOf(','))
                    : args.Name;

                assemblyFile += ".dll";

                string absoluteFolder = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
                string targetPath = Path.Combine(absoluteFolder, "SearchBar", assemblyFile);

                try
                {
                    return Assembly.LoadFile(targetPath);
                }
                catch (Exception ex)
                {
                    return null;
                }
            };
        }

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
            about.VersionMinor = 7;
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

        private void Startup()
        {
            if (!LAZY_LOAD)
                MusicBeeHelpers.LoadInvokeCommandMethod();

            mbApi.MB_RegisterCommand("Modern Search Bar: Search", (a, b) => ShowSearchBar());
            mbApi.MB_RegisterCommand("Modern Search Bar: Search (2)", (a, b) => ShowSearchBar());

            mbApi.MB_RegisterCommand("Modern Search Bar: Search Artists", (a, b) => ShowSearchBar("a: "));
            mbApi.MB_RegisterCommand("Modern Search Bar: Search Albums", (a, b) => ShowSearchBar("l: "));
            mbApi.MB_RegisterCommand("Modern Search Bar: Search Songs", (a, b) => ShowSearchBar("s: "));
            mbApi.MB_RegisterCommand("Modern Search Bar: Search Playlists", (a, b) => ShowSearchBar("p: "));
            mbApi.MB_RegisterCommand("Modern Search Bar: Search Commands", (a, b) => ShowSearchBar("> "));

            mbApi.MB_RegisterCommand("Modern Search Bar: Selected: Artist Action", (a, b) => PerformActionOnSelected(ResultType.Artist));
            mbApi.MB_RegisterCommand("Modern Search Bar: Selected: Album Action", (a, b) => PerformActionOnSelected(ResultType.Album));
            mbApi.MB_RegisterCommand("Modern Search Bar: Selected: Song Action", (a, b) => PerformActionOnSelected(ResultType.Song));

            LoadConfig();

            if (!config.FirstStartupComplete)
            {
                var result = MessageBox.Show(
                    "It's recommended to adjust the search bar actions before use. Open settings now?",
                    "Modern Search Bar: First Time Setup",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question
                );

                config.FirstStartupComplete = true;
                SaveConfig();

                if (result == DialogResult.OK)
                {
                    ShowConfigDialog();
                }
            }
            
            startupComplete.Set();
        }

        public static void LoadConfig()
        {
            var configPath = Path.Combine(mbApi.Setting_GetPersistentStoragePath(), "ModernSearchBar", "config.json");
            config = Config.Config.LoadFromPath(configPath, mbApi);
        }

        public static void SaveConfig()
        {
            var configPath = Path.Combine(mbApi.Setting_GetPersistentStoragePath(), "ModernSearchBar", "config.json");
            Config.Config.SaveToPath(configPath, config);
        }

        public void ShowSearchBar(string defaultText = null)
        {
            startupComplete.WaitOne();

            var mbHwnd = mbApi.MB_GetWindowHandle();
            var mbControl = Control.FromHandle(mbHwnd);
            SynchronizationContext mainContext = SynchronizationContext.Current;

            var actionService = new ActionService(config.SearchActions);

            Thread searchBarThread = new Thread(() =>
            {
                var searchBarForm = new SearchBar(mbControl, mainContext, mbApi, actionService.RunAction, config.SearchUI, defaultText);
                Application.Run(searchBarForm);
            });

            searchBarThread.SetApartmentState(ApartmentState.STA);
            searchBarThread.Start();
        }

        public void PerformActionOnSelected(ResultType actionType)
        {
            var modifiers = Keys.None;
            if (Control.ModifierKeys.HasFlag(Keys.Control)) modifiers |= Keys.Control;
            if (Control.ModifierKeys.HasFlag(Keys.Shift)) modifiers |= Keys.Shift;

            var path = MusicBeeHelpers.GetFirstSelectedTrack().path;
            var track = new Track(path);

            SearchResult result = null;

            if (actionType == ResultType.Song)
            {
                result = new SongResult(new Track(path));
            }
            else if (actionType == ResultType.Artist)
            {
                result = new ArtistResult(track.Artist.Split(';')[0].Trim(), track.SortArtist);
            }
            else if (actionType == ResultType.Album)
            {
                result = new AlbumResult(track.Album, track.AlbumArtist, track.SortAlbumArtist);
            }

            var actionService = new ActionService(config.SearchActions);
            actionService.RunAction(result.DisplayTitle, result, new KeyEventArgs(modifiers));
        }

        public bool Configure(IntPtr panelHandle)
        {
            LoadConfig();
            ShowConfigDialog();
            return true;
        }

        public static void ShowConfigDialog()
        {
            var cfgForm = new ConfigurationForm(config, mbApi);
            if (cfgForm.ShowDialog() == DialogResult.OK)
            {
                config = cfgForm.Config;
                SaveConfig();
            }
        }
    }
}
