using MusicBeePlugin.Services;
using MusicBeePlugin.Utils;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using static MusicBeePlugin.Plugin;

namespace MusicBeePlugin.UI
{
    public partial class SearchBar
    {
        private void HandleSearchBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.D)
            {
                ToggleDetachedMode();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.P)
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
            else if (e.Control && e.KeyCode == Keys.S)
            {
                if (resultsListBox.SelectedItem is SearchResult selectedResult && selectedResult.Type != ResultType.Command && selectedResult.Type != ResultType.Header)
                {
                    HandleResultSelection(selectedResult, e, () =>
                    {
                        actionService.PerformShufflePlay(selectedResult);
                        return Task.CompletedTask;
                    });
                }
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
            else if (e.KeyCode == Keys.F5)
            {
                isLoading = true;
                loadingIndicator.Visible = true;
                LoadTracksAsync();
                e.Handled = true;
                e.SuppressKeyPress = true;
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
            if (_suppressDeactivate) return;

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

        private void HandleResultSelection(SearchResult selectedItem, KeyEventArgs e, Func<Task> customAction = null)
        {
            if (!isDetached)
            {
                Close();
            }

            // Fire-and-forget the async action on the MusicBee UI thread.
            musicBeeContext.Post(async _ =>
            {
                try
                {
                    // If detached, temporarily make the search bar topmost to keep it visible during the action.
                    if (isDetached && !IsDisposed)
                    {
                        BeginInvoke((Action)(() => { if (!IsDisposed) TopMost = true; }));
                    }

                    if (customAction != null)
                    {
                        await customAction();
                    }
                    else
                    {
                        await actionService.RunAction(searchBox.Text, selectedItem, e);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error executing action for {selectedItem.DisplayTitle}", ex);
                }
                finally
                {
                    // After the action, if still detached, revert TopMost and refocus the search bar.
                    if (isDetached && !IsDisposed)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            if (!IsDisposed && isDetached)
                            {
                                TopMost = false;
                                Activate(); // Steal focus back.
                            }
                        }));
                    }
                }
            }, null);
        }

        private void ResultsListBox_ActionButtonClicked(object sender, ResultActionEventArgs e)
        {
            // customAction is always supplied here, so the KeyEventArgs is never actually read.
            HandleResultSelection(e.Result, null, () =>
            {
                actionService.PerformAction(e.ActionType, e.Result);
                return Task.CompletedTask;
            });
        }

        private void DragPanel_MouseDown(object sender, MouseEventArgs e)
        {
            isDragging = true;
            dragCursorPoint = Cursor.Position;
            dragFormPoint = Location;
        }

        private void DragPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                Location = Point.Add(dragFormPoint, new Size(dif));
            }
        }

        private void DragPanel_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }

        private void BeginWidthResize(bool fromLeft, Control grip)
        {
            _isResizingWidth = true;
            _resizeFromLeft = fromLeft;
            _resizeStartCursorPos = Cursor.Position;
            _resizeStartWidth = Width;
            _resizeStartLeft = Left;
            _resizeMaxWidth = Screen.FromControl(this).WorkingArea.Width;

            // Capture the mouse so MouseUp still reaches this grip even once the cursor
            // slips past its (few-pixels-wide) bounds or outside the window entirely.
            grip.Capture = true;

            // Drop the rounded-corner clip region for the duration of the drag (see
            // OnResize) and switch the result list to cheap rendering - both get restored
            // once in EndWidthResize.
            Region = null;
            resultsListBox.IsInteractiveResizing = true;
        }

        private void ApplyResizeFromCursor()
        {
            if (!_isResizingWidth) return;

            int deltaX = Cursor.Position.X - _resizeStartCursorPos.X;
            int minWidth = (int)(MIN_WIDTH_UNSCALED * dpiScale);

            int newWidth = _resizeFromLeft ? _resizeStartWidth - deltaX : _resizeStartWidth + deltaX;
            newWidth = Math.Max(minWidth, Math.Min(_resizeMaxWidth, newWidth));

            // Nothing to do once clamped at the min/max - skip the SetBounds/Update below
            // so dragging past either edge doesn't keep forcing pointless synchronous paints.
            if (newWidth == Width) return;

            int newLeft = _resizeFromLeft ? _resizeStartLeft - (newWidth - _resizeStartWidth) : Left;

            // A single SetBounds call is one native resize + one layout pass, instead of
            // the two of each that separate Left/Width assignments used to cause.
            SetBounds(newLeft, Top, newWidth, Height);

            // WM_PAINT sits at the bottom of the message queue's priority and is only
            // generated once the queue has no other pending input - so as long as MouseMove
            // keeps arriving, the window keeps resizing (that part is synchronous) but never
            // gets to actually repaint until the cursor stops and the queue drains. That's
            // the "only updates once the mouse stops" symptom. Update() forces the already-
            // invalidated region to flush right now instead of waiting for that. Any
            // MouseMove that arrives while this is running is coalesced by Windows into a
            // single fresh one waiting for us when we return to the message loop, so this
            // doesn't add a backlog - it just makes each processed move visible immediately.
            Update();
            resultsListBox.Update();
        }

        private void ResizeGrip_MouseMove(object sender, MouseEventArgs e) => ApplyResizeFromCursor();

        private void ResizeGrip_MouseUp(object sender, MouseEventArgs e)
        {
            if (sender is Control grip) EndWidthResize(grip);
        }

        // Also wired to MouseCaptureChanged: capture can be revoked by Windows without a
        // matching MouseUp ever reaching us (Alt-Tab mid-drag, a modal dialog appearing, the
        // workstation locking, ...). Without this guard, _isResizingWidth would stay stuck
        // true. Guarded to be a no-op if the drag already ended normally via MouseUp.
        private void ResizeGrip_MouseCaptureChanged(object sender, EventArgs e)
        {
            if (sender is Control grip) EndWidthResize(grip);
        }

        private void EndWidthResize(Control grip)
        {
            if (!_isResizingWidth) return;

            // Apply the exact final cursor position once more, in case the last MouseMove
            // landed slightly before this.
            ApplyResizeFromCursor();
            _isResizingWidth = false;

            // Releasing capture here (when it isn't already released) re-enters this method
            // via MouseCaptureChanged, but _isResizingWidth is already false by that point,
            // so the guard above makes it a no-op.
            grip.Capture = false;

            resultsListBox.IsInteractiveResizing = false;
            resultsListBox.Invalidate();
            if (this.ClientRectangle.Width > 0 && this.ClientRectangle.Height > 0)
            {
                using (var path = GetRoundedRectPath(this.ClientRectangle, CORNER_RADIUS))
                {
                    this.Region = new Region(path);
                }
            }

            PersistWindowWidth();
        }

        private void PersistWindowWidth()
        {
            if (searchUIConfig.InitialSize.Width == Width) return;

            searchUIConfig.InitialSize = new Size(Width, searchUIConfig.InitialSize.Height);
            SaveConfig();
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
