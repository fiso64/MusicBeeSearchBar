using System;
using System.Windows.Forms;
using MusicBeePlugin.Config;
using System.Drawing;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

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
            this.ClientSize = new Size(800, 600);
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
                new[] { "a:", "Artists" }, new[] { "l:", "Albums" }, new[] { "s:", "Songs" },
                new[] { "p:", "Playlists" }, new[] { ">", "Commands" }
            });
            AddHelpSection(helpLayout, "Suffixes", "Use these at the end of your search to show more results:", new[]
            {
                new[] { "..", "Show up to 100 results" }, new[] { "....", "Show up to 1,000 results" },
                new[] { "......", "Show up to 10,000 results, etc." }
            });
            AddHelpSection(helpLayout, "Shortcuts", "These can be used while the search bar is open:", new[]
            {
                new[] { "Enter", "Execute default action for selected result" },
                new[] { "Ctrl+Enter", "Execute alternative action" },
                new[] { "Shift+Enter", "Execute alternative action" },
                new[] { "Ctrl+Shift+Enter", "Execute alternative action" },
                new[] { "Alt+Enter", "Execute alternative action" },
                new[] { "Tab / Shift+Tab", "Cycle search filters" },
                new[] { "Shift+F10", "Open the selected result's context menu" },
                new[] { "Right", "Open the selected result's context menu (alternative, floating mode only)" },
                new[] { "Alt+R", "Execute artist action for selected result" },
                new[] { "Alt+A", "Execute album action for selected result" },
                new[] { "Ctrl+S", "Shuffle play selected result" },
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
            var (defaultCombo, defaultOptions) = CreateActionControls(actionConfig.Default, actionConfig, resultType);
            defaultLayout.Controls.Add(defaultCombo, 1, 0);
            defaultLayout.Controls.Add(defaultOptions, 2, 0);

            var alternativesLayout = CreateActionRowsLayout(4);
            alternativesLayout.Visible = false;
            alternativesLayout.Controls.Add(new Label { Text = "Ctrl+Enter:", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) }, 0, 0);
            alternativesLayout.Controls.Add(new Label { Text = "Shift+Enter:", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) }, 0, 1);
            alternativesLayout.Controls.Add(new Label { Text = "Ctrl+Shift+Enter:", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) }, 0, 2);
            alternativesLayout.Controls.Add(new Label { Text = "Alt+Enter:", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) }, 0, 3);

            var (ctrlCombo, ctrlOptions) = CreateActionControls(actionConfig.Ctrl, actionConfig, resultType);
            var (shiftCombo, shiftOptions) = CreateActionControls(actionConfig.Shift, actionConfig, resultType);
            var (ctrlShiftCombo, ctrlShiftOptions) = CreateActionControls(actionConfig.CtrlShift, actionConfig, resultType);
            var (altCombo, altOptions) = CreateActionControls(actionConfig.Alt, actionConfig, resultType);
            alternativesLayout.Controls.Add(ctrlCombo, 1, 0);
            alternativesLayout.Controls.Add(shiftCombo, 1, 1);
            alternativesLayout.Controls.Add(ctrlShiftCombo, 1, 2);
            alternativesLayout.Controls.Add(altCombo, 1, 3);
            alternativesLayout.Controls.Add(ctrlOptions, 2, 0);
            alternativesLayout.Controls.Add(shiftOptions, 2, 1);
            alternativesLayout.Controls.Add(ctrlShiftOptions, 2, 2);
            alternativesLayout.Controls.Add(altOptions, 2, 3);

            var alternativesLink = new LinkLabel
            {
                Text = "Show alternative actions (Ctrl / Shift / Alt)",
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 5)
            };
            alternativesLink.LinkClicked += (sender, e) =>
            {
                alternativesLayout.Visible = !alternativesLayout.Visible;
                alternativesLink.Text = alternativesLayout.Visible
                    ? "Hide alternative actions"
                    : "Show alternative actions (Ctrl / Shift / Alt)";
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
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < rowCount; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return layout;
        }

        private (FlowLayoutPanel, Panel) CreateActionControls(BaseActionData currentAction, ActionConfig actionConfig, Services.ResultType resultType)
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

            // Select the current action in the combo box based on Type
            ActionDefinition selectedDef = availableActions.FirstOrDefault(a => a.DataType == currentAction.GetType());
            if (selectedDef != null)
            {
                comboBox.SelectedItem = selectedDef;
            }
            else
            {
                // Fallback if config has an action not supported by the current type
                comboBox.SelectedIndex = 0;
            }

            UpdateOptionsPanel(dropdownPanel, optionsPanel, currentAction);

            // Store reference to which action we're modifying
            Action<BaseActionData> updateAction = null;
            if (currentAction == actionConfig.Default) updateAction = x => actionConfig.Default = x;
            else if (currentAction == actionConfig.Ctrl) updateAction = x => actionConfig.Ctrl = x;
            else if (currentAction == actionConfig.Shift) updateAction = x => actionConfig.Shift = x;
            else if (currentAction == actionConfig.CtrlShift) updateAction = x => actionConfig.CtrlShift = x;
            else if (currentAction == actionConfig.Alt) updateAction = x => actionConfig.Alt = x;

            // Add event handler to update options when selection changes
            comboBox.SelectedIndexChanged += (sender, e) =>
            {
                if (comboBox.SelectedItem is ActionDefinition def)
                {
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
            if (action is DoNothingActionData) return;

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
