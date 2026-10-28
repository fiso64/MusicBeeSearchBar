using System;
using System.Windows.Forms;
using MusicBeePlugin.Config;
using System.Drawing;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System.Threading;

namespace MusicBeePlugin.UI
{
    public partial class ConfigurationForm : Form
    {
        private Config.Config _config;
        private readonly Plugin.MusicBeeApiInterface _mbApi;

        public Config.Config Config => _config;
        private TabControl tabControl;
        private readonly ToolTip _actionSettingsToolTip = new ToolTip();

        public ConfigurationForm(Config.Config config, Plugin.MusicBeeApiInterface mbApi, int initialTabIndex = 0)
        {
            // Create a deep copy of the config for our use
            _config = JsonConvert.DeserializeObject<Config.Config>(
                JsonConvert.SerializeObject(config));
            _mbApi = mbApi;
            Disposed += (s, e) => _actionSettingsToolTip.Dispose();
            InitializeComponent();
            if (initialTabIndex >= 0 && initialTabIndex < this.tabControl.TabCount)
            {
                this.tabControl.SelectedIndex = initialTabIndex;
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // Form properties
            this.Text = "Modern Search Bar Configuration";
            this.ClientSize = new Size(800, 780);
            this.MinimumSize = new Size(420, 240);
            this.StartPosition = FormStartPosition.CenterParent;
            
            // Create tab control for different settings sections
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(12, 4)
            };

            // Add tabs
            TabPage actionsTab = new TabPage("Actions");
            TabPage searchTab = new TabPage("Search");
            TabPage appearanceTab = new TabPage("Appearance");
            TabPage helpTab = new TabPage("Help");
            
            // Configure Actions tab
            var actionsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            actionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            actionsLayout.Controls.Add(CreateActionPanel("Artist Actions", _config.SearchActions.ArtistAction, Services.ResultType.Artist), 0, 0);
            actionsLayout.Controls.Add(CreateActionPanel("Album Actions", _config.SearchActions.AlbumAction, Services.ResultType.Album), 0, 1);
            actionsLayout.Controls.Add(CreateActionPanel("Song Actions", _config.SearchActions.SongAction, Services.ResultType.Song), 0, 2);
            actionsLayout.Controls.Add(CreateActionPanel("Playlist Actions", _config.SearchActions.PlaylistAction, Services.ResultType.Playlist), 0, 3);
            
            actionsTab.Controls.Add(CreateSettingsScrollPanel(actionsLayout));

            // Configure Search tab using factory
            var searchLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            ConfigControlFactory.CreateControlsForObject(searchLayout, _config.SearchUI, "Search", includeSpacer: false);
            searchTab.Controls.Add(CreateSettingsScrollPanel(searchLayout));

            // Configure Appearance tab using factory
            var appearanceLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            appearanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            appearanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            ConfigControlFactory.CreateControlsForObject(appearanceLayout, _config.SearchUI, "Appearance", AddDockedColorsSection, includeSpacer: false);
            appearanceTab.Controls.Add(CreateSettingsScrollPanel(appearanceLayout));
            
            // Configure Help Tab
            var helpLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1, Padding = new Padding(10)
            };
            helpLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddHelpSection(helpLayout, "Prefixes", "Use these at the start of your search to filter results:", new[]
            {
                new[] { "r:", "Artists" }, new[] { "l:", "Albums" }, new[] { "s:", "Songs" },
                new[] { "p:", "Playlists" }, new[] { ">", "Commands" }
            });
            AddHelpSection(helpLayout, "Suffixes", "Use these at the end of your search to show more results:", new[]
            {
                new[] { "..", "Show up to 100 results" }, new[] { "....", "Show up to 1,000 results" },
                new[] { "......", "Show up to 10,000 results, etc." }
            });
            AddHelpSection(helpLayout, "Shortcuts", "These can be used while the search bar is open:", new[]
            {
                new[] { "Tab / Shift+Tab", "Cycle search filters" },
                new[] { "Shift+F10", "Open the selected result's context menu" },
                new[] { "Right", "Open the selected result's context menu (alternative, floating mode only)" },
                new[] { "Alt+R / L / S / P", "Filter artists / albums / songs / playlists" },
                new[] { "Alt+Shift+R", "Execute artist action for selected result" },
                new[] { "Alt+Shift+L", "Execute album action for selected result" },
                new[] { "Alt+A / Alt+Shift+A", "Toggle all results without limits" },
                new[] { "Ctrl+Shift+A", "Select visible results of the current type" },
                new[] { "Ctrl+A", "Select search text, or results when the result list is focused" },
                new[] { "Ctrl+D", "Toggle detached mode" },
                new[] { "Ctrl+P", "Open the settings dialog" },
                new[] { "Ctrl+H", "Open this help page" },
                new[] { "F5", "Rebuild database" }
            });
            AddHelpSection(helpLayout, "External Control (IPC)",
                "Send strings to named pipe 'MusicBeeSearchBarIPC' to trigger actions:", new[]
                {
                    new[] { "Artist|<Name>", "Run the default artist action" },
                    new[] { "Album|<Name>|<Artist>", "Run the default album action" },
                    new[] { "Song|<FilePath>", "Run the default song action" }
                });
            helpTab.Controls.Add(CreateSettingsScrollPanel(helpLayout));

