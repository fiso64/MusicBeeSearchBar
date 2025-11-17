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
        private void InitializeUI()
        {
            var mbHandle = mbApi.MB_GetWindowHandle();

            if (searchUIConfig.OverlayOpacity > 0 && WinApiHelpers.IsWindowFocused(mbHandle))
            {
                musicBeeContext.Post(_ =>
                {
                    if (musicBeeControl != null && musicBeeControl.IsHandleCreated)
                    {
                        overlay = new OverlayForm(musicBeeControl, searchUIConfig.OverlayOpacity, 0.08);
                        overlay.Show();
                    }
                }, null);

                FormClosed += (s, e) =>
                {
                    musicBeeContext.Post(__ => {
                        if (overlay != null && !overlay.IsDisposed)
                        {
                            overlay.Close();
                        }
                    }, null);
                };
            }

            int lineSize = 3;
            songIcon = CreateIcon(Color.DarkGray, iconSize, iconSize, Services.ResultType.Song, lineSize);
            albumIcon = CreateIcon(Color.DarkGray, iconSize, iconSize, Services.ResultType.Album, lineSize - 1);
            artistIcon = CreateIcon(Color.Gray, iconSize, iconSize, Services.ResultType.Artist, lineSize);
            playlistIcon = CreateIcon(Color.DarkGray, iconSize, iconSize, Services.ResultType.Playlist, lineSize);
            commandIcon = CreateIcon(Color.DarkGray, iconSize, iconSize, Services.ResultType.Command, lineSize -1);

            Size = searchUIConfig.InitialSize;
            BackColor = searchUIConfig.BaseColor;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;

            bool minimized = WinApiHelpers.WinGetMinMax(mbHandle) == WinApiHelpers.WindowState.Minimized;

            if (minimized)
            {
                Location = new Point(
                    (Screen.PrimaryScreen.Bounds.Width - Size.Width) / 2,
                    Screen.PrimaryScreen.Bounds.Height / 4 - 50
                );
            }
            else
            {
                if (musicBeeControl != null)
                {
                    var mbBounds = musicBeeControl.Bounds;
                    Location = new Point(
                        mbBounds.Left + (mbBounds.Width - Size.Width) / 2,
                        mbBounds.Top + 100
                    );
                }
                else
                {
                     Location = new Point(
                        (Screen.PrimaryScreen.WorkingArea.Width - Size.Width) / 2,
                        (Screen.PrimaryScreen.WorkingArea.Height - Size.Height) / 2
                    );
                }
            }


            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent, // Let the form's background show through
                Padding = new Padding(8) // This creates the offset from the main rounded border
            };

            var searchBoxContainer = new Panel
            {
                Dock = DockStyle.Top,
                Padding = new Padding(12, 8, 12, 8), // Inner padding for the text box
                BackColor = Color.Transparent, // The border will be painted by the parent form
                Height = SEARCH_BOX_HEIGHT,
                BorderStyle = BorderStyle.None,
            };

            searchBox = new TextBox
            {
                Font = searchBoxFont,
                ForeColor = searchUIConfig.TextColor,
                BackColor = searchUIConfig.BaseColor,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                TextAlign = HorizontalAlignment.Left,
                ShortcutsEnabled = true,
                AutoCompleteMode = AutoCompleteMode.Suggest,
                AutoCompleteSource = AutoCompleteSource.CustomSource,
            };
            searchBox.TextChanged += SearchBox_TextChanged;
            searchBoxContainer.Controls.Add(searchBox);
            searchBox.TabStop = true;

            if (searchUIConfig.ShowPlaceholderText)
            {
                this.Shown += (s, e) =>
                {
                    if (searchBox.IsHandleCreated)
                    {
                        WinApiHelpers.SetCueBanner(searchBox.Handle, "Search or type a prefix a: l: s: p: >...");
                    }
                };
            }

            spacerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 8,
                BackColor = Color.Transparent
            };

            var baseColor = searchUIConfig.BaseColor;
            var highlightColor = searchUIConfig.ResultHighlightColor;
            var hoverColor = Color.FromArgb(
                255,
                baseColor.R + (highlightColor.R - baseColor.R) / 2,
                baseColor.G + (highlightColor.G - baseColor.G) / 2,
                baseColor.B + (highlightColor.B - baseColor.B) / 2
            );

            resultsListBox = new CustomResultList
            {
                Dock = DockStyle.Fill, // Use Fill to take up the remaining space
                BackColor = searchUIConfig.BaseColor,
                ForeColor = searchUIConfig.TextColor,
                HighlightColor = searchUIConfig.ResultHighlightColor,
                HoverColor = hoverColor,
                ItemHeight = (int)(searchUIConfig.ResultItemHeight * (CreateGraphics().DpiX / 96.0)),
                Visible = false,
                Height = 0,
                ResultFont = this.resultFont,
                ResultDetailFont = this.resultDetailFont,
                ImageService = this.imageService,
                ShowTypeIcons = searchUIConfig.ShowTypeIcons,
                Icons = new Dictionary<ResultType, Image>
                {
                    { ResultType.Song, songIcon },
                    { ResultType.Album, albumIcon },
                    { ResultType.Artist, artistIcon },
                    { ResultType.Playlist, playlistIcon },
                    { ResultType.Command, commandIcon },
                }
            };
            resultsListBox.Click += ResultsListBox_Click;
            resultsListBox.Scrolled += ResultsListBox_Scrolled;

            // Add controls to main panel in correct order for docking
            mainPanel.Controls.Add(resultsListBox); // Fills remaining space
            mainPanel.Controls.Add(spacerPanel);
            mainPanel.Controls.Add(searchBoxContainer); // At the top
            Controls.Add(mainPanel);

            searchBox.Focus(); // Set focus to searchBox initially

            InitializeLoadingIndicator();
        }

        private void InitializeHotkeys()
        {
            // Assign event handlers defined in SearchBar.EventHandlers.cs
            searchBox.KeyDown += HandleSearchBoxKeyDown;
            Deactivate += HandleFormDeactivate;
            KeyDown += HandleFormKeyDown;
            KeyPreview = true; // Need to set KeyPreview to true for Form to receive KeyDown events before controls
        }

        private void InitializeLoadingIndicator()
        {
            const int ROTATION_INTERVAL = 20;
            const int ROTATION_ANGLE = 20;
            var COLOR = Color.FromArgb((int)(255 * 0.3), searchUIConfig.TextColor);

            var searchContainer = searchBox.Parent;
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
                            searchContainer.ClientSize.Width - searchContainer.Padding.Right - loadingIndicator.Width - 2,
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
            spinTimer.Start();
        }


    }
}
