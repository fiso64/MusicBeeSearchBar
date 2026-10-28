using MusicBeePlugin.Config;
using MusicBeePlugin.Services;
using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using static MusicBeePlugin.Plugin;

namespace MusicBeePlugin.UI
{
    public partial class SearchBar
    {
        private sealed class ClearSearchButton : Control
        {
            private readonly Color normalColor;
            private readonly Color hoverColor;
            private bool hovered;

            public ClearSearchButton(Theme theme, float scale)
            {
                normalColor = theme.IconDimmed;
                hoverColor = theme.Icon;
                Width = Math.Max(20, (int)(24 * scale));
                Dock = DockStyle.Right;
                BackColor = theme.Background;
                Cursor = Cursors.Hand;
                TabStop = false;
                AccessibleName = "Clear search";
                AccessibleRole = AccessibleRole.PushButton;
                SetStyle(ControlStyles.Selectable, false);
                SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float half = Math.Max(3, Math.Min(Width, Height) * 0.19f);
                float x = Width / 2f, y = Height / 2f;
                using (var pen = new Pen(hovered ? hoverColor : normalColor, Math.Max(1f, DeviceDpi / 96f * 1.4f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    e.Graphics.DrawLine(pen, x - half, y - half, x + half, y + half);
                    e.Graphics.DrawLine(pen, x + half, y - half, x - half, y + half);
                }
            }
        }

        private void InitializeUI(float dpiScale)
        {
            FormClosed += (s, e) =>
            {
                // This is our final cleanup. Ensure the overlay is gone when the search bar closes.
                CloseOverlay();
            };

            int lineSize = 3;
            songIcon = CreateIcon(theme.Icon, iconSize, iconSize, Services.ResultType.Song, lineSize);
            albumIcon = CreateIcon(theme.Icon, iconSize, iconSize, Services.ResultType.Album, lineSize - 1);
            artistIcon = CreateIcon(theme.IconDimmed, iconSize, iconSize, Services.ResultType.Artist, lineSize);
            playlistIcon = CreateIcon(theme.Icon, iconSize, iconSize, Services.ResultType.Playlist, lineSize);
            commandIcon = CreateIcon(theme.Icon, iconSize, iconSize, Services.ResultType.Command, lineSize -1);

            Size = searchUIConfig.InitialSize;
            BackColor = theme.Background;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            if (!isEmbedded) TopMost = true;

            if (!isEmbedded) ResetPosition();


            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent, // Let the form's background show through
                Padding = new Padding((int)(8 * dpiScale)) // This creates the offset from the main rounded border
            };

            dragPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(12 * dpiScale),
                BackColor = Color.Transparent,
                Cursor = Cursors.SizeAll,
                Visible = false,
            };
            dragPanel.MouseDown += DragPanel_MouseDown;
            dragPanel.MouseMove += DragPanel_MouseMove;
            dragPanel.MouseUp += DragPanel_MouseUp;
            dragPanel.Paint += (s, e) =>
            {
                float dotSize = Math.Max(1.5f, 2.5f * dpiScale);
                float dotSpacing = 5f * dpiScale;
                float centerX = dragPanel.ClientSize.Width / 2f;
                float centerY = (dragPanel.ClientSize.Height - 1) / 2f - dpiScale;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(theme.IconDimmed))
                {
                    for (int i = 0; i < 6; i++)
                    {
                        float dotX = centerX + (i % 3 - 1) * dotSpacing;
                        float dotY = centerY + (i / 3 - 0.5f) * dotSpacing;
                        e.Graphics.FillEllipse(brush, dotX - dotSize / 2f,
                            dotY - dotSize / 2f, dotSize, dotSize);
                    }
                }
            };
            dragPanel.SizeChanged += (s, e) => dragPanel.Invalidate();

            var searchBoxContainer = new Panel
            {
                Dock = DockStyle.Top,
                // Use font metrics for padding to guarantee vertical centering.
                Padding = new Padding((int)(12 * dpiScale), (searchBoxHeight - searchBoxFont.Height) / 2, (int)(12 * dpiScale), (searchBoxHeight - searchBoxFont.Height) / 2),
                BackColor = Color.Transparent, // The border will be painted by the parent form
                Height = searchBoxHeight,
                BorderStyle = BorderStyle.None,
            };

