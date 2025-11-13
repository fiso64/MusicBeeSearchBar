using MusicBeePlugin.Services;
using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using MusicBeePlugin.Services;
using System.Collections.Generic;

namespace MusicBeePlugin.UI
{
    public class CustomResultList : Control
    {
        private List<SearchResult> _items = new List<SearchResult>();
        private int _selectedIndex = -1;
        private int _topIndex = 0;
        private bool _isUpdating = false;

        // Scrollbar state
        private Rectangle _scrollTrack;
        private Rectangle _scrollThumb;
        private bool _isThumbVisible = false;
        private bool _isDraggingThumb = false;
        private int _dragStartY;
        private int _dragStartTopIndex;

        // Styling & Resources
        public int ItemHeight { get; set; } = 56;
        public Color HighlightColor { get; set; }
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
                TopIndex = 0;
                SelectedIndex = _items.Count > 0 ? 0 : -1;
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

        public int TopIndex
        {
            get => _topIndex;
            set
            {
                int maxTopIndex = Math.Max(0, _items.Count - VisibleItemCount);
                var newValue = Math.Max(0, Math.Min(value, maxTopIndex));
                if (_topIndex != newValue)
                {
                    Debug.WriteLine($"[CustomResultList] TopIndex set from {_topIndex} to {newValue}.");
                    _topIndex = newValue;
                    UpdateScrollbar();
                    Invalidate();
                }
            }
        }

        private int VisibleItemCount
        {
            get
            {
                var count = Height > 0 && ItemHeight > 0 ? Height / ItemHeight : 0;
                return count;
            }
        }

        public CustomResultList()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
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
            if (index >= TopIndex && index < TopIndex + VisibleItemCount + 1)
            {
                var rect = new Rectangle(0, (index - TopIndex) * ItemHeight, Width, ItemHeight);
                Invalidate(rect);
            }
        }

        private void EnsureVisible(int index)
        {
            if (index < 0) return;
            
            if (index < TopIndex)
            {
                TopIndex = index;
            }
            else if (index >= TopIndex + VisibleItemCount)
            {
                TopIndex = index - VisibleItemCount + 1;
            }
        }

        private void UpdateScrollbar()
        {
            if (_items.Count <= VisibleItemCount)
            {
                _isThumbVisible = false;
                return;
            }

            _isThumbVisible = true;
            const int scrollWidth = 8;
            _scrollTrack = new Rectangle(Width - scrollWidth, 0, scrollWidth, Height);

            float contentHeight = _items.Count * ItemHeight;
            float thumbHeight = Math.Max(20, Height * (Height / contentHeight));

            float scrollableRatio = (TopIndex * ItemHeight) / (contentHeight - Height);
            float thumbY = (Height - thumbHeight) * scrollableRatio;

            _scrollThumb = new Rectangle(
                _scrollTrack.X + 1,
                (int)thumbY,
                scrollWidth - 2,
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

            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int visibleCount = VisibleItemCount;
            Debug.WriteLine($"[CustomResultList] OnPaint: TopIndex={TopIndex}, VisibleItemCount={visibleCount}, TotalItems={_items.Count}.");
            int end = Math.Min(_items.Count, TopIndex + visibleCount + 1);
            for (int i = TopIndex; i < end; i++)
            {
                var item = _items[i];
                var bounds = new Rectangle(0, (i - TopIndex) * ItemHeight, Width, ItemHeight);
                if (i == TopIndex)
                {
                    Debug.WriteLine($"[CustomResultList] OnPaint: Drawing first visible item '{item.DisplayTitle}' at bounds {bounds}.");
                }
                DrawItem(e.Graphics, item, bounds, i == _selectedIndex);
            }
            
            if (_isThumbVisible)
            {
                DrawScrollbar(e.Graphics);
            }
        }

        private void DrawItem(Graphics g, SearchResult resultItem, Rectangle bounds, bool isSelected)
        {
            const int HORIZONTAL_PADDING = 10;
            const int VERTICAL_PADDING = 10;
            const int IMAGE_TO_TEXT_SPACING = 10;

            Color backgroundColor = this.BackColor;
            if (isSelected)
            {
                backgroundColor = this.HighlightColor;
            }

            using (var backgroundBrush = new SolidBrush(backgroundColor))
            {
                g.FillRectangle(backgroundBrush, bounds);
            }

            // 1. Create the main content area by applying padding to the item's bounds.
            var contentBounds = new Rectangle(
                bounds.X + HORIZONTAL_PADDING,
                bounds.Y + VERTICAL_PADDING,
                bounds.Width - (HORIZONTAL_PADDING * 2),
                bounds.Height - (VERTICAL_PADDING * 2)
            );
            
            // Exit if the content area is invalid
            if (contentBounds.Width <= 0 || contentBounds.Height <= 0) return;

            // 2. Define a fixed-size square area on the left for the image/icon.
            // Its size is determined by the available content height.
            var imageArea = new Rectangle(
                contentBounds.X,
                contentBounds.Y,
                contentBounds.Height,
                contentBounds.Height
            );

            // 3. Determine which image to use and its actual size.
            int artworkSize = imageArea.Height;
            int iconSize = (int)(imageArea.Height * 0.6);
            
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
                g.DrawImage(displayImage, imageX, imageY, effectiveSize, effectiveSize);
            }

            // 5. Define the text area, which starts after the image area.
            var textBounds = new Rectangle(
                imageArea.Right + IMAGE_TO_TEXT_SPACING,
                contentBounds.Y,
                contentBounds.Right - (imageArea.Right + IMAGE_TO_TEXT_SPACING),
                contentBounds.Height
            );

            // 6. Draw the text, centered vertically within the text area.
            if (textBounds.Width > 0)
            {
                TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
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
        
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!_isThumbVisible) return;
            
            int delta = e.Delta > 0 ? -1 : 1;
            TopIndex += delta;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            
            if (_isThumbVisible && _scrollThumb.Contains(e.Location))
            {
                _isDraggingThumb = true;
                _dragStartY = e.Y;
                _dragStartTopIndex = TopIndex;
            }
            else
            {
                int index = TopIndex + (e.Y / ItemHeight);
                if (index < _items.Count)
                {
                    SelectedIndex = index;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDraggingThumb)
            {
                float contentHeight = _items.Count * ItemHeight;
                float scrollableHeight = contentHeight - Height;
                float trackHeight = Height - _scrollThumb.Height;

                int deltaY = e.Y - _dragStartY;
                float deltaContent = (deltaY / trackHeight) * scrollableHeight;
                
                TopIndex = _dragStartTopIndex + (int)(deltaContent / ItemHeight);
            }
        }
        
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isDraggingThumb = false;
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