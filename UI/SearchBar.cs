using MusicBeePlugin.Config;
using MusicBeePlugin.Services;
using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static MusicBeePlugin.Plugin;


namespace MusicBeePlugin.UI
{
    public class SearchBar : Form
    {
        private MusicBeeApiInterface mbApi;
        private Control musicBeeControl;
        private SynchronizationContext musicBeeContext;
        private SearchService searchService;

        private Func<string, SearchResult, KeyEventArgs, bool> resultAcceptAction;

        private TextBox searchBox;
        private ListBox resultsListBox;
        private Image songIcon;
        private Image albumIcon;
        private Image artistIcon;
        private OverlayForm overlay;

        private SearchUIConfig searchUIConfig;

        public SearchBar(
            Control musicBeeControl,
            SynchronizationContext musicBeeContext,
            MusicBeeApiInterface musicBeeApi,
            Func<string, SearchResult, KeyEventArgs, bool> resultAcceptAction,
            SearchUIConfig searchUIConfig,
            string defaultText = null
        )
        {
            this.musicBeeControl = musicBeeControl;
            this.musicBeeContext = musicBeeContext;
            mbApi = musicBeeApi;
            this.resultAcceptAction = resultAcceptAction;
            this.searchUIConfig = searchUIConfig;
            searchService = new SearchService(musicBeeApi, searchUIConfig.GroupResultsByType);
            searchService.LoadTracks();
            InitializeUI();
            InitializeHotkeys();

            if (!string.IsNullOrEmpty(defaultText))
            {
                searchBox.Text = defaultText;
                searchBox.Select(searchBox.Text.Length, 0);
            }
        }

        private void InitializeUI()
        {
            var mbHandle = mbApi.MB_GetWindowHandle();

            if (searchUIConfig.OverlayOpacity > 0 && WinApiHelpers.IsWindowFocused(mbHandle))
            {
                musicBeeContext.Post(_ =>
                {
                    overlay = new OverlayForm(musicBeeControl, searchUIConfig.OverlayOpacity);
                    overlay.Show();
                }, null);

                FormClosed += (s, e) =>
                {
                    musicBeeContext.Post(_ =>
                    {
                        if (overlay != null && !overlay.IsDisposed)
                            overlay.Close();
                    }, null);
                };
            }

            songIcon = CreateIcon(Color.LightGray, 16, 16, ResultType.Song, 2);
            albumIcon = CreateIcon(Color.DarkGray, 16, 16, ResultType.Album, 2);
            artistIcon = CreateIcon(Color.Gray, 16, 16, ResultType.Artist, 2);

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
                var mbBounds = musicBeeControl.Bounds;
                Location = new Point(
                    mbBounds.Left + (mbBounds.Width - Size.Width) / 2,
                    mbBounds.Top + 100
                );
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
                Font = new Font("Arial", 14),
                ForeColor = searchUIConfig.TextColor,
                BackColor = searchUIConfig.BaseColor,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                TextAlign = HorizontalAlignment.Left,
                ShortcutsEnabled = true,
                AutoCompleteMode = AutoCompleteMode.Suggest, // this is only needed to fix ctrl+backspace
                AutoCompleteSource = AutoCompleteSource.CustomSource,
            };
            searchBox.TextChanged += SearchBox_TextChanged;
            searchBoxContainer.Controls.Add(searchBox);
            searchBox.TabStop = true; // Ensure searchBox can be focused

            resultsListBox = new ListBox
            {
                Dock = DockStyle.Top, // Dock to top and adjust height dynamically
                BackColor = searchUIConfig.BaseColor,
                ForeColor = searchUIConfig.TextColor,
                BorderStyle = BorderStyle.None,
                Font = new Font("Arial", 12),
                ItemHeight = 42,
                Visible = false,
                TabStop = false, // To prevent focusing on listbox with tab key.
                Height = 0 // Initially set height to 0
            };

            resultsListBox.DrawMode = DrawMode.OwnerDrawFixed;
            resultsListBox.DrawItem += ResultsListBox_DrawItem;
            resultsListBox.Click += ResultsListBox_Click;
            resultsListBox.TabStop = false; // To prevent focusing on listbox with tab key.

            mainPanel.Controls.Add(resultsListBox);
            mainPanel.Controls.Add(searchBoxContainer);
            Controls.Add(mainPanel);

            searchBox.Focus(); // Set focus to searchBox initially
        }

