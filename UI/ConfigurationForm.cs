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

        public ConfigurationForm(Config.Config config, Plugin.MusicBeeApiInterface mbApi, int initialTabIndex = 0)
        {
            // Create a deep copy of the config for our use
            _config = JsonConvert.DeserializeObject<Config.Config>(
                JsonConvert.SerializeObject(config));
            _mbApi = mbApi;
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
            this.MinimumSize = new Size(800, 600);
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
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = true,
                AutoScroll = true
            };

            actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            actionsLayout.Controls.Add(CreateActionPanel("Artist Actions", _config.SearchActions.ArtistAction, Services.ResultType.Artist), 0, 0);
            actionsLayout.Controls.Add(CreateActionPanel("Album Actions", _config.SearchActions.AlbumAction, Services.ResultType.Album), 0, 1);
            actionsLayout.Controls.Add(CreateActionPanel("Song Actions", _config.SearchActions.SongAction, Services.ResultType.Song), 0, 2);
            actionsLayout.Controls.Add(CreateActionPanel("Playlist Actions", _config.SearchActions.PlaylistAction, Services.ResultType.Playlist), 0, 3);
            
            var actionsScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            actionsScrollPanel.Controls.Add(actionsLayout);
            actionsTab.Controls.Add(actionsScrollPanel);

            // Configure Search tab using factory
            var searchLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 2,
                AutoSize = false,
                AutoScroll = true
            };
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            ConfigControlFactory.CreateControlsForObject(searchLayout, _config.SearchUI, "Search");
            searchTab.Controls.Add(searchLayout);

            // Configure Appearance tab using factory
            var appearanceLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 2,
                AutoSize = false,
                AutoScroll = true
            };
            appearanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            appearanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            ConfigControlFactory.CreateControlsForObject(appearanceLayout, _config.SearchUI, "Appearance");
            appearanceTab.Controls.Add(appearanceLayout);
            
            // Configure Help Tab
            var helpTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Multiline = true,
                BorderStyle = BorderStyle.None,
                BackColor = this.BackColor,
                Font = new Font("Consolas", 10),
                Text = string.Join(Environment.NewLine, new[]
                {
                    "Quick Reference",
                    "",
                    "Prefixes",
                    "Use these at the start of your search to filter results:",
                    "  a:        Artists",
                    "  l:        Albums",
                    "  s:        Songs",
                    "  p:        Playlists",
                    "  >         Commands",
                    "",
                    "Suffixes",
                    "Use these at the end of your search to show more results:",
                    "  ..        Show up to 100 results",
                    "  ....      Show up to 1000 results",
                    "  ......    Show up to 10000 results, etc.",
                    "",
                    "Shortcuts",
                    "These can be used while the search bar is open:",
                    "  Enter             Execute default action for selected result",
                    "  Ctrl+Enter        Execute alternative action",
                    "  Shift+Enter       Execute alternative action",
                    "  Ctrl+Shift+Enter  Execute alternative action",
                    "  Alt+R             Execute artist action for selected result",
                    "  Alt+A             Execute album action for selected result",
                    "  Ctrl+S            Shuffle play selected result",
                    "  Ctrl+D            Toggle detached mode",
                    "  Ctrl+P            Open the settings dialog",
                    "  Ctrl+H            Open this help page",
                })
            };
            helpTab.Controls.Add(helpTextBox);

            tabControl.TabPages.Add(actionsTab);
            tabControl.TabPages.Add(searchTab);
            tabControl.TabPages.Add(appearanceTab);
            tabControl.TabPages.Add(helpTab);

            this.Controls.Add(tabControl);

            // Add bottom buttons panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
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
            this.Controls.Add(buttonPanel);

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

        private GroupBox CreateActionPanel(string title, ActionConfig actionConfig, Services.ResultType resultType)
        {
            GroupBox groupBox = new GroupBox
            {
                Text = title,
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(10)
            };

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 4,
                AutoSize = true
            };

            // Add row headers
            layout.Controls.Add(new Label { Text = "Enter:", AutoSize = true }, 0, 0);
            layout.Controls.Add(new Label { Text = "Ctrl+Enter:", AutoSize = true }, 0, 1);
            layout.Controls.Add(new Label { Text = "Shift+Enter:", AutoSize = true }, 0, 2);
            layout.Controls.Add(new Label { Text = "Ctrl+Shift+Enter:", AutoSize = true }, 0, 3);

            // Add action combo boxes with their corresponding options panels
            var (defaultCombo, defaultOptions) = CreateActionControls(actionConfig.Default, actionConfig, resultType);
            var (ctrlCombo, ctrlOptions) = CreateActionControls(actionConfig.Ctrl, actionConfig, resultType);
            var (shiftCombo, shiftOptions) = CreateActionControls(actionConfig.Shift, actionConfig, resultType);
            var (ctrlShiftCombo, ctrlShiftOptions) = CreateActionControls(actionConfig.CtrlShift, actionConfig, resultType);

            layout.Controls.Add(defaultCombo, 1, 0);
            layout.Controls.Add(ctrlCombo, 1, 1);
            layout.Controls.Add(shiftCombo, 1, 2);
            layout.Controls.Add(ctrlShiftCombo, 1, 3);

            layout.Controls.Add(defaultOptions, 2, 0);
            layout.Controls.Add(ctrlOptions, 2, 1);
            layout.Controls.Add(shiftOptions, 2, 2);
            layout.Controls.Add(ctrlShiftOptions, 2, 3);

            groupBox.Controls.Add(layout);
            return groupBox;
        }

        private (ComboBox, Panel) CreateActionControls(BaseActionData currentAction, ActionConfig actionConfig, Services.ResultType resultType)
        {
            ComboBox comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200
            };
            comboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;

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

            UpdateOptionsPanel(optionsPanel, currentAction);

            // Store reference to which action we're modifying
            Action<BaseActionData> updateAction = null;
            if (currentAction == actionConfig.Default) updateAction = x => actionConfig.Default = x;
            else if (currentAction == actionConfig.Ctrl) updateAction = x => actionConfig.Ctrl = x;
            else if (currentAction == actionConfig.Shift) updateAction = x => actionConfig.Shift = x;
            else if (currentAction == actionConfig.CtrlShift) updateAction = x => actionConfig.CtrlShift = x;

            // Add event handler to update options when selection changes
            comboBox.SelectedIndexChanged += (sender, e) =>
            {
                if (comboBox.SelectedItem is ActionDefinition def)
                {
                    var newAction = def.Factory();
                    updateAction(newAction);
                    UpdateOptionsPanel(optionsPanel, newAction);
                }
            };

            return (comboBox, optionsPanel);
        }

        private void UpdateOptionsPanel(Panel panel, BaseActionData action)
        {
            panel.Controls.Clear();
            var controls = new List<Control>();

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
                        Width = 150
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
                        Width = 150
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
                        Width = 150
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
                        Width = 150
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
                control.Location = new Point(0, currentY);
                panel.Controls.Add(control);
                currentY += control.Height + 4;
            }
        }

        // CreateActionFromComboBox and GetActionIndex are no longer needed with the ActionRegistry pattern
    }
}