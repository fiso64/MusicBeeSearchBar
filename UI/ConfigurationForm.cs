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

        // Add control fields
        private CheckBox groupResultsCheckbox;
        private NumericUpDown opacityInput;
        private NumericUpDown maxResultsInput;
        private NumericUpDown widthInput;
        private Button textColorButton;
        private Button baseColorButton;
        private Button highlightColorButton;

        public ConfigurationForm(Config.Config config, Plugin.MusicBeeApiInterface mbApi)
        {
            // Create a deep copy of the config for our use
            _config = JsonConvert.DeserializeObject<Config.Config>(
                JsonConvert.SerializeObject(config));
            _mbApi = mbApi;
            InitializeComponent();
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
            TabControl tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(12, 4)
            };

            // Add tabs
            TabPage actionsTab = new TabPage("Actions");
            TabPage searchTab = new TabPage("Search");
            TabPage appearanceTab = new TabPage("Appearance");
            
            // Configure Actions tab
            TableLayoutPanel actionsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = true,
                AutoScroll = true
            };

            // Set row styles to ensure proper spacing
            actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Add action configuration panels for each type
            actionsLayout.Controls.Add(CreateActionPanel("Artist Actions", _config.SearchActions.ArtistAction), 0, 0);
            actionsLayout.Controls.Add(CreateActionPanel("Album Actions", _config.SearchActions.AlbumAction), 0, 1);
            actionsLayout.Controls.Add(CreateActionPanel("Song Actions", _config.SearchActions.SongAction), 0, 2);
            actionsLayout.Controls.Add(CreateActionPanel("Playlist Actions", _config.SearchActions.PlaylistAction), 0, 3);

            // Wrap the layout in a panel to enable scrolling
            Panel actionsScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            actionsScrollPanel.Controls.Add(actionsLayout);
            actionsTab.Controls.Add(actionsScrollPanel);
            
            tabControl.TabPages.Add(actionsTab);
            tabControl.TabPages.Add(searchTab);
            tabControl.TabPages.Add(appearanceTab);

            this.Controls.Add(tabControl);

            // Configure Search tab
            TableLayoutPanel searchLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 2,
                RowCount = 8,
                AutoSize = true
            };

            // Set column styles for search tab
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Group Results checkbox
            groupResultsCheckbox = new CheckBox
            {
                Text = "Group Results by Type",
                Checked = _config.SearchUI.GroupResultsByType,
                AutoSize = true
            };
            groupResultsCheckbox.CheckedChanged += (s, e) => _config.SearchUI.GroupResultsByType = groupResultsCheckbox.Checked;
            searchLayout.Controls.Add(new Label { Text = "Group Results:", AutoSize = true }, 0, 1);
            searchLayout.Controls.Add(groupResultsCheckbox, 1, 1);

            // Enable Contains Check
            var enableContainsCheckbox = new CheckBox
            {
                Text = "Enable Contains Check",
                Checked = _config.SearchUI.EnableContainsCheck,
                AutoSize = true
            };
            enableContainsCheckbox.CheckedChanged += (s, e) => _config.SearchUI.EnableContainsCheck = enableContainsCheckbox.Checked;
            searchLayout.Controls.Add(new Label { Text = "Filter Results:", AutoSize = true }, 0, 2);
            searchLayout.Controls.Add(enableContainsCheckbox, 1, 2);

            // Artist Result Limit
            var artistLimitInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 10000,
                Value = _config.SearchUI.ArtistResultLimit,
                Width = 70
            };
            artistLimitInput.ValueChanged += (s, e) => _config.SearchUI.ArtistResultLimit = (int)artistLimitInput.Value;
            searchLayout.Controls.Add(new Label { Text = "Artist Result Limit:", AutoSize = true }, 0, 3);
            searchLayout.Controls.Add(artistLimitInput, 1, 3);

            // Album Result Limit
            var albumLimitInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 10000,
                Value = _config.SearchUI.AlbumResultLimit,
                Width = 70
            };
            albumLimitInput.ValueChanged += (s, e) => _config.SearchUI.AlbumResultLimit = (int)albumLimitInput.Value;
            searchLayout.Controls.Add(new Label { Text = "Album Result Limit:", AutoSize = true }, 0, 4);
            searchLayout.Controls.Add(albumLimitInput, 1, 4);

            // Song Result Limit
            var songLimitInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 10000,
                Value = _config.SearchUI.SongResultLimit,
                Width = 70
            };
            songLimitInput.ValueChanged += (s, e) => _config.SearchUI.SongResultLimit = (int)songLimitInput.Value;
            searchLayout.Controls.Add(new Label { Text = "Song Result Limit:", AutoSize = true }, 0, 5);
            searchLayout.Controls.Add(songLimitInput, 1, 5);

            // Playlist Result Limit
            var playlistLimitInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 10000,
                Value = _config.SearchUI.PlaylistResultLimit,
                Width = 70
            };
            playlistLimitInput.ValueChanged += (s, e) => _config.SearchUI.PlaylistResultLimit = (int)playlistLimitInput.Value;
            searchLayout.Controls.Add(new Label { Text = "Playlist Result Limit:", AutoSize = true }, 0, 6);
            searchLayout.Controls.Add(playlistLimitInput, 1, 6);

            // Default Results Choice
            var defaultResultsComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120
            };
            defaultResultsComboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
            defaultResultsComboBox.Items.AddRange(Enum.GetNames(typeof(SearchUIConfig.DefaultResultsChoice)));
            defaultResultsComboBox.SelectedItem = _config.SearchUI.DefaultResults.ToString();
            defaultResultsComboBox.SelectedIndexChanged += (s, e) =>
            {
                if (Enum.TryParse(defaultResultsComboBox.SelectedItem.ToString(), out SearchUIConfig.DefaultResultsChoice choice))
                {
                    _config.SearchUI.DefaultResults = choice;
                }
            };
            searchLayout.Controls.Add(new Label { Text = "Default Results:", AutoSize = true }, 0, 7);
            searchLayout.Controls.Add(defaultResultsComboBox, 1, 7);

            searchTab.Controls.Add(searchLayout);

            // Configure Appearance tab
            TableLayoutPanel appearanceLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 2,
                RowCount = 6,
                AutoSize = true
            };

            // Set column styles
            appearanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            appearanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Show Images checkbox
            var showImagesCheckbox = new CheckBox
            {
                Text = "Show Images in Results",
                Checked = _config.SearchUI.ShowImages,
                AutoSize = true
            };
            showImagesCheckbox.CheckedChanged += (s, e) => _config.SearchUI.ShowImages = showImagesCheckbox.Checked;
            appearanceLayout.Controls.Add(new Label { Text = "Show Images:", AutoSize = true }, 0, 0);
            appearanceLayout.Controls.Add(showImagesCheckbox, 1, 0);

            // Overlay Opacity numeric input
            opacityInput = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 100,
                Value = (decimal)(_config.SearchUI.OverlayOpacity * 100),
                Width = 70
            };
            opacityInput.ValueChanged += (s, e) => _config.SearchUI.OverlayOpacity = (double)opacityInput.Value / 100;
            appearanceLayout.Controls.Add(new Label { Text = "Overlay Opacity (%):", AutoSize = true }, 0, 1);
            appearanceLayout.Controls.Add(opacityInput, 1, 1);

            // Max Results numeric input
            maxResultsInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 20,
                Value = _config.SearchUI.MaxResultsVisible,
                Width = 70
            };
            maxResultsInput.ValueChanged += (s, e) => _config.SearchUI.MaxResultsVisible = (int)maxResultsInput.Value;
            appearanceLayout.Controls.Add(new Label { Text = "Max Visible Results:", AutoSize = true }, 0, 2);
            appearanceLayout.Controls.Add(maxResultsInput, 1, 2);

            // Color pickers
            textColorButton = CreateColorButton("Text Color", _config.SearchUI.TextColor);
            baseColorButton = CreateColorButton("Base Color", _config.SearchUI.BaseColor);
            highlightColorButton = CreateColorButton("Highlight Color", _config.SearchUI.ResultHighlightColor);

            appearanceLayout.Controls.Add(new Label { Text = "Text Color:", AutoSize = true }, 0, 3);
            appearanceLayout.Controls.Add(textColorButton, 1, 3);
            appearanceLayout.Controls.Add(new Label { Text = "Base Color:", AutoSize = true }, 0, 4);
            appearanceLayout.Controls.Add(baseColorButton, 1, 4);
            appearanceLayout.Controls.Add(new Label { Text = "Highlight Color:", AutoSize = true }, 0, 5);
            appearanceLayout.Controls.Add(highlightColorButton, 1, 5);

            // Initial Size inputs
            TableLayoutPanel sizePanel = new TableLayoutPanel
            {
                ColumnCount = 4,
                AutoSize = true
            };

            widthInput = new NumericUpDown
            {
                Minimum = 200,
                Maximum = 1000,
                Value = _config.SearchUI.InitialSize.Width,
                Width = 70
            };
            widthInput.ValueChanged += (s, e) => _config.SearchUI.InitialSize = new Size((int)widthInput.Value, _config.SearchUI.InitialSize.Height);

            sizePanel.Controls.Add(new Label { Text = "Width:", AutoSize = true }, 0, 0);
            sizePanel.Controls.Add(widthInput, 1, 0);

            appearanceLayout.Controls.Add(new Label { Text = "Initial Size:", AutoSize = true }, 0, 6);
            appearanceLayout.Controls.Add(sizePanel, 1, 6);

            appearanceTab.Controls.Add(appearanceLayout);

            // Add bottom buttons panel
            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50
            };

            Button saveButton = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Right,
                Width = 75,
                Height = 23,
                Location = new Point(buttonPanel.Width - 170, 14)
            };

            Button cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Anchor = AnchorStyles.Right,
                Width = 75,
                Height = 23,
                Location = new Point(buttonPanel.Width - 85, 14)
            };

            // Set the CancelButton property to enable Esc key
            this.CancelButton = cancelButton;

            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(cancelButton);
            this.Controls.Add(buttonPanel);

            this.ResumeLayout(false);

            // Simplify save button click handler to just validate
            saveButton.Click += (sender, e) =>
            {
                if (ValidateInputs())
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            // Simplify cancel button click handler
            cancelButton.Click += (sender, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
        }

        private GroupBox CreateActionPanel(string title, ActionConfig actionConfig)
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
            var (defaultCombo, defaultOptions) = CreateActionControls(actionConfig.Default, actionConfig);
            var (ctrlCombo, ctrlOptions) = CreateActionControls(actionConfig.Ctrl, actionConfig);
            var (shiftCombo, shiftOptions) = CreateActionControls(actionConfig.Shift, actionConfig);
            var (ctrlShiftCombo, ctrlShiftOptions) = CreateActionControls(actionConfig.CtrlShift, actionConfig);

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

        private (ComboBox, Panel) CreateActionControls(BaseActionData currentAction, ActionConfig actionConfig)
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

            comboBox.Items.AddRange(new string[]
            {
                "Play Now",
                "Queue Next",
                "Queue Last",
                "Search In Tab",
                "Open Filter In Tab"
            });

            // Set the current selection and create option controls
            comboBox.SelectedIndex = GetActionIndex(currentAction);
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
                var newAction = CreateActionFromIndex(comboBox.SelectedIndex);
                updateAction(newAction);  // Update the correct reference in the config
                UpdateOptionsPanel(optionsPanel, newAction);
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

        private BaseActionData CreateActionFromIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return new PlayActionData();
                case 1:
                    return new QueueNextActionData();
                case 2:
                    return new QueueLastActionData();
                case 3:
                    return new SearchInTabActionData();
                case 4:
                    return new OpenFilterInTabActionData();
                default:
                    return new PlayActionData();
            }
        }

        private int GetActionIndex(BaseActionData action)
        {
            if (action is PlayActionData)
                return 0;
            if (action is QueueNextActionData)
                return 1;
            if (action is QueueLastActionData)
                return 2;
            if (action is SearchInTabActionData)
                return 3;
            if (action is OpenFilterInTabActionData)
                return 4;
            return 0;
        }

        private Button CreateColorButton(string text, Color initialColor)
        {
            Button button = new Button
            {
                Text = text,
                Width = 100,
                Height = 23,
                BackColor = initialColor
            };

            button.Click += (sender, e) =>
            {
                using (ColorDialog colorDialog = new ColorDialog())
                {
                    colorDialog.Color = button.BackColor;
                    if (colorDialog.ShowDialog() == DialogResult.OK)
                    {
                        button.BackColor = colorDialog.Color;
                        // Store the selected color in the config when saving
                        switch (text)
                        {
                            case "Text Color":
                                _config.SearchUI.TextColor = colorDialog.Color;
                                break;
                            case "Base Color":
                                _config.SearchUI.BaseColor = colorDialog.Color;
                                break;
                            case "Highlight Color":
                                _config.SearchUI.ResultHighlightColor = colorDialog.Color;
                                break;
                        }
                    }
                }
            };

            return button;
        }

        private bool ValidateInputs()
        {
            // Validate opacity
            double opacity = (double)opacityInput.Value / 100;
            if (opacity < 0 || opacity > 1)
            {
                ShowError("Opacity must be between 0 and 100.");
                return false;
            }

            // Validate size
            if (widthInput.Value < 1 || widthInput.Value > 10000)
            {
                ShowError("Width must be between 1 and 10000 pixels.");
                return false;
            }

            return true;
        }

        private void ShowError(string message)
        {
            MessageBox.Show(
                message,
                "Validation Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
} 
