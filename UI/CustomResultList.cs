using MusicBeePlugin.Services;
using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace MusicBeePlugin.UI
{
    public class CustomResultList : Control
    {
        // A reasonable default pixel height for one "line" of scrolling, inspired by browser standards.
        private const int ScrollLineHeight = 18;
        private const int SCROLLBAR_WIDTH = 8;
        private const int ARTWORK_CORNER_RADIUS = 8;
        public const int HEADER_TOP_PADDING = 8;

        private List<SearchResult> _items = new List<SearchResult>();
        private int _selectedIndex = -1;
        private int _hoveredIndex = -1;
        private int _scrollTop = 0;
        private bool _isUpdating = false;
        private List<int> _itemYPositions = new List<int>();

        // Scrollbar state
        private Rectangle _scrollTrack;
        private Rectangle _scrollThumb;
        private bool _isThumbVisible = false;
        private bool _isDraggingThumb = false;
        private int _dragStartY;
        private int _dragStartScrollTop;

        public event EventHandler Scrolled;

        // Styling & Resources
        public int ItemHeight { get; set; } = 56;
        public int HeaderHeight { get; set; } = 24;
        public Color HighlightColor { get; set; }
        public Color HoverColor { get; set; }
        public Font ResultFont { get; set; }
        public Font ResultDetailFont { get; set; }
        public ImageService ImageService { get; set; }
        public Dictionary<ResultType, Image> Icons { get; set; }

        public List<SearchResult> Items
        {
            get => _items;
            set
            {
                _items = value ?? new List<SearchResult>();

                _itemYPositions.Clear();
                if (_items.Count > 0)
                {
                    int currentY = 0;
                    for (int i = 0; i < _items.Count; i++)
                    {
                        _itemYPositions.Add(currentY);
                        currentY += GetItemHeight(i);
                    }
                }

                ScrollTop = 0;

                int firstSelectable = -1;
                if (_items.Count > 0)
                {
                    firstSelectable = _items.FindIndex(i => i.Type != ResultType.Header);
                }
                SelectedIndex = firstSelectable;

                UpdateScrollbar();
                Invalidate();
            }
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (value >= _items.Count) value = _items.Count - 1;
                if (value < 0) value = -1;

                if (_selectedIndex != value)
                {
                    int oldIndex = _selectedIndex;
                    _selectedIndex = value;

                    if (oldIndex != -1) InvalidateItem(oldIndex);
                    if (_selectedIndex != -1) InvalidateItem(_selectedIndex);
                    
                    EnsureVisible(_selectedIndex);
                }
            }
        }

        public SearchResult SelectedItem => (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex] : null;

        public int ScrollTop
        {
            get => _scrollTop;
            set
            {
                int contentHeight = 0;
                if (_items.Any() && _itemYPositions.Any())
                {
                    contentHeight = _itemYPositions.Last() + GetItemHeight(_items.Count - 1);
                }

                int maxScrollTop = Math.Max(0, contentHeight - Height);
                var newValue = Math.Max(0, Math.Min(value, maxScrollTop));

                if (_scrollTop != newValue)
                {
                    _scrollTop = newValue;
                    UpdateScrollbar();
                    Invalidate();
                    Scrolled?.Invoke(this, EventArgs.Empty);

                    // Re-evaluate hover state since the items under the cursor have changed
                    var relativeMousePos = PointToClient(Cursor.Position);
                    if (ClientRectangle.Contains(relativeMousePos))
                    {
                        OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, relativeMousePos.X, relativeMousePos.Y, 0));
                    }
                    else if (_hoveredIndex != -1) // Mouse is outside, so clear hover
                    {
                        var oldIndex = _hoveredIndex;
                        _hoveredIndex = -1;
                        InvalidateItem(oldIndex);
                    }
                }
            }
        }

        public int FirstVisibleIndex
        {
            get
            {
                if (_items.Count == 0) return 0;
                // Use a binary search to find the last item whose top is at or before the current scroll position
                int low = 0;
                int high = _itemYPositions.Count - 1;
                int result = 0;
                while (low <= high)
                {
                    int mid = low + (high - low) / 2;
                    if (_itemYPositions[mid] <= _scrollTop)
                    {
                        result = mid;
                        low = mid + 1;
                    }
                    else
                    {
                        high = mid - 1;
                    }
                }
                return result;
            }
        }

        public CustomResultList()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        private int GetItemHeight(int index)
        {
            if (index < 0 || index >= _items.Count) return ItemHeight;
            if (_items[index].Type == ResultType.Header)
            {
                // Add extra spacing before headers that are not the very first item.
                return HeaderHeight + ((index > 0) ? HEADER_TOP_PADDING : 0);
            }
            return ItemHeight;
        }

        private int GetIndexFromY(int y)
        {
            if (_items.Count == 0 || _itemYPositions.Count == 0) return -1;

            int absoluteY = y + _scrollTop;

            // Binary search to find the item. This is more efficient than a linear scan.
            int low = 0;
            int high = _itemYPositions.Count - 1;
            int result = -1;

            // Find the last item whose top is at or before the absoluteY
            while (low <= high)
            {
                int mid = low + (high - low) / 2;
                if (_itemYPositions[mid] <= absoluteY)
                {
                    result = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            // After the loop, 'result' is the index of the item that contains or is before the y-coordinate.
            // Now, check if the coordinate is actually within this item's bounds.
            if (result != -1 && absoluteY < _itemYPositions[result] + GetItemHeight(result))
            {
                return result;
            }

            return -1; // Not over any item
        }

        public void BeginUpdate() => _isUpdating = true;
        public void EndUpdate()
        {
            _isUpdating = false;
            UpdateScrollbar();
            Invalidate();
        }

        private void InvalidateItem(int index)
        {
            if (index >= 0 && index < _itemYPositions.Count)
            {
                int itemY = _itemYPositions[index] - _scrollTop;
                var rect = new Rectangle(0, itemY, Width, GetItemHeight(index));
                
                if (rect.Bottom >= 0 && rect.Top <= Height)
                {
                    Invalidate(rect);
                }
            }
        }

        private void EnsureVisible(int index)
        {
            if (index < 0 || index >= _items.Count || _itemYPositions.Count <= index) return;

            int itemTop = _itemYPositions[index];
            int itemBottom = itemTop + GetItemHeight(index);

            if (itemTop < _scrollTop)
            {
                // Item is above the view, scroll up to show it at the top.
                ScrollTop = itemTop;
            }
            else if (itemBottom > _scrollTop + Height)
            {
                // Item is below the view, scroll down to make its bottom flush with the view's bottom.
                ScrollTop = itemBottom - Height;
            }
        }

        private void UpdateScrollbar()
        {
            if (_items.Count == 0 || _itemYPositions.Count == 0)
            {
                _isThumbVisible = false;
                return;
            }

            int contentHeight = _itemYPositions.Last() + GetItemHeight(_items.Count - 1);

            if (contentHeight <= Height)
            {
                _isThumbVisible = false;
                return;
            }

            _isThumbVisible = true;
            _scrollTrack = new Rectangle(Width - SCROLLBAR_WIDTH, 0, SCROLLBAR_WIDTH, Height);

            float thumbHeight = Math.Max(20, Height * (Height / (float)contentHeight));
            float scrollableHeight = contentHeight - Height;
            
            float scrollRatio = (scrollableHeight > 0) ? (_scrollTop / scrollableHeight) : 0;
            float thumbY = (Height - thumbHeight) * scrollRatio;

            _scrollThumb = new Rectangle(
                _scrollTrack.X + 1,
                (int)thumbY,
                SCROLLBAR_WIDTH - 2,
                (int)thumbHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Debug.WriteLine($"[CustomResultList] OnPaint: Firing. ClipRectangle={e.ClipRectangle}. IsUpdating={_isUpdating}. Items={_items.Count}. Height={Height}.");

            if (_isUpdating || _items.Count == 0)
            {
                if (_isUpdating) Debug.WriteLine("[CustomResultList] OnPaint: Exiting because IsUpdating=true.");
                if (_items.Count == 0) Debug.WriteLine("[CustomResultList] OnPaint: Exiting because Items.Count=0.");
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (_items.Count == 0 || _itemYPositions.Count == 0) return;

            int startIndex = FirstVisibleIndex;

            for (int i = startIndex; i < _items.Count; i++)
            {
                int itemY = _itemYPositions[i] - _scrollTop;
                // Optimization: stop drawing if we're past the bottom of the control
                if (itemY >= Height) break;

                var item = _items[i];
                var bounds = new Rectangle(0, itemY, Width, GetItemHeight(i));

                // Only draw if the item is actually within the clip rectangle
                if (e.ClipRectangle.IntersectsWith(bounds))
                {
                    DrawItem(e.Graphics, item, bounds, i, i == _selectedIndex);
                }
            }
            
            if (_isThumbVisible)
            {
                DrawScrollbar(e.Graphics);
            }
        }

        private void DrawItem(Graphics g, SearchResult resultItem, Rectangle bounds, int index, bool isSelected)
        {
            if (resultItem.Type == ResultType.Header)
            {
                using (var backgroundBrush = new SolidBrush(this.BackColor))
                {
                    g.FillRectangle(backgroundBrush, bounds);
                }

                using (var headerFont = new Font(ResultFont.FontFamily, ResultFont.Size, FontStyle.Italic))
                {
                    var headerColor = Color.Gray;
                    TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.NoPrefix;

                    int currentTopPadding = (index > 0) ? HEADER_TOP_PADDING : 0;

                    Rectangle textRenderBounds = new Rectangle(
                        bounds.X + 10,
                        bounds.Y + currentTopPadding, // Position text after the padding
                        bounds.Width - 20,
                        HeaderHeight // The text area has the original header height
                    );

                    TextRenderer.DrawText(g, resultItem.DisplayTitle.ToUpper(), headerFont, textRenderBounds, headerColor, flags | TextFormatFlags.VerticalCenter);
                }
                return;
            }

            const int HORIZONTAL_PADDING = 10;
            const int IMAGE_VERTICAL_MARGIN = 6; // Margin from top and bottom for the image
            const int IMAGE_TO_TEXT_SPACING = 10;

            int itemWidth = bounds.Width;
            if (_isThumbVisible)
            {
                itemWidth -= SCROLLBAR_WIDTH;
            }

            // Draw background for the whole item, which is visible if the highlight is smaller
            using (var backgroundBrush = new SolidBrush(this.BackColor))
            {
                g.FillRectangle(backgroundBrush, bounds);
            }

            bool isHovered = index == _hoveredIndex && !_isDraggingThumb;
            Color? highlightBrushColor = null;
            if (isSelected)
            {
                highlightBrushColor = this.HighlightColor;
            }
            else if (isHovered)
            {
                highlightBrushColor = this.HoverColor;
            }

            if (highlightBrushColor.HasValue)
            {
                const int HIGHLIGHT_MARGIN = 2;
                var highlightBounds = new Rectangle(
                    bounds.X + HIGHLIGHT_MARGIN,
                    bounds.Y + HIGHLIGHT_MARGIN,
                    itemWidth - (HIGHLIGHT_MARGIN * 2),
                    bounds.Height - (HIGHLIGHT_MARGIN * 2)
                );
                
                if (highlightBounds.Width > 0 && highlightBounds.Height > 0)
                {
                    using (var path = GetRoundedRectPath(highlightBounds, 8))
                    using (var highlightBrush = new SolidBrush(highlightBrushColor.Value))
                    {
                        g.FillPath(highlightBrush, path);
                    }
                }
            }

            // 1. Define the total available content height for images/text.
            int availableHeight = bounds.Height - (IMAGE_VERTICAL_MARGIN * 2);
            if (availableHeight <= 0) return;

            // 2. Define the image area on the left, respecting horizontal padding.
            var imageArea = new Rectangle(
                bounds.X + HORIZONTAL_PADDING,
                bounds.Y + IMAGE_VERTICAL_MARGIN,
                availableHeight, // Make it a square
                availableHeight
            );

            // 3. Determine which image to use.
            int artworkSize = availableHeight;
            int iconSize = (int)(availableHeight * 0.6);

            Image displayImage = null;
            bool isArtwork = false;
            if (ImageService != null) { displayImage = ImageService.GetCachedImage(resultItem); }

            if (displayImage != null)
            {
                isArtwork = true;
            }
            else if (Icons != null && Icons.TryGetValue(resultItem.Type, out var icon))
            {
                displayImage = icon;
            }

            // 4. Draw the image, centered within its dedicated area.
            if (displayImage != null)
            {
                int effectiveSize = isArtwork ? artworkSize : iconSize;
                int imageX = imageArea.X + (imageArea.Width - effectiveSize) / 2;
                int imageY = imageArea.Y + (imageArea.Height - effectiveSize) / 2;
                var imageBounds = new Rectangle(imageX, imageY, effectiveSize, effectiveSize);

                // Apply rounded corners to square artwork (albums, songs)
                if (isArtwork && ARTWORK_CORNER_RADIUS > 0 && resultItem.Type != ResultType.Artist)
                {
                    using (var path = GetRoundedRectPath(imageBounds, ARTWORK_CORNER_RADIUS))
                    using (var brush = new TextureBrush(displayImage, WrapMode.Clamp))
                    {
                        // Align brush to the destination rectangle
                        brush.TranslateTransform(imageBounds.X, imageBounds.Y);
                        g.FillPath(brush, path);
                    }
                }
                else // Draw normally (icons or pre-rendered circular artist art)
                {
                    g.DrawImage(displayImage, imageBounds);
                }
            }

            // 5. Define the text area, which starts after the image area.
            var textBounds = new Rectangle(
                imageArea.Right + IMAGE_TO_TEXT_SPACING,
                bounds.Y, // Use full item bounds for vertical centering later
                itemWidth - (imageArea.Right + IMAGE_TO_TEXT_SPACING) - HORIZONTAL_PADDING,
                bounds.Height
            );

            // 6. Draw the text, centered vertically within the text area.
            if (textBounds.Width > 0)
            {
                TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
                if (string.IsNullOrEmpty(resultItem.DisplayDetail))
                {
                    TextRenderer.DrawText(g, resultItem.DisplayTitle, ResultFont, textBounds, this.ForeColor, flags | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    var titleSize = TextRenderer.MeasureText(g, resultItem.DisplayTitle, ResultFont, textBounds.Size, flags);
                    var detailSize = TextRenderer.MeasureText(g, resultItem.DisplayDetail, ResultDetailFont, textBounds.Size, flags);

                    const int lineSpacing = 2;
                    int totalTextHeight = titleSize.Height + detailSize.Height + lineSpacing;
                    int y = textBounds.Y + (textBounds.Height - totalTextHeight) / 2;

                    var titleRect = new Rectangle(textBounds.X, y, textBounds.Width, titleSize.Height);
                    TextRenderer.DrawText(g, resultItem.DisplayTitle, ResultFont, titleRect, this.ForeColor, flags);

                    var detailRect = new Rectangle(textBounds.X, y + titleSize.Height + lineSpacing, textBounds.Width, detailSize.Height);
                    TextRenderer.DrawText(g, resultItem.DisplayDetail, ResultDetailFont, detailRect, Color.Gray, flags);
                }
            }
        }

        private void DrawScrollbar(Graphics g)
        {
            using (var trackBrush = new SolidBrush(Color.FromArgb(20, Color.White)))
            {
                g.FillRectangle(trackBrush, _scrollTrack);
            }
            
            using (var thumbBrush = new SolidBrush(Color.FromArgb(100, Color.Gray)))
            using (var path = new GraphicsPath())
            {
                int cornerRadius = _scrollThumb.Width;
                path.AddArc(_scrollThumb.X, _scrollThumb.Y, cornerRadius, cornerRadius, 180, 90);
                path.AddArc(_scrollThumb.Right - cornerRadius, _scrollThumb.Y, cornerRadius, cornerRadius, 270, 90);
                path.AddArc(_scrollThumb.Right - cornerRadius, _scrollThumb.Bottom - cornerRadius, cornerRadius, cornerRadius, 0, 90);
                path.AddArc(_scrollThumb.X, _scrollThumb.Bottom - cornerRadius, cornerRadius, cornerRadius, 90, 90);
                path.CloseFigure();
                g.FillPath(thumbBrush, path);
            }
        }

        private GraphicsPath GetRoundedRectPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            // to prevent issues with radius larger than the rectangle
            int diameter = radius * 2;
            diameter = Math.Min(diameter, Math.Min(bounds.Width, bounds.Height));

            if (diameter == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
    
            return path;
        }
        
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!_isThumbVisible) return;

            // A positive delta means scrolling up. Normalize the delta to "ticks".
            int ticks = e.Delta / 120;

            // Get the user's system-wide scroll preference.
            int lineMultiplier = SystemInformation.MouseWheelScrollLines;

            // Check for the special case where the user wants to scroll a full page at a time.
            // A very large value for lineMultiplier indicates this setting.
            if (lineMultiplier > 100)
            {
                // Scroll by the height of the visible area, minus one item to maintain context.
                ScrollTop -= ticks * (this.Height - ItemHeight);
                return;
            }

            // Standard line-based scrolling, translated to pixels.
            // This is the direct C# equivalent of the browser's DOM_DELTA_LINE handling.
            int scrollAmount = ticks * ScrollLineHeight * lineMultiplier;

            // Apply the scroll. We subtract because a positive delta (scroll up) means
            // decreasing the ScrollTop value.
            ScrollTop -= scrollAmount;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            
            if (_isThumbVisible && _scrollThumb.Contains(e.Location))
            {
                _isDraggingThumb = true;
                _dragStartY = e.Y;
                _dragStartScrollTop = _scrollTop;
            }
            else
            {
                int index = GetIndexFromY(e.Y);
                if (index >= 0 && index < _items.Count)
                {
                    if (_items[index].Type != ResultType.Header)
                    {
                        SelectedIndex = index;
                    }
                    else
                    {
                        SelectedIndex = -1; // Deselect if a header is clicked
                    }
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDraggingThumb)
            {
                if (_items.Count == 0) return;
                int contentHeight = _itemYPositions.Last() + GetItemHeight(_items.Count - 1);
                float scrollableHeight = contentHeight - Height;
                float trackHeight = Height - _scrollThumb.Height;

                if (trackHeight <= 0 || scrollableHeight <= 0) return;

                int deltaY = e.Y - _dragStartY;
                float deltaContent = (deltaY / trackHeight) * scrollableHeight;
                
                ScrollTop = _dragStartScrollTop + (int)deltaContent;
            }
            else
            {
                int index = GetIndexFromY(e.Y);
                if (index >= _items.Count)
                {
                    index = -1; // outside of items
                }

                if (_hoveredIndex != index)
                {
                    int oldHoveredIndex = _hoveredIndex;
                    _hoveredIndex = index;

                    if (oldHoveredIndex != -1) InvalidateItem(oldHoveredIndex);
                    if (_hoveredIndex != -1) InvalidateItem(_hoveredIndex);
                }
            }
        }
        
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDraggingThumb)
            {
                _isDraggingThumb = false;
                // After dragging, re-evaluate which item is being hovered over
                OnMouseMove(e);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1)
            {
                int oldHoveredIndex = _hoveredIndex;
                _hoveredIndex = -1;
                InvalidateItem(oldHoveredIndex);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollbar();
            Invalidate();
        }
        
        public new void Invalidate()
        {
            if (!_isUpdating)
            {
                base.Invalidate();
            }
        }
    }
}