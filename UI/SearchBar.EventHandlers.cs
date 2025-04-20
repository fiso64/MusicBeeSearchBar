﻿using MusicBeePlugin.Services;
using System;
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
                    int cur = resultsListBox.SelectedIndex;
                    resultsListBox.SelectedIndex = (resultsListBox.SelectedIndex + 1) % resultsListBox.Items.Count;
                    resultsListBox.Invalidate(resultsListBox.GetItemRectangle(cur));
                    LoadImagesForVisibleResults(); // Method in SearchBar.Data.cs
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                {
                    int cur = resultsListBox.SelectedIndex;
                    resultsListBox.SelectedIndex = cur > 0 ? cur - 1 : resultsListBox.Items.Count - 1;
                    resultsListBox.Invalidate(resultsListBox.GetItemRectangle(cur));
                    LoadImagesForVisibleResults(); // Method in SearchBar.Data.cs
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                {
                    resultsListBox.SelectedIndex = 0;
                    LoadImagesForVisibleResults(); // Method in SearchBar.Data.cs
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                {
                    resultsListBox.SelectedIndex = resultsListBox.Items.Count - 1;
                    LoadImagesForVisibleResults(); // Method in SearchBar.Data.cs
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
            Close();
            musicBeeContext.Post(_ => resultAcceptAction(searchBox.Text, selectedItem, e), null);
        }
    }
}
