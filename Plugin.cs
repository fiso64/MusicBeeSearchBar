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
using System.Text;
using System.Threading.Tasks;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        public const bool LAZY_LOAD = false;

        public static MusicBeeApiInterface mbApi;
        private static Config.Config config;
        public static Config.Config GetConfig() => config;
        
        private PluginInfo about = new PluginInfo();

        private static readonly ManualResetEvent startupComplete = new ManualResetEvent(false);
        private static SearchBar searchBarInstance;
        // Docked views are created and disposed on MusicBee's UI thread.
        private static readonly List<SearchBar> dockedSearchViews = new List<SearchBar>();
        private static SearchBar.ExpandedSearchState expandedSearchState;
        private static long expandedSearchStateVersion;
        private static readonly object searchBarLock = new object();
        private static bool searchBarReserved;
        private static bool searchBarReady;
        private static bool searchBarRefreshScheduled;
        private static bool searchBarRefreshPending;
        private static bool searchBarTextPending;
        private static string searchBarPendingText;
        private IpcService ipcService;

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApi = new MusicBeeApiInterface();
            mbApi.Initialise(apiInterfacePtr);
            about.PluginInfoVersion = PluginInfoVersion;
            about.Name = "Modern Search Bar";
            about.Description = "Adds a customizable modern search bar";
            about.Author = "fiso64";
            about.TargetApplication = "Search";
            about.Type = PluginType.General;
            about.VersionMajor = 1;
            about.VersionMinor = 15;
            about.Revision = 0;
            about.MinInterfaceVersion = MinInterfaceVersion;
            about.MinApiRevision = MinApiRevision;
            about.ReceiveNotifications = ReceiveNotificationFlags.StartupOnly;
            about.ConfigurationPanelHeight = 0;
            return about;
        }

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
                    if (File.Exists(targetPath))
                    {
                        return Assembly.LoadFile(targetPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to load assembly {args.Name}", ex);
                }

                return null;
            };
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

        // MusicBee invokes this callback when the main panel's selection changes.
        public void OnSelectedFilesChanged(string[] urls)
        {
            var musicBeeControl = Control.FromHandle(mbApi.MB_GetWindowHandle());
            if (musicBeeControl == null || musicBeeControl.IsDisposed || !musicBeeControl.IsHandleCreated) return;
            Action refresh = () =>
            {
                foreach (var view in dockedSearchViews.ToArray())
                    if (!view.IsDisposed && !view.Disposing) view.ScheduleSelectedResultsRefresh();
            };
            try
            {
                if (musicBeeControl.InvokeRequired) musicBeeControl.BeginInvoke(refresh);
                else refresh();
            }
            catch (InvalidOperationException) { /* MusicBee is shutting down. */ }
        }

        private void Startup()
        {
            // 1. Catch exceptions on background threads
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    Logger.Error("AppDomain Unhandled Exception", ex);
                }
            };

            // 2. Catch exceptions on the UI thread
            Application.ThreadException += (sender, args) =>
            {
                Logger.Error("Application Thread Exception", args.Exception);
            };

            // 3. Catch ignored Task exceptions
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Logger.Error("TaskScheduler Unobserved Exception", args.Exception);
                args.SetObserved(); // Prevent process crash
            };

            if (!LAZY_LOAD)
                MusicBeeHelpers.LoadInvokeCommandMethod();

            mbApi.MB_RegisterCommand("Modern Search Bar: Search", (a, b) => ShowSearchBar());
            mbApi.MB_RegisterCommand("Modern Search Bar: Search (2)", (a, b) => ShowSearchBar());
            mbApi.MB_RegisterCommand("Modern Search Bar: Search (Detached)", (a, b) => ShowSearchBar(null, true));
            mbApi.MB_RegisterCommand("Modern Search Bar: Focus Docked Search", (a, b) => FocusDockedSearch());

            mbApi.MB_RegisterCommand("Modern Search Bar: Search Artists", (a, b) => ShowSearchBar("a: "));
            mbApi.MB_RegisterCommand("Modern Search Bar: Search Albums", (a, b) => ShowSearchBar("l: "));
            mbApi.MB_RegisterCommand("Modern Search Bar: Search Songs", (a, b) => ShowSearchBar("s: "));
            mbApi.MB_RegisterCommand("Modern Search Bar: Search Playlists", (a, b) => ShowSearchBar("p: "));
            mbApi.MB_RegisterCommand("Modern Search Bar: Search Commands", (a, b) => ShowSearchBar("> "));

            mbApi.MB_RegisterCommand("Modern Search Bar: Selected (Main MB Window): Artist Action", (a, b) => PerformActionOnSelected(ResultType.Artist));
            mbApi.MB_RegisterCommand("Modern Search Bar: Selected (Main MB Window): Album Action", (a, b) => PerformActionOnSelected(ResultType.Album));
            mbApi.MB_RegisterCommand("Modern Search Bar: Selected (Main MB Window): Song Action", (a, b) => PerformActionOnSelected(ResultType.Song));

            LoadConfig();

            if (!config.FirstStartupComplete)
            {
                var result = MessageBox.Show(
                    "It's recommended to adjust the search bar actions before use. Open settings now?",
                    "Modern Search Bar: First Time Setup",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                config.FirstStartupComplete = true;
                SaveConfig();

                if (result == DialogResult.Yes)
                {
                    ShowConfigDialog();
                }

                var result2 = MessageBox.Show(
                    "To use the search bar, you must configure a keyboard shortcut.\n" +
                    "Look for 'Modern Search Bar: Search' in the Hotkeys tab.\n\n" +
                    "Open MusicBee Settings now?",
                    "Modern Search Bar: First Time Setup",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result2 == DialogResult.Yes)
                {
                    var mbHwnd = mbApi.MB_GetWindowHandle();
                    var mbControl = Control.FromHandle(mbHwnd);
                    mbControl?.BeginInvoke((Action)(() => MusicBeeHelpers.InvokeCommand(ApplicationCommand.EditPreferences)));
                }
            }
            
            startupComplete.Set();

            var ipcHwnd = mbApi.MB_GetWindowHandle();
            var ipcControl = Control.FromHandle(ipcHwnd);
            ipcService = new IpcService(ipcControl);

            //mbApi.MB_RegisterCommand("Modern Search Bar: Selected: TEST TEST TEST", (a, b) => { ReflectionService.Instance.OpenMusicExplorerTab("Casiopea"); });
        }

        public void Close(PluginCloseReason reason)
        {
            ipcService?.Dispose();
        }

        public static void LoadConfig()
        {
            var storagePath = Path.Combine(mbApi.Setting_GetPersistentStoragePath(), "ModernSearchBar");
            var configPath = Path.Combine(storagePath, "config.json");
            var logPath = Path.Combine(storagePath, "error.log");
            
            Logger.Initialize(logPath);

            config = Config.Config.LoadFromPath(configPath, mbApi);
        }

        public static void SaveConfig()
        {
            var configPath = Path.Combine(mbApi.Setting_GetPersistentStoragePath(), "ModernSearchBar", "config.json");
            Config.Config.SaveToPath(configPath, config);
        }

        public void ShowSearchBar(string defaultText = null, bool startDetached = false)
        {
            try
            {
                ShowSearchBarInternal(defaultText, startDetached);
            }
            catch (Exception ex)
            {
                Logger.Error("Error starting SearchBar", ex);
            }
        }

        private void ShowSearchBarInternal(string defaultText = null, bool startDetached = false)
        {
            startupComplete.WaitOne();

            lock (searchBarLock)
            {
                if (searchBarReserved)
                {
                    searchBarRefreshPending = true;
                    if (defaultText != null)
                    {
                        searchBarPendingText = defaultText;
                        searchBarTextPending = true;
                    }
                    if (searchBarReady) ScheduleSearchBarRefresh();
                    return;
                }

                // Reserve the slot before creating the UI thread. Calls made while the
                // form is being constructed are merged into a pending refresh.
                searchBarReserved = true;
            }

            try
            {
                var mbHwnd = mbApi.MB_GetWindowHandle();
                var mbControl = Control.FromHandle(mbHwnd);
                SynchronizationContext mainContext = SynchronizationContext.Current;
                var actionService = new ActionService(config.SearchActions);

                Thread searchBarThread = new Thread(() =>
                {
                    SearchBar searchBarForm = null;
                    try
                    {
                        searchBarForm = new SearchBar(mbControl, mainContext, mbApi, actionService, config.SearchUI, defaultText, startDetached);
                        lock (searchBarLock)
                        {
                            searchBarInstance = searchBarForm;
                        }
                        searchBarForm.Shown += (s, args) =>
                        {
                            lock (searchBarLock)
                            {
                                if (searchBarInstance != searchBarForm) return;
                                searchBarReady = true;
                                if (searchBarRefreshPending) ScheduleSearchBarRefresh();
                            }
                        };
                        Application.Run(searchBarForm);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("CRITICAL: Error in SearchBar UI Thread", ex);
                    }
                    finally
                    {
                        try
                        {
                            if (searchBarForm != null && !searchBarForm.IsDisposed)
                                searchBarForm.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Error disposing SearchBar", ex);
                        }
                        finally
                        {
                            lock (searchBarLock)
                            {
                                searchBarInstance = null;
                                searchBarReserved = false;
                                searchBarReady = false;
                                searchBarRefreshScheduled = false;
                                searchBarRefreshPending = false;
                                searchBarTextPending = false;
                                searchBarPendingText = null;
                            }
                        }
                    }
                });

                searchBarThread.SetApartmentState(ApartmentState.STA);
                searchBarThread.Start();
            }
            catch
            {
                lock (searchBarLock)
                {
                    searchBarReserved = false;
                    searchBarRefreshPending = false;
                    searchBarTextPending = false;
                    searchBarPendingText = null;
                }
                throw;
            }
        }

        // Called with searchBarLock held; BeginInvoke never waits for the UI thread.
        private static void ScheduleSearchBarRefresh()
        {
            var form = searchBarInstance;
            if (searchBarRefreshScheduled || !searchBarReady || form == null) return;
            searchBarRefreshScheduled = true;
            try
            {
                form.BeginInvoke((Action)(() =>
                {
                    string text;
                    bool updateText;
                    lock (searchBarLock)
                    {
                        if (searchBarInstance != form) return;
                        searchBarRefreshScheduled = false;
                        searchBarRefreshPending = false;
                        updateText = searchBarTextPending;
                        text = searchBarPendingText;
                        searchBarTextPending = false;
                        searchBarPendingText = null;
                    }

                    if (form.IsDisposed || form.Disposing || !form.Visible) return;
                    form.Activate();
                    if (updateText) form.SetSearchText(text);
                }));
            }
            catch (InvalidOperationException)
            {
                // The form is closing or its handle has already been destroyed.
                searchBarRefreshScheduled = false;
            }
        }

        public async void PerformActionOnSelected(ResultType actionType)
        {
            try
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
                await actionService.RunAction(result.DisplayTitle, result, new KeyEventArgs(modifiers));
            }
            catch (Exception ex)
            {
                Logger.Error("Error performing action on selected item", ex);
            }
        }

        public bool Configure(IntPtr panelHandle)
        {
            LoadConfig();
            ShowConfigDialog();
            return true;
        }

        public int OnDockablePanelCreated(Control panel)
        {
            // Zero tells MusicBee that the panel is resizable without filling its dock.
            int panelHeight = 0;
            var musicBeeControl = Control.FromHandle(mbApi.MB_GetWindowHandle());
            if (musicBeeControl == null || musicBeeControl.IsDisposed) return panelHeight;

            Action create = () =>
            {
                if (panel.IsDisposed) return;
                if (config == null) LoadConfig();
                var uiConfig = config.SearchUI;
                var context = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
                var view = new SearchBar(musicBeeControl, context, mbApi,
                    new ActionService(config.SearchActions), uiConfig, embedded: true);
                try
                {
                    Action updateLayout = () =>
                    {
                        if (panel.IsDisposed || view.IsDisposed) return;
                        view.Bounds = panel.ClientRectangle;
                        view.PerformLayout();
                        if (panel.Visible) view.Invalidate(true);
                    };
                    bool initialSizeApplied = false;
                    Action finishInitialLayout = () =>
                    {
                        if (panel.IsDisposed || view.IsDisposed || !panel.Visible) return;
                        if (!initialSizeApplied && uiConfig.DockedPanelLayout == DockedSearchLayout.Expanded)
                        {
                            int stripHeight = (int)Math.Ceiling((34 + 16 + (uiConfig.ShowSearchToolbar ? 42 : 0)) * view.DeviceDpi / 96f);
                            if (panel.Height <= stripHeight)
                            {
                                int desiredHeight = (int)Math.Ceiling(260 * view.DeviceDpi / 96f);
                                if (panel.Parent != null)
                                {
                                    int available = panel.Parent.ClientSize.Height - panel.Top;
                                    if (available > 0) desiredHeight = Math.Min(desiredHeight, available);
                                }
                                if (desiredHeight > panel.Height) panel.Height = desiredHeight;
                            }
                            initialSizeApplied = true;
                        }
                        updateLayout();
                        view.FinishExpandedStateLayout();
                    };
                    panel.Controls.Add(view);
                    RestoreExpandedSearchState(view);
                    view.EnableExpandedStateSync();
                    view.Show();
                    dockedSearchViews.Add(view);
                    view.Disposed += (sender, args) => dockedSearchViews.Remove(view);
                    panel.SizeChanged += (sender, args) => updateLayout();
                    panel.VisibleChanged += (sender, args) =>
                    {
                        if (!panel.Visible || !panel.IsHandleCreated || panel.Disposing || view.IsDisposed) return;
                        RestoreExpandedSearchState(view);
                        try { panel.BeginInvoke(finishInitialLayout); }
                        catch (InvalidOperationException) { /* The panel handle was destroyed. */ }
                    };
                    updateLayout();
                    if (panel.IsHandleCreated) panel.BeginInvoke(finishInitialLayout);
                }
                catch
                {
                    view.Dispose();
                    throw;
                }
                if (uiConfig.DockedPanelLayout == DockedSearchLayout.Compact)
                    panelHeight = (int)Math.Ceiling((34 + 16) * view.DeviceDpi / 96f);
            };
            try
            {
                if (musicBeeControl.InvokeRequired) musicBeeControl.Invoke(create);
                else create();
            }
            catch (Exception ex)
            {
                Logger.Error("Error creating docked search panel", ex);
            }
            return panelHeight;
        }

        private static void FocusDockedSearch()
        {
            var musicBeeControl = Control.FromHandle(mbApi.MB_GetWindowHandle());
            if (musicBeeControl == null || musicBeeControl.IsDisposed || !musicBeeControl.IsHandleCreated) return;
            try
            {
                musicBeeControl.BeginInvoke((Action)(() =>
                {
                    var views = dockedSearchViews.ToArray();
                    for (int i = views.Length - 1; i >= 0; i--)
                    {
                        var view = views[i];
                        if (!view.IsDisposed && view.Visible && view.FocusEmbeddedSearch()) break;
                    }
                }));
            }
            catch (InvalidOperationException) { /* MusicBee is shutting down. */ }
        }

        internal static void SaveExpandedSearchState(SearchBar source)
        {
            if (!source.IsExpandedEmbedded || !source.Visible || !source.ExpandedStateSyncEnabled) return;
            var state = source.CaptureExpandedSearchState();
            if (state == null) return;
            expandedSearchState = state;
            source.ExpandedStateVersion = ++expandedSearchStateVersion;
        }

        private static void RestoreExpandedSearchState(SearchBar target)
        {
            if (expandedSearchState == null || !target.IsExpandedEmbedded
                || target.ExpandedStateVersion == expandedSearchStateVersion) return;
            target.ExpandedStateVersion = expandedSearchStateVersion;
            target.RestoreExpandedSearchState(expandedSearchState);
        }

        public static void ShowConfigDialog(int initialTabIndex = 0)
        {
            var cfgForm = new ConfigurationForm(config, mbApi, initialTabIndex);
            if (cfgForm.ShowDialog() == DialogResult.OK)
            {
                config = cfgForm.Config;
                SaveConfig();
            }
        }
    }
}