            if (isEmbedded && mbApi.MB_AddPanel != null)
            {
                try
                {
                    var musicBeeTextBox = mbApi.MB_AddPanel(null, PluginPanelDock.TextBox);
                    searchBox = musicBeeTextBox as TextBox;
                    if (searchBox == null)
                    {
                        musicBeeTextBox?.Dispose();
                        Logger.Info("MusicBee did not provide a compatible text box for the docked search panel.");
                    }
                }
                catch (Exception ex) { Logger.Error("Could not create MusicBee's text box for the docked search panel", ex); }
            }
            if (searchBox == null) searchBox = new TextBox();
            searchBox.Font = searchBoxFont;
            searchBox.ForeColor = theme.Foreground;
            searchBox.BackColor = theme.Background;
            searchBox.BorderStyle = BorderStyle.None;
            searchBox.Dock = DockStyle.Fill;
            searchBox.TextAlign = HorizontalAlignment.Left;
            searchBox.ShortcutsEnabled = true;
            searchBox.AutoCompleteMode = AutoCompleteMode.Suggest;
            searchBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            searchBox.TextChanged += SearchBox_TextChanged;
            if (isEmbedded)
            {
                searchBox.Enter += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(searchBox.Text)
                        && searchUIConfig.DefaultResults != SearchUIConfig.DefaultResultsChoice.None)
                        SearchBoxSetDefaultResults();
                };
            }
            searchBoxContainer.Controls.Add(searchBox);
            searchBox.TabStop = true;

            if (searchUIConfig.ShowPlaceholderText)
            {
                this.Shown += (s, e) =>
                {
                    if (searchBox.IsHandleCreated)
                    {
                        WinApiHelpers.SetCueBanner(searchBox.Handle, "Search");
                    }
                };
            }

            searchToolbar = new SearchToolbar(theme, dpiScale, resultDetailFont)
            {
                Dock = DockStyle.Top,
                Visible = searchUIConfig.ShowSearchToolbar
            };
            searchToolbar.ShowDetachButton = !isEmbedded;
            searchToolbar.SquareCorners = squareCorners;
            searchToolbar.PrefixSelected += prefix =>
            {
                searchBox.Text = SearchToolbar.ReplacePrefix(searchBox.Text, prefix);
                searchBox.Select(searchBox.Text.Length, 0);
                if (!UsesListSelection) searchBox.Focus();
            };
            searchToolbar.SettingsClicked += (s, e) => OpenSearchSettings();
            searchToolbar.DetachClicked += (s, e) => ToggleDetachedMode();
            searchToolbar.ShowAllClicked += (s, e) => ToggleShowAllResults();

            spacerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(8 * dpiScale),
                BackColor = Color.Transparent
            };

            var baseColor = theme.Background;
            var highlightColor = theme.Highlight;
            var hoverColor = Color.FromArgb(
                255,
                baseColor.R + (highlightColor.R - baseColor.R) / 2,
                baseColor.G + (highlightColor.G - baseColor.G) / 2,
                baseColor.B + (highlightColor.B - baseColor.B) / 2
            );

            resultsListBox = new CustomResultList
            {
                DpiScale = dpiScale,
                Dock = DockStyle.Fill, // Use Fill to take up the remaining space
                BackColor = theme.Background,
                ForeColor = theme.Foreground,
                HighlightColor = theme.Highlight,
                HoverColor = hoverColor,
                Theme = this.theme,
                SquareCorners = squareCorners,
                ItemHeight = (int)(searchUIConfig.ResultItemHeight * dpiScale),
                CommandItemHeight = Math.Max(1, (int)(Math.Max(24, searchUIConfig.CommandResultItemHeight) * dpiScale)),
                Visible = false,
                Height = 0,
                ResultFont = this.resultFont,
                ResultDetailFont = this.resultDetailFont,
                TopMatchResultFont = this.topMatchResultFont,
                TopMatchResultDetailFont = this.topMatchResultDetailFont,
                ImageService = this.imageService,
                ShowTypeIcons = searchUIConfig.ShowTypeIcons,
                ShowAlbumNamesInSongDetails = searchUIConfig.ShowAlbumNamesInSongDetails,
                Icons = new Dictionary<ResultType, Image>
                {
                    { ResultType.Song, songIcon },
                    { ResultType.Album, albumIcon },
                    { ResultType.Artist, artistIcon },
                    { ResultType.Playlist, playlistIcon },
                    { ResultType.Command, commandIcon },
                }
            };
            if (UsesListSelection) resultsListBox.DoubleClickToActivate = true;
            resultsListBox.Click += ResultsListBox_Click;
            resultsListBox.ResultDoubleClicked += ResultsListBox_DoubleClick;
            resultsListBox.Scrolled += ResultsListBox_Scrolled;
            resultsListBox.SelectedIndexChanged += (s, e) =>
            {
                if (isEmbedded && !compactPanel && !_restoringExpandedSearch)
                    Plugin.SaveExpandedSearchState(this);
            };
            resultsListBox.ActionButtonClicked += ResultsListBox_ActionButtonClicked;
            resultsListBox.DetailLinkClicked += ResultsListBox_DetailLinkClicked;
            resultsListBox.ResultContextRequested += ShowResultContextMenu;

            // Add controls to main panel in correct order for docking
            mainPanel.Controls.Add(resultsListBox); // Fills remaining space
            mainPanel.Controls.Add(spacerPanel);
            mainPanel.Controls.Add(searchToolbar);
            mainPanel.Controls.Add(searchBoxContainer);
            mainPanel.Controls.Add(dragPanel); // At the very top
            Controls.Add(mainPanel);
            if (compactPanel) InitializeResultsPopup();
            if (!isEmbedded && searchUIConfig.ShowSearchToolbar)
                Height = Math.Max(Height + searchToolbar.Height,
                    searchBoxContainer.Height + searchToolbar.Height + mainPanel.Padding.Vertical);

            if (!isEmbedded) searchBox.Focus(); // Set focus to the floating search box initially

            InitializeLoadingIndicator();
        }

        private void InitializeResizeGrips(float dpiScale)
        {
            int gripWidth = Math.Max(4, (int)(6 * dpiScale));

            resizeGripRight = new Panel
            {
                Width = gripWidth,
                Height = ClientSize.Height,
                Location = new Point(ClientSize.Width - gripWidth, 0),
                BackColor = Color.Transparent,
                Cursor = Cursors.SizeWE,
                Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
            };
            resizeGripRight.MouseDown += (s, e) => BeginWidthResize(fromLeft: false, resizeGripRight);
            resizeGripRight.MouseMove += ResizeGrip_MouseMove;
            resizeGripRight.MouseUp += ResizeGrip_MouseUp;
            resizeGripRight.MouseCaptureChanged += ResizeGrip_MouseCaptureChanged;

            resizeGripLeft = new Panel
            {
                Width = gripWidth,
                Height = ClientSize.Height,
                Location = new Point(0, 0),
                BackColor = Color.Transparent,
                Cursor = Cursors.SizeWE,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom,
            };
            resizeGripLeft.MouseDown += (s, e) => BeginWidthResize(fromLeft: true, resizeGripLeft);
            resizeGripLeft.MouseMove += ResizeGrip_MouseMove;
            resizeGripLeft.MouseUp += ResizeGrip_MouseUp;
            resizeGripLeft.MouseCaptureChanged += ResizeGrip_MouseCaptureChanged;

            Controls.Add(resizeGripRight);
            Controls.Add(resizeGripLeft);
            resizeGripRight.BringToFront();
            resizeGripLeft.BringToFront();
        }

        private void InitializeHotkeys()
        {
            // Assign event handlers defined in SearchBar.EventHandlers.cs
            searchBox.KeyDown += HandleSearchBoxKeyDown;
            resultsListBox.KeyDown += HandleResultsKeyDown;
            if (!isEmbedded) Deactivate += HandleFormDeactivate;
            KeyDown += HandleFormKeyDown;
            KeyPreview = true; // Need to set KeyPreview to true for Form to receive KeyDown events before controls
        }

        private void InitializeLoadingIndicator()
        {
            const int ROTATION_INTERVAL = 20;
            const int ROTATION_ANGLE = 20;
            var COLOR = Color.FromArgb((int)(255 * 0.3), theme.Foreground);

            var searchContainer = searchBox.Parent;
            var clearButton = new ClearSearchButton(theme, dpiScale) { Visible = searchBox.TextLength > 0 };
            clearButton.Click += (s, e) =>
            {
                searchBox.Clear();
                if (!UsesListSelection) searchBox.Focus();
            };
            searchContainer.Controls.Add(clearButton);
            int availableHeight = searchContainer.ClientSize.Height - searchContainer.Padding.Vertical;
            int indicatorSize = Math.Max(8, availableHeight - 4);

            Bitmap CreateLoadingSpinner(int size, Color color)
            {
                Bitmap bmp = new Bitmap(size, size);
                using (Graphics g = Graphics.FromImage(bmp))
                using (Pen pen = new Pen(color, Math.Max(1f, size / 12f)))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    float pad = pen.Width / 2;
                    if (pad == 0) pad = 0.5f;
                    g.DrawArc(pen, pad, pad, size - (pad * 2), size - (pad * 2), 0, 300);
                }
                return bmp;
            }

            void UpdateLoadingIndicatorPosition()
            {
                if (loadingIndicator != null && !loadingIndicator.IsDisposed && loadingIndicator.Visible)
                {
                    if (searchContainer != null && searchContainer.IsHandleCreated)
                    {
                        loadingIndicator.Location = new Point(
                            searchContainer.ClientSize.Width - searchContainer.Padding.Right
                                - (searchBox.TextLength > 0 ? clearButton.Width : 0) - loadingIndicator.Width - 2,
                            searchContainer.Padding.Top + (availableHeight - loadingIndicator.Height) / 2
                        );
                    }
                }
            }

            loadingIndicator = new PictureBox
            {
                Size = new Size(indicatorSize, indicatorSize),
                BackColor = Color.Transparent,
                Image = CreateLoadingSpinner(indicatorSize, COLOR),
                Visible = true
            };

            searchContainer.SizeChanged += (s, e) => UpdateLoadingIndicatorPosition();
            Shown += (s, e) => UpdateLoadingIndicatorPosition();
            searchBox.TextChanged += (s, e) =>
            {
                clearButton.Visible = searchBox.TextLength > 0;
                UpdateLoadingIndicatorPosition();
            };
            loadingIndicator.VisibleChanged += (s, e) => UpdateLoadingIndicatorPosition();


            searchContainer.Controls.Add(loadingIndicator);
            loadingIndicator.BringToFront();

            var spinTimer = new System.Windows.Forms.Timer { Interval = ROTATION_INTERVAL };
            float rotationAngle = 0;
            spinTimer.Tick += (s, e) =>
            {
                if (loadingIndicator != null && !loadingIndicator.IsDisposed && loadingIndicator.Visible && loadingIndicator.Image != null)
                {
                    Bitmap currentImage = (Bitmap)loadingIndicator.Image;
                    Bitmap rotatedBmp = new Bitmap(currentImage.Width, currentImage.Height);
                    float penWidth = Math.Max(1f, rotatedBmp.Width / 12f);

                    using (Graphics g = Graphics.FromImage(rotatedBmp))
                    using (Pen pen = new Pen(COLOR, penWidth))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TranslateTransform(rotatedBmp.Width / 2f, rotatedBmp.Height / 2f);
                        g.RotateTransform(rotationAngle);
                        g.TranslateTransform(-rotatedBmp.Width / 2f, -rotatedBmp.Height / 2f);
                        float pad = pen.Width / 2;
                        if (pad == 0) pad = 0.5f;
                        g.DrawArc(pen, pad, pad, rotatedBmp.Width - (pad * 2), rotatedBmp.Height - (pad * 2), 0, 300);
                    }
                    loadingIndicator.Image = rotatedBmp;
                    currentImage.Dispose();

                    rotationAngle += ROTATION_ANGLE;
                    if (rotationAngle >= 360)
                    {
                        rotationAngle -= 360;
                    }
                }
                else if (loadingIndicator != null && !loadingIndicator.IsDisposed && loadingIndicator.Visible && loadingIndicator.Image == null)
                {
                    loadingIndicator.Image = CreateLoadingSpinner(loadingIndicator.Width, COLOR);
                }
            };
            FormClosed += (s, e) => spinTimer?.Dispose();
            Disposed += (s, e) => spinTimer?.Dispose();
            spinTimer.Start();
        }


    }
}