            tabControl.TabPages.Add(actionsTab);
            tabControl.TabPages.Add(searchTab);
            tabControl.TabPages.Add(appearanceTab);
            tabControl.TabPages.Add(helpTab);

            var dialogLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            dialogLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dialogLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            dialogLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            tabControl.Margin = Padding.Empty;
            dialogLayout.Controls.Add(tabControl, 0, 0);

            // Add bottom buttons panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Height = 50
            };

            var saveButton = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Right,
                Width = 75,
                Height = 23,
                Location = new Point(buttonPanel.Width - 170, 14)
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Anchor = AnchorStyles.Right,
                Width = 75,
                Height = 23,
                Location = new Point(buttonPanel.Width - 85, 14)
            };
            
            this.CancelButton = cancelButton;

            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(cancelButton);
            dialogLayout.Controls.Add(buttonPanel, 0, 1);
            this.Controls.Add(dialogLayout);

            this.ResumeLayout(false);

            saveButton.Click += (sender, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            cancelButton.Click += (sender, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
        }

        private static void AddHelpSection(TableLayoutPanel layout, string title, string description, string[][] rows)
        {
            var heading = new Label { Text = title, AutoSize = true, Margin = new Padding(0, 8, 0, 4) };
            var headingFont = new Font(heading.Font, FontStyle.Bold);
            heading.Font = headingFont;
            heading.Disposed += (s, e) => headingFont.Dispose();
            var note = new Label { Text = description, AutoSize = true, Margin = new Padding(0, 0, 0, 5) };
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = rows[0].Length, RowCount = rows.Length,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single, Margin = new Padding(0, 0, 0, 8)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            if (table.ColumnCount > 1) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int row = 0; row < rows.Length; row++)
            {
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                for (int column = 0; column < rows[row].Length; column++)
                    table.Controls.Add(new Label { Text = rows[row][column], AutoSize = true, Margin = new Padding(6, 4, 6, 4) }, column, row);
            }
            foreach (Control control in new Control[] { heading, note, table })
            {
                int row = layout.RowCount++;
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(control, 0, row);
            }
        }

        private static Panel CreateSettingsScrollPanel(TableLayoutPanel content)
        {
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            panel.Controls.Add(content);
            bool scheduled = false;
            Action scheduleUpdate = () =>
            {
                if (scheduled || panel.IsDisposed || panel.Disposing || !panel.IsHandleCreated) return;
                scheduled = true;
                try
                {
                    panel.BeginInvoke((Action)(() =>
                    {
                        scheduled = false;
                        if (panel.IsDisposed || panel.Disposing || content.IsDisposed) return;
                        int width = Math.Max(1, panel.ClientSize.Width - panel.Padding.Horizontal);
                        int height = Math.Max(content.Height, content.GetPreferredSize(new Size(width, 0)).Height);
                        var extent = new Size(0, height);
                        if (panel.AutoScrollMinSize != extent) panel.AutoScrollMinSize = extent;
                    }));
                }
                catch (InvalidOperationException) { scheduled = false; }
            };
            panel.HandleCreated += (s, e) => scheduleUpdate();
            panel.ClientSizeChanged += (s, e) => scheduleUpdate();
            content.SizeChanged += (s, e) => scheduleUpdate();
            content.Layout += (s, e) => scheduleUpdate();
            return panel;
        }

        private void AddDockedColorsSection(TableLayoutPanel layout)
        {
            var section = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 8, 0, 0)
            };
            var toggle = new LinkLabel { Text = "Show docked colors", AutoSize = true };
            var colors = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Margin = new Padding(16, 4, 0, 0),
                Visible = false
            };
            colors.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            colors.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            ConfigControlFactory.CreateControlsForObject(colors, _config.SearchUI, "DockedColors", includeSpacer: false);
            var useSeparateColors = colors.GetControlFromPosition(0, 0) as CheckBox;
            Action updateEnabled = () =>
            {
                foreach (Control control in colors.Controls)
                    if (control != useSeparateColors) control.Enabled = useSeparateColors.Checked;
            };
            if (useSeparateColors != null)
            {
                useSeparateColors.CheckedChanged += (s, e) => updateEnabled();
                updateEnabled();
            }
            toggle.LinkClicked += (s, e) =>
            {
                colors.Visible = !colors.Visible;
                toggle.Text = colors.Visible ? "Hide docked colors" : "Show docked colors";
            };
            section.Controls.Add(toggle);
            section.Controls.Add(colors);
            int row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(section, 0, row);
            layout.SetColumnSpan(section, 2);
        }

        private static string FormatShortcut(Keys key)
        {
            return new KeysConverter().ConvertToString(key) ?? key.ToString();
        }

        private void CaptureShortcut(ActionConfig config, ActionShortcut existing, Action refresh)
        {
            Enabled = false;
            var owner = this;
            var thread = new Thread(() =>
            {
                Keys selected = Keys.None;
                try
                {
                    using (var dialog = new ShortcutCaptureDialog())
                        if (dialog.ShowDialog() == DialogResult.OK) selected = dialog.CapturedKey;
                }
                catch (Exception ex)
                {
                    MusicBeePlugin.Utils.Logger.Error("Could not open shortcut capture window", ex);
                }
                if (owner.IsDisposed || !owner.IsHandleCreated) return;
                try
                {
                    owner.BeginInvoke((Action)(() =>
                    {
                        if (owner.IsDisposed) return;
                        owner.Enabled = true;
                        owner.Activate();
                        if (selected == Keys.None) return;
                        if (selected == Keys.Enter || config.Shortcuts.Any(x => x != existing && x.Key == selected))
                        {
                            MessageBox.Show(owner, "This key is already assigned for this result type.", "Shortcut", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                        if (existing == null)
                            config.Shortcuts.Add(new ActionShortcut { Key = selected, Action = ActionRegistry.GetActionsForType(GetResultTypeForConfig(config))[0].Factory() });
                        else existing.Key = selected;
                        refresh();
                    }));
                }
                catch (InvalidOperationException) { }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        private Services.ResultType GetResultTypeForConfig(ActionConfig config)
        {
            if (config == _config.SearchActions.ArtistAction) return Services.ResultType.Artist;
            if (config == _config.SearchActions.AlbumAction) return Services.ResultType.Album;
            if (config == _config.SearchActions.SongAction) return Services.ResultType.Song;
            return Services.ResultType.Playlist;
        }

        private sealed class ShortcutCaptureDialog : Form
        {
            private readonly FlowLayoutPanel keyPreview;
            private readonly System.Windows.Forms.Timer acceptTimer;
            public Keys CapturedKey { get; private set; }
            public ShortcutCaptureDialog()
            {
                Text = "Bind shortcut";
                ClientSize = new Size(360, 150);
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterScreen;
                TopMost = true;
                KeyPreview = true;
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.Controls.Add(new Label { Text = "Press a key combination", AutoSize = true, Anchor = AnchorStyles.None }, 0, 0);
                keyPreview = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.None, WrapContents = false };
                layout.Controls.Add(keyPreview, 0, 1);
                layout.Controls.Add(new Label { Text = "Press Escape to cancel", AutoSize = true, Anchor = AnchorStyles.None }, 0, 2);
                Controls.Add(layout);
                acceptTimer = new System.Windows.Forms.Timer { Interval = 600 };
                acceptTimer.Tick += (s, e) => { acceptTimer.Stop(); DialogResult = DialogResult.OK; Close(); };
                Disposed += (s, e) => acceptTimer.Dispose();
                ShowKeys(Keys.None);
            }
            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                return CaptureKey(keyData) || base.ProcessCmdKey(ref msg, keyData);
            }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (CaptureKey(e.KeyData)) { e.Handled = true; e.SuppressKeyPress = true; }
                else base.OnKeyDown(e);
            }
            protected override void OnKeyUp(KeyEventArgs e)
            {
                base.OnKeyUp(e);
                if (!acceptTimer.Enabled) ShowKeys(ModifierKeys);
            }
            private void ShowKeys(Keys key)
            {
                foreach (Control control in keyPreview.Controls.Cast<Control>().ToArray()) control.Dispose();
                keyPreview.Controls.Clear();
                var names = new List<string>();
                if ((key & Keys.Control) != 0) names.Add("Ctrl");
                if ((key & Keys.Shift) != 0) names.Add("Shift");
                if ((key & Keys.Alt) != 0) names.Add("Alt");
                var code = key & Keys.KeyCode;
                if (code != Keys.None && code != Keys.ControlKey && code != Keys.ShiftKey && code != Keys.Menu)
                    names.Add(FormatShortcut(code));
                if (names.Count == 0) names.Add("Waiting for keys…");
                foreach (var name in names)
                    keyPreview.Controls.Add(new Label { Text = name, AutoSize = true, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(9, 5, 9, 5), Margin = new Padding(3) });
            }
            private bool CaptureKey(Keys key)
            {
                if (key == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return true;
                }
                if (acceptTimer.Enabled) return true;
                ShowKeys(key);
                var code = key & Keys.KeyCode;
                if (code == Keys.None || code == Keys.ControlKey || code == Keys.ShiftKey || code == Keys.Menu || code == Keys.LWin || code == Keys.RWin) return false;
                CapturedKey = key;
                acceptTimer.Start();
                return true;
            }
        }

        private GroupBox CreateActionPanel(string title, ActionConfig actionConfig, Services.ResultType resultType)
        {
            GroupBox groupBox = new GroupBox
            {
                Text = title,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10)
            };

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var defaultLayout = CreateActionRowsLayout(1);
            defaultLayout.Controls.Add(new Label { Text = "Enter / click:", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) }, 0, 0);
            var (defaultCombo, defaultOptions) = CreateActionControls(actionConfig.Default, action => actionConfig.Default = action, resultType);
            defaultLayout.Controls.Add(defaultCombo, 1, 0);
            defaultLayout.Controls.Add(defaultOptions, 2, 0);
            var alternativesLayout = CreateActionRowsLayout(0);
            alternativesLayout.ColumnCount = 4;
            alternativesLayout.ColumnStyles.Clear();
            alternativesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            alternativesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            alternativesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            alternativesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            alternativesLayout.Visible = false;
            var add = new Button { Text = "Add shortcut", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 6, 0, 6) };
            void rebuild()
            {
                alternativesLayout.SuspendLayout();
                foreach (Control child in alternativesLayout.Controls.Cast<Control>().ToArray())
                    if (child != add) child.Dispose();
                alternativesLayout.Controls.Clear();
                alternativesLayout.RowStyles.Clear();
                alternativesLayout.RowCount = 0;
                foreach (var shortcut in actionConfig.Shortcuts.ToArray())
                {
                    if (alternativesLayout.RowCount > 0)
                    {
                        int separatorRow = alternativesLayout.RowCount++;
                        alternativesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                        var separator = new Label { BorderStyle = BorderStyle.Fixed3D, Height = 2, Dock = DockStyle.Top, Margin = new Padding(3, 6, 3, 6) };
                        alternativesLayout.Controls.Add(separator, 0, separatorRow);
                        alternativesLayout.SetColumnSpan(separator, 4);
                    }
                    int row = alternativesLayout.RowCount++;
                    alternativesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    var keysButton = new Button { Text = FormatShortcut(shortcut.Key), AutoSize = true };
                    keysButton.Click += (s, e) => CaptureShortcut(actionConfig, shortcut, rebuild);
                    var remove = new Button { Text = "×", Size = new Size(25, 23), Anchor = AnchorStyles.Top | AnchorStyles.Left, Margin = new Padding(3) };
                    _actionSettingsToolTip.SetToolTip(remove, "Remove this shortcut");
                    remove.Click += (s, e) =>
                    {
                        if (MessageBox.Show(this, "Remove the " + FormatShortcut(shortcut.Key) + " shortcut?", "Remove shortcut", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                        actionConfig.Shortcuts.Remove(shortcut);
                        rebuild();
                    };
                    var (dropdown, options) = CreateActionControls(shortcut.Action, action => shortcut.Action = action, resultType);
                    alternativesLayout.Controls.Add(remove, 0, row);
                    alternativesLayout.Controls.Add(keysButton, 1, row);
                    alternativesLayout.Controls.Add(dropdown, 2, row);
                    alternativesLayout.Controls.Add(options, 3, row);
                }
                int addRow = alternativesLayout.RowCount++;
                alternativesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                alternativesLayout.Controls.Add(add, 0, addRow);
                alternativesLayout.SetColumnSpan(add, 4);
                alternativesLayout.ResumeLayout(true);
            }
            add.Click += (s, e) => CaptureShortcut(actionConfig, null, rebuild);
            rebuild();
            var alternativesLink = new LinkLabel { Text = "Show shortcuts", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
            alternativesLink.LinkClicked += (s, e) =>
            {
                alternativesLayout.Visible = !alternativesLayout.Visible;
                alternativesLink.Text = alternativesLayout.Visible ? "Hide shortcuts" : "Show shortcuts";
            };
            layout.Controls.Add(defaultLayout, 0, 0);
            layout.Controls.Add(alternativesLink, 0, 1);
            layout.Controls.Add(alternativesLayout, 0, 2);

            groupBox.Controls.Add(layout);
            return groupBox;
        }

        private static TableLayoutPanel CreateActionRowsLayout(int rowCount)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = rowCount,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < rowCount; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return layout;
        }

        private sealed class ActionPickerDialog : Form
        {
            private readonly ListBox resultList;
            private readonly ListBox commandList;
            private readonly TextBox filter;
            private readonly TableLayoutPanel sections;
            private readonly Label resultHeader;
            private readonly Font sectionHeaderFont;
            private readonly List<(string Name, string Description, string SearchText, bool IsResultAction, Func<BaseActionData> Create)> entries;
            public BaseActionData SelectedAction { get; private set; }

            public ActionPickerDialog(Services.ResultType type, Plugin.MusicBeeApiInterface api, SearchUIConfig config)
            {
                Text = "Choose action";
                ClientSize = new Size(600, 550);
                MinimumSize = new Size(380, 300);
                StartPosition = FormStartPosition.CenterParent;
                entries = ActionRegistry.GetActionsForType(type)
                    .Select(x => (x.Name, x.Description, x.Name + " " + x.Description, true, x.Factory)).ToList();
                var search = new Services.SearchService(api, config);
                foreach (var command in search.GetAvailableCommands(CancellationToken.None))
                {
                    var entry = command;
                    entries.Add((entry.DisplayTitle, null,
                        entry.DisplayTitle + " " + (entry.Command?.ToString() ?? ""),
                        false,
                        () => new CommandActionData { Command = entry.Command, PluginCommandName = entry.PluginCommandName, DisplayName = entry.DisplayTitle }));
                }

                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                TextBox musicBeeFilter = null;
                if (api.MB_AddPanel != null)
                {
                    try
                    {
                        var control = api.MB_AddPanel(null, Plugin.PluginPanelDock.TextBox);
                        musicBeeFilter = control as TextBox;
                        if (musicBeeFilter == null) control?.Dispose();
                    }
                    catch (Exception ex) { MusicBeePlugin.Utils.Logger.Error("Could not create MusicBee's action filter text box", ex); }
                }
                filter = musicBeeFilter ?? new TextBox();
                filter.Dock = DockStyle.Fill;
                filter.Margin = new Padding(0, 0, 0, 8);
                resultList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Font.Height + 8 };
                resultList.DrawItem += (s, e) =>
                {
                    e.DrawBackground();
                    if (e.Index < 0 || e.Index >= resultList.Items.Count) return;
                    var item = (PickerItem)resultList.Items[e.Index];
                    var bounds = e.Bounds;
                    var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
                    int nameWidth = Math.Min(bounds.Width, Math.Max(bounds.Width / 3, TextRenderer.MeasureText(item.Name, e.Font).Width + 8));
                    TextRenderer.DrawText(e.Graphics, item.Name, e.Font, new Rectangle(bounds.X + 3, bounds.Y, Math.Max(0, nameWidth - 3), bounds.Height), e.ForeColor, flags);
                    if (!string.IsNullOrEmpty(item.Description) && bounds.Width > nameWidth)
                        TextRenderer.DrawText(e.Graphics, item.Description, e.Font, new Rectangle(bounds.X + nameWidth, bounds.Y, bounds.Width - nameWidth - 3, bounds.Height), e.ForeColor, flags | TextFormatFlags.Right);
                    e.DrawFocusRectangle();
                };
                commandList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Font.Height + 8 };
                commandList.DrawItem += (s, e) =>
                {
                    e.DrawBackground();
                    if (e.Index < 0 || e.Index >= commandList.Items.Count) return;
                    var item = (PickerItem)commandList.Items[e.Index];
                    TextRenderer.DrawText(e.Graphics, item.Name, e.Font, new Rectangle(e.Bounds.X + 3, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 6), e.Bounds.Height), e.ForeColor,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    e.DrawFocusRectangle();
                };
                sections = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
                sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                sections.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                sections.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
                sections.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                sections.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                sectionHeaderFont = new Font(Font, FontStyle.Bold);
                Disposed += (s, e) => sectionHeaderFont.Dispose();
                resultHeader = new Label { Text = "Result actions", AutoSize = true, Margin = new Padding(0, 0, 0, 4), Font = sectionHeaderFont };
                sections.Controls.Add(resultHeader, 0, 0);
                sections.Controls.Add(resultList, 0, 1);
                sections.Controls.Add(new Label { Text = "MusicBee and plugin commands (Experimental)", AutoSize = true, Margin = new Padding(0, 10, 0, 4), Font = sectionHeaderFont }, 0, 2);
                sections.Controls.Add(commandList, 0, 3);
                sections.SizeChanged += (s, e) => UpdateResultListHeight();
                var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
                var select = new Button { Text = "Select", AutoSize = true, Enabled = false };
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(select);
                layout.Controls.Add(filter, 0, 0);
                layout.Controls.Add(sections, 0, 1);
                layout.Controls.Add(buttons, 0, 2);
                Controls.Add(layout);
                CancelButton = cancel;
                AcceptButton = select;
                filter.TextChanged += (s, e) => RefreshEntries();
                resultList.SelectedIndexChanged += (s, e) =>
                {
                    if (resultList.SelectedIndex >= 0) commandList.ClearSelected();
                    select.Enabled = resultList.SelectedIndex >= 0 || commandList.SelectedIndex >= 0;
                };
                commandList.SelectedIndexChanged += (s, e) =>
                {
                    if (commandList.SelectedIndex >= 0) resultList.ClearSelected();
                    select.Enabled = resultList.SelectedIndex >= 0 || commandList.SelectedIndex >= 0;
                };
                Action accept = () =>
                {
                    var item = (PickerItem)(resultList.SelectedItem ?? commandList.SelectedItem);
                    if (item == null) return;
                    SelectedAction = item.Create();
                    DialogResult = DialogResult.OK;
                    Close();
                };
                select.Click += (s, e) => accept();
                resultList.DoubleClick += (s, e) => accept();
                commandList.DoubleClick += (s, e) => accept();
                RefreshEntries();
                Shown += (s, e) => filter.Focus();
            }

            private void RefreshEntries()
            {
                var words = filter.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                resultList.BeginUpdate();
                commandList.BeginUpdate();
                resultList.Items.Clear();
                commandList.Items.Clear();
                foreach (var entry in entries.Where(x => words.All(w => x.SearchText.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(x => x.Name))
                {
                    var target = entry.IsResultAction ? resultList : commandList;
                    target.Items.Add(new PickerItem { Name = entry.Name, Description = entry.Description, Create = entry.Create });
                }
                resultList.EndUpdate();
                commandList.EndUpdate();
                resultHeader.Visible = resultList.Items.Count > 0;
                UpdateResultListHeight();
                if (resultList.Items.Count > 0) resultList.SelectedIndex = 0;
                else if (commandList.Items.Count > 0) commandList.SelectedIndex = 0;
            }

            private void UpdateResultListHeight()
            {
                int maxHeight = Math.Max(0, sections.ClientSize.Height / 2);
                int visibleRows = Math.Min(resultList.Items.Count, Math.Max(0, (maxHeight - 10) / resultList.ItemHeight));
                int height = visibleRows == 0 ? 0 : visibleRows * resultList.ItemHeight + 10;
                if (sections.RowStyles[1].Height != height) sections.RowStyles[1].Height = height;
            }

            private sealed class PickerItem
            {
                public string Name;
                public string Description;
                public Func<BaseActionData> Create;
                public override string ToString() => string.IsNullOrEmpty(Description) ? Name : Name + "  —  " + Description;
            }
        }

        private (FlowLayoutPanel, Panel) CreateActionControls(BaseActionData currentAction, Action<BaseActionData> updateAction, Services.ResultType resultType)
        {
            ComboBox comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200
            };
            comboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
            _actionSettingsToolTip.SetToolTip(comboBox, "Choose the action to run for this result type and shortcut.");

            var dropdownPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty
            };
            dropdownPanel.Controls.Add(comboBox);

            Panel optionsPanel = new Panel
            {
                AutoSize = true,
                Padding = new Padding(10, 0, 0, 0)
            };

            var availableActions = ActionRegistry.GetActionsForType(resultType);
            comboBox.Items.AddRange(availableActions.ToArray());
            const string moreCommands = "More commands…";
            comboBox.Items.Add(moreCommands);
            bool changingSelection = false;
            object lastSelection = null;

            void setSelection(BaseActionData action)
            {
                changingSelection = true;
                var previousCommand = comboBox.Items.Cast<object>().OfType<ActionDefinition>().FirstOrDefault(x => x.DataType == typeof(CommandActionData));
                if (previousCommand != null) comboBox.Items.Remove(previousCommand);
                ActionDefinition definition;
                if (action is CommandActionData command)
                {
                    definition = new ActionDefinition(command.DisplayName ?? command.PluginCommandName ?? command.Command?.ToString() ?? "Command", typeof(CommandActionData), () => command);
                    comboBox.Items.Insert(comboBox.Items.Count - 1, definition);
                }
                else definition = availableActions.FirstOrDefault(x => x.DataType == action.GetType());
                comboBox.SelectedItem = definition ?? availableActions[0];
                lastSelection = comboBox.SelectedItem;
                changingSelection = false;
            }

            void chooseAction()
            {
                using (var dialog = new ActionPickerDialog(resultType, _mbApi, _config.SearchUI))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedAction == null) return;
                    var action = dialog.SelectedAction;
                    setSelection(action);
                    updateAction(action);
                    UpdateOptionsPanel(dropdownPanel, optionsPanel, action);
                }
            }

            // Select the current action in the combo box based on Type
            setSelection(currentAction);

            UpdateOptionsPanel(dropdownPanel, optionsPanel, currentAction);

            // Add event handler to update options when selection changes
            comboBox.SelectedIndexChanged += (sender, e) =>
            {
                if (changingSelection) return;
                if (Equals(comboBox.SelectedItem, moreCommands))
                {
                    changingSelection = true;
                    comboBox.SelectedItem = lastSelection;
                    changingSelection = false;
                    chooseAction();
                    return;
                }
                if (comboBox.SelectedItem is ActionDefinition def)
                {
                    lastSelection = def;
                    var newAction = def.Factory();
                    updateAction(newAction);
                    UpdateOptionsPanel(dropdownPanel, optionsPanel, newAction);
                }
            };

            return (dropdownPanel, optionsPanel);
        }

        private void UpdateOptionsPanel(FlowLayoutPanel dropdownPanel, Panel panel, BaseActionData action)
        {
            while (dropdownPanel.Controls.Count > 1)
            {
                var control = dropdownPanel.Controls[1];
                _actionSettingsToolTip.SetToolTip(control, null);
                control.Dispose();
            }
            while (panel.Controls.Count > 0)
            {
                var control = panel.Controls[0];
                _actionSettingsToolTip.SetToolTip(control, null);
                control.Dispose();
            }
            var controls = new List<Control>();
            if (action is DoNothingActionData || action is EditTagsActionData) return;

            // Action-specific options
            switch (action)
            {
                case OpenInMusicExplorerActionData _:
                    // No specific options for this action.
                    break;
                case OpenInMusicExplorerInTabActionData meTabAction:
                    var meTabComboBox = new ComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Width = 100
                    };
                    meTabComboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
                    meTabComboBox.Items.AddRange(Enum.GetNames(typeof(TabChoice)));
                    meTabComboBox.SelectedIndex = (int)meTabAction.TabChoice;
                    meTabComboBox.SelectedIndexChanged += (s, e) => meTabAction.TabChoice = (TabChoice)meTabComboBox.SelectedIndex;
                    controls.Add(meTabComboBox);
                    break;
                case OpenPlaylistInTabActionData playlistAction:
                    var plTabComboBox = new ComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Width = 100
                    };
                    plTabComboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
                    plTabComboBox.Items.AddRange(Enum.GetNames(typeof(TabChoice)));
                    plTabComboBox.SelectedIndex = (int)playlistAction.TabChoice;
                    plTabComboBox.SelectedIndexChanged += (s, e) => playlistAction.TabChoice = (TabChoice)plTabComboBox.SelectedIndex;
                    controls.Add(plTabComboBox);
                    break;

                case PlayActionData playAction:
                    var shufflePlayCheckBox = new CheckBox
                    {
                        Text = "Shuffle Play",
                        AutoSize = true,
                        Checked = playAction.ShufflePlay
                    };
                    shufflePlayCheckBox.CheckedChanged += (s, e) => playAction.ShufflePlay = shufflePlayCheckBox.Checked;
                    controls.Add(shufflePlayCheckBox);
                    break;

                case QueueNextActionData queueNextAction:
                    var shuffleQueueNextCheckBox = new CheckBox
                    {
                        Text = "Shuffle Play",
                        AutoSize = true,
                        Checked = queueNextAction.ShufflePlay
                    };
                    shuffleQueueNextCheckBox.CheckedChanged += (s, e) => queueNextAction.ShufflePlay = shuffleQueueNextCheckBox.Checked;

                    var clearQueueCheckBox = new CheckBox
                    {
                        Text = "Clear Queue",
                        AutoSize = true,
                        Checked = queueNextAction.ClearQueueBeforeAdd
                    };
                    clearQueueCheckBox.CheckedChanged += (s, e) => queueNextAction.ClearQueueBeforeAdd = clearQueueCheckBox.Checked;
                    
                    controls.AddRange(new Control[] { shuffleQueueNextCheckBox, clearQueueCheckBox });
                    break;

                case QueueLastActionData queueLastAction:
                    var shuffleQueueLastCheckBox = new CheckBox
                    {
                        Text = "Shuffle Play",
                        AutoSize = true,
                        Checked = queueLastAction.ShufflePlay
                    };
                    shuffleQueueLastCheckBox.CheckedChanged += (s, e) => queueLastAction.ShufflePlay = shuffleQueueLastCheckBox.Checked;
                    controls.Add(shuffleQueueLastCheckBox);
                    break;

                case SearchInTabActionData searchAction:
                    var tabComboBox = new ComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Width = 100
                    };
                    tabComboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
                    tabComboBox.Items.AddRange(Enum.GetNames(typeof(TabChoice)));
                    tabComboBox.SelectedIndex = (int)searchAction.TabChoice;
                    tabComboBox.SelectedIndexChanged += (s, e) => searchAction.TabChoice = (TabChoice)tabComboBox.SelectedIndex;
                    controls.Add(tabComboBox);

                    var useSortArtistCheckBox = new CheckBox
                    {
                        Text = "Use Sort Artist",
                        AutoSize = true,
                        Checked = searchAction.UseSortArtist
                    };
                    useSortArtistCheckBox.CheckedChanged += (s, e) => searchAction.UseSortArtist = useSortArtistCheckBox.Checked;

                    var searchAddPrefixCheckBox = new CheckBox
                    {
                        Text = "Add Search Prefix",
                        AutoSize = true,
                        Checked = searchAction.SearchAddPrefix
                    };
                    searchAddPrefixCheckBox.CheckedChanged += (s, e) => searchAction.SearchAddPrefix = searchAddPrefixCheckBox.Checked;

                    var clearSearchBarCheckBox = new CheckBox
                    {
                        Text = "Clear Search Box After Search",
                        AutoSize = true,
                        Checked = searchAction.ClearSearchBarTextAfterSearch
                    };
                    clearSearchBarCheckBox.CheckedChanged += (s, e) => searchAction.ClearSearchBarTextAfterSearch = clearSearchBarCheckBox.Checked;

                    var useSearchBarTextCheckBox = new CheckBox
                    {
                        Text = "Use Search Bar Text",
                        AutoSize = true,
                        Checked = searchAction.UseSearchBarText
                    };
                    useSearchBarTextCheckBox.CheckedChanged += (s, e) => searchAction.UseSearchBarText = useSearchBarTextCheckBox.Checked;

                    var toggleSearchEntireLibraryCheckBox = new CheckBox
                    {
                        Text = "Toggle Search Entire Library",
                        AutoSize = true,
                        Checked = searchAction.ToggleSearchEntireLibraryBeforeSearch
                    };
                    toggleSearchEntireLibraryCheckBox.CheckedChanged += (s, e) => 
                        searchAction.ToggleSearchEntireLibraryBeforeSearch = toggleSearchEntireLibraryCheckBox.Checked;

                    var useLeftSidebarCheckBox = new CheckBox
                    {
                        Text = "Use Left Sidebar",
                        AutoSize = true,
                        Checked = searchAction.UseLeftSidebar
                    };
                    useLeftSidebarCheckBox.CheckedChanged += (s, e) => 
                        searchAction.UseLeftSidebar = useLeftSidebarCheckBox.Checked;

                    controls.AddRange(new Control[] { 
                        useSortArtistCheckBox, 
                        searchAddPrefixCheckBox,
                        clearSearchBarCheckBox,
                        useSearchBarTextCheckBox,
                        toggleSearchEntireLibraryCheckBox,
                        useLeftSidebarCheckBox
                    });
                    break;

                case OpenFilterInTabActionData filterAction:
                    var filterTabComboBox = new ComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Width = 100
                    };
                    filterTabComboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
                    filterTabComboBox.Items.AddRange(Enum.GetNames(typeof(TabChoice)));
                    filterTabComboBox.SelectedIndex = (int)filterAction.TabChoice;
                    filterTabComboBox.SelectedIndexChanged += (s, e) => filterAction.TabChoice = (TabChoice)filterTabComboBox.SelectedIndex;
                    controls.Add(filterTabComboBox);

                    var filterUseSortArtistCheckBox = new CheckBox
                    {
                        Text = "Use Sort Artist",
                        AutoSize = true,
                        Checked = filterAction.UseSortArtist
                    };
                    filterUseSortArtistCheckBox.CheckedChanged += (s, e) => filterAction.UseSortArtist = filterUseSortArtistCheckBox.Checked;

                    var goBackBeforeFilterCheckBox = new CheckBox
                    {
                        Text = "Go Back Before Opening Filter",
                        AutoSize = true,
                        Checked = filterAction.GoBackBeforeOpenFilter
                    };
                    goBackBeforeFilterCheckBox.CheckedChanged += (s, e) => filterAction.GoBackBeforeOpenFilter = goBackBeforeFilterCheckBox.Checked;

                    controls.AddRange(new Control[] { filterUseSortArtistCheckBox, goBackBeforeFilterCheckBox });
                    break;
            }

            // Add common option last
            var focusMainCheckBox = new CheckBox
            {
                Text = "Focus Main Panel After Action",
                AutoSize = true,
                Checked = action.FocusMainPanelAfterAction
            };
            focusMainCheckBox.CheckedChanged += (s, e) => action.FocusMainPanelAfterAction = focusMainCheckBox.Checked;
            controls.Add(focusMainCheckBox);

            // Position controls vertically
            int currentY = 0;
            foreach (var control in controls)
            {
                if (control is ComboBox)
                {
                    _actionSettingsToolTip.SetToolTip(control, "Choose the MusicBee tab where this action runs.");
                    dropdownPanel.Controls.Add(control);
                    continue;
                }
                string description;
                switch (control.Text)
                {
                    case "Shuffle Play":
                        description = "Shuffle the result's tracks before playing or adding them to the queue.";
                        break;
                    case "Clear Queue":
                        description = "Clear the current queue before adding the result.";
                        break;
                    case "Use Sort Artist":
                        description = "Use the sort artist or sort album artist field when available.";
                        break;
                    case "Add Search Prefix":
                        description = "Add MusicBee field prefixes to the generated search text.";
                        break;
                    case "Clear Search Box After Search":
                        description = "Clear MusicBee's search box after performing the search.";
                        break;
                    case "Use Search Bar Text":
                        description = "Search for the text typed in this search bar instead of the selected result.";
                        break;
                    case "Toggle Search Entire Library":
                        description = "Toggle MusicBee's search scope before the search, then toggle it back afterward.";
                        break;
                    case "Use Left Sidebar":
                        description = "Search in MusicBee's left sidebar instead of its main search box.";
                        break;
                    case "Go Back Before Opening Filter":
                        description = "Navigate back in the selected tab before opening the filter.";
                        break;
                    case "Focus Main Panel After Action":
                        description = "Move focus to MusicBee's main panel after the action runs.";
                        break;
                    default:
                        description = null;
                        break;
                }
                if (description != null) _actionSettingsToolTip.SetToolTip(control, description);
                control.Location = new Point(0, currentY);
                panel.Controls.Add(control);
                currentY += control.Height + 4;
            }
        }

        // CreateActionFromComboBox and GetActionIndex are no longer needed with the ActionRegistry pattern
    }
}
