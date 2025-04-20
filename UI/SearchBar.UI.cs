using MusicBeePlugin.Config;
using MusicBeePlugin.Utils;
using System;
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


            Panel mainPanel = new Panel { Dock = DockStyle.Fill, BackColor = searchUIConfig.BaseColor };

            Panel searchBoxContainer = new Panel
            {
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                BackColor = searchUIConfig.BaseColor,
                Height = 42,
                BorderStyle = BorderStyle.FixedSingle,
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
                AutoCompleteMode = AutoCompleteMode.Suggest, // this is only needed to fix ctrl+backspace
                AutoCompleteSource = AutoCompleteSource.CustomSource,
            };
            searchBox.TextChanged += SearchBox_TextChanged; // Event handler in SearchBar.Data.cs
            searchBoxContainer.Controls.Add(searchBox);
            searchBox.TabStop = true; // Ensure searchBox can be focused

            resultsListBox = new ListBox
            {
                Dock = DockStyle.Top, // Dock to top and adjust height dynamically
                BackColor = searchUIConfig.BaseColor,
                ForeColor = searchUIConfig.TextColor,
                BorderStyle = BorderStyle.None,
                Font = resultFont,
                ItemHeight = (int)(searchUIConfig.ResultItemHeight * (CreateGraphics().DpiX / 96.0)), // Scale based on DPI
                Visible = false,
                TabStop = false, // To prevent focusing on listbox with tab key.
                Height = 0 // Initially set height to 0
            };

            resultsListBox.DoubleBuffering(true);

            resultsListBox.DrawMode = DrawMode.OwnerDrawFixed;
            resultsListBox.DrawItem += ResultsListBox_DrawItem; // Event handler in SearchBar.Drawing.cs
            resultsListBox.Click += ResultsListBox_Click; // Event handler in SearchBar.EventHandlers.cs
            resultsListBox.TabStop = false; // To prevent focusing on listbox with tab key.

            resultsListBox.MouseWheel += (s, e) => LoadImagesForVisibleResults(); // Event handler in SearchBar.Data.cs

            mainPanel.Controls.Add(resultsListBox);
            mainPanel.Controls.Add(searchBoxContainer);
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

            Bitmap CreateLoadingSpinner(int size, Color color)
            {
                Bitmap bmp = new Bitmap(size, size);
                using (Graphics g = Graphics.FromImage(bmp))
                using (Pen pen = new Pen(color, 2))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawArc(pen, 2, 2, size - 4, size - 4, 0, 300);
                }
                return bmp;
            }

            void UpdateLoadingIndicatorPosition()
            {
                if (loadingIndicator != null && !loadingIndicator.IsDisposed && loadingIndicator.Visible)
                {
                    // Ensure searchBox handle is created before accessing properties like Right/Top/Height
                    if (searchBox != null && searchBox.IsHandleCreated)
                    {
                         loadingIndicator.Location = new Point(
                            searchBox.Right - loadingIndicator.Width - 10,
                            searchBox.Top + (searchBox.Height - loadingIndicator.Height) / 2
                        );
                    }
                }
            }

            loadingIndicator = new PictureBox
            {
                Size = new Size(24, 24),
                BackColor = Color.Transparent,
                Image = CreateLoadingSpinner(24, COLOR),
                Visible = true
            };

            searchBox.TextChanged += (s, e) => UpdateLoadingIndicatorPosition();
            searchBox.SizeChanged += (s, e) => UpdateLoadingIndicatorPosition();
            // Call initial position update after the form is shown or handle is created
             Shown += (s, e) => UpdateLoadingIndicatorPosition();


            Controls.Add(loadingIndicator);
            loadingIndicator.BringToFront();

            var spinTimer = new System.Windows.Forms.Timer { Interval = ROTATION_INTERVAL };
            float rotationAngle = 0;
            spinTimer.Tick += (s, e) => {
                if (loadingIndicator != null && !loadingIndicator.IsDisposed && loadingIndicator.Visible && loadingIndicator.Image != null)
                {
                    Bitmap currentImage = (Bitmap)loadingIndicator.Image;
                    Bitmap rotatedBmp = new Bitmap(currentImage.Width, currentImage.Height);
                    using (Graphics g = Graphics.FromImage(rotatedBmp))
                    using (Pen pen = new Pen(COLOR, 2)) // Use the defined COLOR
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TranslateTransform(rotatedBmp.Width / 2f, rotatedBmp.Height / 2f);
                        g.RotateTransform(rotationAngle);
                        g.TranslateTransform(-rotatedBmp.Width / 2f, -rotatedBmp.Height / 2f);
                        // Redraw the arc in the rotated context
                        g.DrawArc(pen, 2, 2, rotatedBmp.Width - 4, rotatedBmp.Height - 4, 0, 300);
                    }
                    loadingIndicator.Image = rotatedBmp; // Assign the new bitmap
                    currentImage.Dispose(); // Dispose the old bitmap

                    rotationAngle += ROTATION_ANGLE;
                    if (rotationAngle >= 360)
                    {
                        rotationAngle -= 360;
                    }
                }
                 else if (loadingIndicator != null && !loadingIndicator.IsDisposed && loadingIndicator.Visible && loadingIndicator.Image == null)
                {
                    // Recreate spinner if image somehow got disposed
                    loadingIndicator.Image = CreateLoadingSpinner(loadingIndicator.Width, COLOR);
                }
            };
            // Ensure the timer is disposed when the form closes
            FormClosed += (s, e) => spinTimer?.Dispose();
            spinTimer.Start();
        }

        private void UpdateResultsListHeight(int resultCount)
        {
            int newHeight = Math.Min(resultCount, searchUIConfig.MaxResultsVisible) * resultsListBox.ItemHeight;
            resultsListBox.Height = newHeight;
        }
    }
}