        private int preservedIndex = -1;
        private void InitializeHotkeys()
        {
            searchBox.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.P)
                {
                    Close();
                    musicBeeContext.Post(_ => Plugin.ShowConfigDialog(), null);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.ControlKey) 
                {
                    // temporary hack to fix control+enter bug that makes the selected index jump to 0
                    preservedIndex = resultsListBox.SelectedIndex;
                }
                else if (e.KeyCode == Keys.Enter && e.Control && preservedIndex != -1)
                {
                    resultsListBox.SelectedIndex = preservedIndex;
                    HandleSearchBoxEnter(e);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    preservedIndex = -1;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    HandleSearchBoxEnter(e);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    Close();
                }
                else if (e.KeyCode == Keys.Down)
                {
                    if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                    {
                        if (resultsListBox.SelectedIndex < resultsListBox.Items.Count - 1)
                        {
                            resultsListBox.SelectedIndex++;
                            resultsListBox.Invalidate();
                        }
                    }
                    e.Handled = true; // Prevent further processing of Down key, keep focus on searchBox
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Up)
                {
                    if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                    {
                        if (resultsListBox.SelectedIndex > 0)
                        {
                            resultsListBox.SelectedIndex--;
                            resultsListBox.Invalidate();
                        }
                    }
                    e.Handled = true; // Prevent further processing of Up key, keep focus on searchBox
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.PageUp)
                {
                    if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                    {
                        resultsListBox.SelectedIndex = 0;
                        resultsListBox.Invalidate();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.PageDown)
                {
                    if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                    {
                        resultsListBox.SelectedIndex = resultsListBox.Items.Count - 1;
                        resultsListBox.Invalidate();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            Deactivate += (s, e) => Close();

            KeyDown += (s, e) =>
            {
                if (e.Alt && e.KeyCode == Keys.D)
                {
                    searchBox.Focus();
                    searchBox.SelectAll();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.Alt && e.KeyCode == Keys.R)
                {
                    var item = (SearchResult)resultsListBox.SelectedItem;
                    item.Type = ResultType.Artist;
                    HandleResultSelection(item, e);
                }
                else if (e.Alt && e.KeyCode == Keys.A)
                {
                    var item = (SearchResult)resultsListBox.SelectedItem;
                    item.Type = ResultType.Album;
                    HandleResultSelection(item, e);
                }
            };
            KeyPreview = true; // Need to set KeyPreview to true for Form to receive KeyDown events before controls
        }

        private Bitmap CreateIcon(Color color, int width, int height, ResultType type, int lineWidth = 1)
        {
            Bitmap icon = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(icon))
            using (Pen pen = new Pen(color, lineWidth))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                switch (type)
                {
                    case ResultType.Album:
                        // Square with circle inside
                        g.DrawRectangle(pen, 1, 1, width - 2, height - 2);
                        g.DrawEllipse(pen, 4, 4, width - 8, height - 8);
                        break;
                    case ResultType.Song:
                        // Triangle pointing right
                        Point[] points = {
                            new Point(2, 2),
                            new Point(width - 2, height / 2),
                            new Point(2, height - 2)
                        };
                        g.DrawPolygon(pen, points);
                        break;
                    case ResultType.Artist:
                        // Head (small circle)
                        int headSize = width / 3;
                        g.DrawEllipse(pen, 
                            (width - headSize) / 2,  // center horizontally
                            1,                       // near top
                            headSize, 
                            headSize
                        );
                        
                        // Body (oval)
                        int bodyWidth = width - 4;
                        int bodyHeight = height - headSize - 4;
                        g.DrawEllipse(pen,
                            2,                          // left edge
                            headSize + 2,               // below head
                            bodyWidth,
                            bodyHeight
                        );
                        break;
                }
            }
            return icon;
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            string query = searchBox.Text.Trim();
            var filter = ResultType.All;

            if (query.StartsWith("a:", StringComparison.OrdinalIgnoreCase))
            {
                filter &= ResultType.Artist;
                query = query.Substring(2).TrimStart();
            }
            else if (query.StartsWith("l:", StringComparison.OrdinalIgnoreCase))
            {
                filter &= ResultType.Album;
                query = query.Substring(2).TrimStart();
            }
            else if (query.StartsWith("t:", StringComparison.OrdinalIgnoreCase) || query.StartsWith("s:", StringComparison.OrdinalIgnoreCase))
            {
                filter &= ResultType.Song;
                query = query.Substring(2).TrimStart();
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                resultsListBox.Visible = false;
                resultsListBox.Items.Clear();
                UpdateResultsListHeight(0);
                Height = 42;
                return;
            }

            var searchResults = searchService.Search(query, filter);
            UpdateResultsList(searchResults);
        }

        private void UpdateResultsList(List<SearchResult> searchResults)
        {
            resultsListBox.Items.Clear();
            if (searchResults.Count > 0)
            {
                foreach (var result in searchResults)
                {
                    resultsListBox.Items.Add(result);
                }
                resultsListBox.Visible = true;
                resultsListBox.SelectedIndex = 0; // Select the first item by default when results appear
                UpdateResultsListHeight(searchResults.Count);
                Height = 42 + resultsListBox.Height;
            }
            else
            {
                resultsListBox.Visible = false;
                UpdateResultsListHeight(0);
                Height = 42;
            }
        }

