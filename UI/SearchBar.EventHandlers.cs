using MusicBeePlugin.Services;
using System;
using System.Linq;
using System.Windows.Forms;
using static MusicBeePlugin.Plugin;

namespace MusicBeePlugin.UI
{
    public partial class SearchBar
    {
        private void HandleSearchBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.P)
            {
                Close();
                musicBeeContext.Post(_ => Plugin.ShowConfigDialog(), null);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.H)
            {
                Close();
                musicBeeContext.Post(_ => Plugin.ShowConfigDialog(3), null);
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
                resultsListBox.SetSelectedIndex(preservedIndex);
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
                    if (resultsListBox.Items.All(i => i.Type == ResultType.Header)) return;

                    int count = resultsListBox.Items.Count;
                    int newIndex = resultsListBox.SelectedIndex;
                    if (newIndex == -1) newIndex = count - 1;

                    do
                    {
                        newIndex = (newIndex + 1) % count;
                    } while (resultsListBox.Items[newIndex].Type == ResultType.Header);
                    resultsListBox.SetSelectedIndex(newIndex, animateScroll: false);
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                {
                    if (resultsListBox.Items.All(i => i.Type == ResultType.Header)) return;

                    int count = resultsListBox.Items.Count;
                    int newIndex = resultsListBox.SelectedIndex;
                    if (newIndex == -1) newIndex = 0;

                    do
                    {
                        newIndex = newIndex > 0 ? newIndex - 1 : count - 1;
                    } while (resultsListBox.Items[newIndex].Type == ResultType.Header);
                    resultsListBox.SetSelectedIndex(newIndex, animateScroll: false);
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                {
                    int newIndex = 0;
                    // Skip header if it's the first item
                    if (resultsListBox.Items[newIndex].Type == ResultType.Header && resultsListBox.Items.Count > 1)
                    {
                        newIndex = 1;
                    }
                    resultsListBox.SetSelectedIndex(newIndex, animateScroll: true);
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                {
                    int newIndex = resultsListBox.Items.Count - 1;
                    // Skip header if it's the last item
                    if (resultsListBox.Items[newIndex].Type == ResultType.Header && resultsListBox.Items.Count > 1)
                    {
                        newIndex = resultsListBox.Items.Count - 2;
                    }
                    resultsListBox.SetSelectedIndex(newIndex, animateScroll: true);
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void HandleFormDeactivate(object sender, EventArgs e)
        {
            // Use BeginInvoke to marshal the Close call to the UI thread
            if (!IsDisposed && !Disposing)
            {
                try
                {
                    BeginInvoke((Action)Close);
                }
                catch (InvalidOperationException)
                {
                    // Ignore if the form is already being disposed or handle is already gone
                }
            }
        }

        private void HandleFormKeyDown(object sender, KeyEventArgs e)
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
                if (resultsListBox.SelectedItem is SearchResult selectedResult)
                {
                    HandleResultSelection(ArtistResult.FromSearchResult(selectedResult), e);
                }
            }
            else if (e.Alt && e.KeyCode == Keys.A)
            {
                 if (resultsListBox.SelectedItem is SearchResult selectedResult)
                {
                    HandleResultSelection(AlbumResult.FromSearchResult(selectedResult), e);
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

                HandleResultSelection(resultsListBox.SelectedItem, keyEventArgs);
            }
        }

        private void HandleSearchBoxEnter(KeyEventArgs e)
        {
            if (resultsListBox.Visible && resultsListBox.SelectedItem != null)
            {
                HandleResultSelection(resultsListBox.SelectedItem, e);
            }
            else
            {
                Close();
            }
        }

        private void HandleResultSelection(SearchResult selectedItem, KeyEventArgs e)
        {
            Close();
            // Fire-and-forget the async action on the MusicBee UI thread.
            // The async method will yield on await Task.Delay, preventing UI lockup.
            musicBeeContext.Post(_ => { _ = resultAcceptAction(searchBox.Text, selectedItem, e); }, null);
        }

        private async void ResultsListBox_Scrolled(object sender, EventArgs e)
        {
            if (searchUIConfig.ShowImages && !_isImageLoading)
            {
                _isImageLoading = true;
                try
                {
                    await LoadImagesForVisibleResults();
                }
                finally
                {
                    _isImageLoading = false;
                }
            }
        }
    }
}
