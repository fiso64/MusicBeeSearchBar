using MusicBeePlugin.Services;
using MusicBeePlugin.Utils;
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using static MusicBeePlugin.Plugin;

namespace MusicBeePlugin.UI
{
    public partial class SearchBar
    {
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((searchBox?.Focused == true || resultsListBox?.Focused == true) && TryRunResultShortcut(keyData))
                return true;
            if (keyData == (Keys.Shift | Keys.F10) && (searchBox?.Focused == true || resultsListBox?.Focused == true)
                && ShowSelectedResultContextMenu())
            {
                return true;
            }
            if (searchBox != null && searchBox.Focused
                && (keyData == Keys.Tab || keyData == (Keys.Shift | Keys.Tab)))
            {
                string oldText = searchBox.Text;
                int oldContentStart = SearchToolbar.ContentStart(oldText);
                int selectionStart = searchBox.SelectionStart;
                int selectionEnd = selectionStart + searchBox.SelectionLength;
                string prefix = SearchToolbar.NextPrefix(oldText, keyData == (Keys.Shift | Keys.Tab));
                string newText = SearchToolbar.ReplacePrefix(oldText, prefix);
                int newContentStart = SearchToolbar.ContentStart(newText);
                int newStart = Math.Min(newText.Length, newContentStart + Math.Max(0, selectionStart - oldContentStart));
                int newEnd = Math.Min(newText.Length, newContentStart + Math.Max(0, selectionEnd - oldContentStart));
                searchBox.Text = newText;
                searchBox.Select(newStart, newEnd - newStart);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool TryRunResultShortcut(Keys keyData)
        {
            if (!resultsListBox.Visible || !(resultsListBox.SelectedItem is SearchResult result)
                || result.Type == ResultType.Command || result.Type == ResultType.Header || result.Type == ResultType.ShowMore
                || keyData == Keys.Enter) return false;
            var key = new KeyEventArgs(keyData);
            if (actionService.GetConfiguredAction(result, key) == null) return false;
            HandleResultSelection(result, key);
            return true;
        }

        private void HandleSearchBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (TryRunResultShortcut(e.KeyData))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (!isEmbedded && e.KeyCode == Keys.Right && e.Modifiers == Keys.None
                && searchBox.Focused && searchBox.SelectionLength == 0
                && searchBox.SelectionStart == searchBox.TextLength && ShowSelectedResultContextMenu(alignRight: true))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Back && (e.Modifiers == Keys.Control || e.Modifiers == Keys.Alt))
            {
                DeletePreviousSearchWord();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (!isEmbedded && e.Control && e.KeyCode == Keys.D)
            {
                ToggleDetachedMode();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.P)
            {
                OpenSearchSettings();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.H)
            {
                if (!isEmbedded) Close();
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
                if (e.Modifiers == Keys.None) HandleSearchBoxEnter(e);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                if (isEmbedded) resultsPopup?.Hide();
                else Close();
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

        private void OpenSearchSettings()
        {
            if (!isEmbedded) Close();
            else resultsPopup?.Hide();
            musicBeeContext.Post(_ => Plugin.ShowConfigDialog(), null);
        }

        private void DeletePreviousSearchWord()
        {
            if (searchBox.SelectionLength > 0)
            {
                searchBox.SelectedText = string.Empty;
                return;
            }

            int end = searchBox.SelectionStart;
            int start = end;
            string text = searchBox.Text;
            while (start > 0 && char.IsWhiteSpace(text[start - 1])) start--;
            if (start > 0)
            {
                bool word = IsSearchWordCharacter(text[start - 1]);
                while (start > 0 && !char.IsWhiteSpace(text[start - 1])
                    && IsSearchWordCharacter(text[start - 1]) == word)
                    start--;
            }

            if (start == end) return;
            searchBox.Select(start, end - start);
            searchBox.SelectedText = string.Empty;
        }

        private static bool IsSearchWordCharacter(char value)
        {
            var category = char.GetUnicodeCategory(value);
            return char.IsLetterOrDigit(value) || value == '_'
                || category == UnicodeCategory.NonSpacingMark
                || category == UnicodeCategory.SpacingCombiningMark
                || category == UnicodeCategory.EnclosingMark;
        }

        private void HandleFormDeactivate(object sender, EventArgs e)
        {
            if (_closing || isDetached || _suppressDeactivate) return;

            int attachmentVersion = _attachmentVersion;

            PostToUi(() =>
            {
                // Detaching may have happened since deactivation queued this callback.
                if (!_closing && attachmentVersion == _attachmentVersion && !isDetached && !_suppressDeactivate)
                {
                    var foregroundWindow = WinApiHelpers.GetForegroundWindow();
                    if (foregroundWindow == Handle) return;

                    // An activation of our own backdrop is not an external focus loss.
                    if (overlay != null && !overlay.IsDisposed && overlay.IsHandleCreated
                        && foregroundWindow == overlay.Handle)
                    {
                        _seenMusicBeeForeground = true;
                        RestoreFocusFromOverlay(overlay);
                    }
                    else if (foregroundWindow != IntPtr.Zero
                        && WinApiHelpers.AreWindowsInSameProcess(foregroundWindow, Handle) == true)
                    {
                        _seenMusicBeeForeground = true;
                    }
                    else if (foregroundWindow != IntPtr.Zero
                        && WinApiHelpers.AreWindowsInSameProcess(foregroundWindow, Handle) == false)
                    {
                        Close();
                    }
                }
            });
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
                if (resultsListBox.SelectedItem is SearchResult selectedResult && selectedResult.Type != ResultType.ShowMore)
                {
                    var artist = ArtistResult.FromDetail(selectedResult);
                    if (artist != null) HandleResultSelection(artist, new KeyEventArgs(e.Modifiers & ~Keys.Alt));
                }
            }
            else if (e.Alt && e.KeyCode == Keys.A)
            {
                 if (resultsListBox.SelectedItem is SearchResult selectedResult && selectedResult.Type != ResultType.ShowMore)
                {
                    HandleResultSelection(AlbumResult.FromSearchResult(selectedResult), new KeyEventArgs(e.Modifiers & ~Keys.Alt));
                }
            }
        }

        private void ResultsListBox_Click(object sender, EventArgs e)
        {
            if (UsesListSelection && !(resultsListBox.SelectedItem is ShowMoreResult)) return;
            if (resultsListBox.SelectedItem != null)
            {
                var modifiers = Keys.None;
                if (Control.ModifierKeys.HasFlag(Keys.Control)) modifiers |= Keys.Control;
                if (Control.ModifierKeys.HasFlag(Keys.Shift)) modifiers |= Keys.Shift;
                var keyEventArgs = new KeyEventArgs(modifiers);

                HandleResultSelection(resultsListBox.SelectedItem, keyEventArgs);
            }
        }

        private bool ShowSelectedResultContextMenu(bool alignRight = false)
        {
            if (!resultsListBox.Visible || !(resultsListBox.SelectedItem is SearchResult result)
                || result.Type == ResultType.Header || result.Type == ResultType.ShowMore) return false;
            ShowResultContextMenu(result, resultsListBox.SelectedItemContextLocation(alignRight), alignRight);
            return true;
        }

        private void ShowResultContextMenu(SearchResult result, Point screenPosition)
        {
            ShowResultContextMenu(result, screenPosition, false);
        }

        private void ShowResultContextMenu(SearchResult result, Point screenPosition, bool alignRight)
        {
            Action selectedAction = null;
            void select(Action action) { selectedAction = action; }
            var menu = ResultContextMenu.Create(result, actionService.GetActionConfig(result.Type),
                quick => select(() => HandleResultSelection(result, new KeyEventArgs(Keys.None), () =>
                {
                    actionService.PerformAction(quick, result);
                    return Task.CompletedTask;
                })),
                () => select(() => HandleResultSelection(result, new KeyEventArgs(Keys.None), () =>
                {
                    actionService.PerformShufflePlay(result);
                    return Task.CompletedTask;
                })),
                modifiers => select(() => HandleResultSelection(result, new KeyEventArgs(modifiers))),
                () => select(() => HandleResultSelection(result, new KeyEventArgs(Keys.None))));
            if (!isEmbedded) ResultContextMenu.ApplyFloatingTheme(menu, theme);
            menu.Closed += (sender, args) =>
            {
                // Defer both cleanup and the action until WinForms finishes the menu click.
                PostToUi(() =>
                {
                    menu.Dispose();
                    selectedAction?.Invoke();
                });
            };
            if (alignRight)
                menu.Show(this, PointToClient(screenPosition), ToolStripDropDownDirection.BelowLeft);
            else
                menu.Show(this, PointToClient(screenPosition));
        }

        private void ResultsListBox_DoubleClick(object sender, EventArgs e)
        {
            if (!UsesListSelection || resultsListBox.SelectedItem == null) return;
            var modifiers = Control.ModifierKeys & (Keys.Control | Keys.Shift);
            HandleResultSelection(resultsListBox.SelectedItem, new KeyEventArgs(modifiers));
        }

        private void HandleResultsKeyDown(object sender, KeyEventArgs e)
        {
            if (!UsesListSelection) return;
            if (TryRunResultShortcut(e.KeyData))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            switch (e.KeyCode)
            {
                case Keys.Up: case Keys.Down: case Keys.PageUp: case Keys.PageDown: case Keys.Enter:
                    HandleSearchBoxKeyDown(sender, e);
                    break;
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
                if (!isEmbedded && !UsesListSelection) Close();
            }
        }

        private void HandleResultSelection(SearchResult selectedItem, KeyEventArgs e, Func<Task> customAction = null, bool keepDetachedOnTop = true)
        {
            if (selectedItem is ShowMoreResult showMore)
            {
                ExpandSearchResults(showMore.TargetType);
                return;
            }

            if (customAction == null && e != null)
            {
                var configuredAction = actionService.GetConfiguredAction(selectedItem, e);
                if (configuredAction == null && selectedItem.Type != ResultType.Command) return;
                if (configuredAction is MusicBeePlugin.Config.DoNothingActionData) return;
                if (configuredAction is MusicBeePlugin.Config.EditTagsActionData) keepDetachedOnTop = false;
            }

            string actionSearchText = searchBox.Text;
            if (isEmbedded && !compactPanel) Plugin.SaveExpandedSearchState(this);
            if (isEmbedded)
            {
                resultsPopup?.Hide();
            }
            else if (!isDetached)
            {
                Close();
            }
            else if (!IsDisposed && keepDetachedOnTop)
            {
                // Set this before dispatching so it cannot race with the action focusing MusicBee.
                TopMost = true;
            }

            // Fire-and-forget the async action on the MusicBee UI thread.
            musicBeeContext.Post(async _ =>
            {
                try
                {
                    if (customAction != null)
                    {
                        await customAction();
                    }
                    else
                    {
                        await actionService.RunAction(actionSearchText, selectedItem, e);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error executing action for {selectedItem.DisplayTitle}", ex);
                }
                finally
                {
                    // After the action, if still detached, revert TopMost and refocus the search bar.
                    if (!isEmbedded && isDetached && !IsDisposed)
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

        private void ResultsListBox_DetailLinkClicked(SearchResult item, ResultType type, int artistIndex)
        {
            SearchResult target = null;
            if (type == ResultType.Album && item is SongResult song)
                target = AlbumResult.FromSearchResult(song);
            else if (type == ResultType.Artist)
                target = ArtistResult.FromDetail(item, artistIndex);

            if (target != null)
                HandleResultSelection(target, new KeyEventArgs(Keys.None));
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
            if (isEmbedded && !compactPanel && !_restoringExpandedSearch) Plugin.SaveExpandedSearchState(this);
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