        private void UpdateResultsListHeight(int resultCount)
        {
            int newHeight = Math.Min(resultCount, searchUIConfig.MaxResultsVisible) * resultsListBox.ItemHeight;
            resultsListBox.Height = newHeight;
        }


        private void ResultsListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= resultsListBox.Items.Count) return;

            SearchResult resultItem = (SearchResult)resultsListBox.Items[e.Index];
            Graphics g = e.Graphics;
            Rectangle bounds = e.Bounds;

            Color backgroundColor = searchUIConfig.BaseColor;
            if (e.State.HasFlag(DrawItemState.Selected) || resultsListBox.SelectedIndex == e.Index && resultsListBox.Visible && resultsListBox.Items.Count > 0)
            {
                backgroundColor = searchUIConfig.ResultHighlightColor;
            }

            using (SolidBrush backgroundBrush = new SolidBrush(backgroundColor))
            {
                e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
            }


            Color textColor = resultsListBox.ForeColor;
            using (SolidBrush textBrush = new SolidBrush(textColor))
            {
                int iconWidth = 16;
                int iconPaddingRight = 5;
                int textStartX = bounds.X + iconWidth + iconPaddingRight + 5;
                int offsetY = 4;

                Font titleFont = new Font(resultsListBox.Font.FontFamily, 12, FontStyle.Regular);
                Font detailFont = new Font(resultsListBox.Font.FontFamily, 10, FontStyle.Regular);
                Color detailColor = Color.Gray;


                Image currentIcon = null;
                if (resultItem.Type == ResultType.Song)
                    currentIcon = songIcon;
                else if (resultItem.Type == ResultType.Album)
                    currentIcon = albumIcon;
                else if (resultItem.Type == ResultType.Artist)
                    currentIcon = artistIcon;

                if (currentIcon != null)
                {
                    g.DrawImage(currentIcon, bounds.X + 5, bounds.Y + (bounds.Height - currentIcon.Height) / 2);
                }


                if (resultItem.Type == ResultType.Song)
                {
                    g.DrawString(resultItem.Title, titleFont, textBrush, textStartX, bounds.Y + offsetY);
                    g.DrawString(resultItem.Detail, detailFont, new SolidBrush(detailColor), textStartX, bounds.Y + offsetY + titleFont.GetHeight() + 2);
                }
                else if (resultItem.Type == ResultType.Album)
                {
                    g.DrawString(resultItem.Title, titleFont, textBrush, textStartX, bounds.Y + offsetY);
                    g.DrawString(resultItem.Detail, detailFont, new SolidBrush(detailColor), textStartX, bounds.Y + offsetY + titleFont.GetHeight() + 2);
                }
                else if (resultItem.Type == ResultType.Artist)
                {
                    g.DrawString(resultItem.Title, titleFont, textBrush, textStartX, bounds.Y + offsetY + 5);
                }
            }
        }

        private void ResultsListBox_Click(object sender, EventArgs e)
        {
            if (resultsListBox.SelectedItem != null)
            {
                var modifiers = Keys.None;
                if (Control.ModifierKeys.HasFlag(Keys.Control)) modifiers |= Keys.Control;
                if (Control.ModifierKeys.HasFlag(Keys.Shift)) modifiers |= Keys.Shift;
                var keyEventArgs = new KeyEventArgs(modifiers);
                
                HandleResultSelection((SearchResult)resultsListBox.SelectedItem, keyEventArgs);
            }
        }

        private void HandleSearchBoxEnter(KeyEventArgs e)
        {
            if (resultsListBox.Visible && resultsListBox.SelectedIndex != -1) // Use selected index if list is visible
            {
                HandleResultSelection((SearchResult)resultsListBox.SelectedItem, e);
            }
            else if (resultsListBox.Visible && resultsListBox.Items.Count > 0) // If list is visible but no selection (shouldn't happen but for safety), take first item
            {
                HandleResultSelection((SearchResult)resultsListBox.Items[0], e);
            }
            else
            {
                Close();
            }
        }

        private void HandleResultSelection(SearchResult selectedItem, KeyEventArgs e)
        {
            //musicBeeContext.Post(_ =>
            //{
            //    if (resultAcceptAction(selectedItem, e))
            //    {
            //        BeginInvoke((Action)Close);
            //    }
            //}, null);
            Close();
            musicBeeContext.Post(_ => resultAcceptAction(searchBox.Text, selectedItem, e), null);
        }
    }

    public class OverlayForm : Form
    {
        public OverlayForm(Control targetControl, double opacity)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            // this.TopMost = true;
            BackColor = Color.Black;
            Opacity = opacity;
            ShowInTaskbar = false;
            Size = targetControl.Size;
            Location = targetControl.PointToScreen(Point.Empty);
        }
    }
}

