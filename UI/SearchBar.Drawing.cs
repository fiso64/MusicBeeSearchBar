﻿using MusicBeePlugin.Config;
using MusicBeePlugin.Services;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MusicBeePlugin.UI
{
    public partial class SearchBar
    {
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
                int iconWidth = iconSize;
                int ICON_PADDING_RIGHT = 8;
                int OFFSET_Y = 6;
                int leftPadding = 6 + (imageSize - iconSize) / 2;
                int textStartX = bounds.X + imageSize + ICON_PADDING_RIGHT + 8;

                Color detailColor = Color.Gray;

                Image displayImage = null;
                if (searchUIConfig.ShowImages)
                {
                    displayImage = imageService.GetCachedImage(resultItem);

                    if (displayImage != null)
                    {
                        leftPadding = 6;
                        iconWidth = imageSize;
                    }
                }

                // Fall back to icon if no image available
                if (displayImage == null)
                {
                    displayImage = GetIcon(resultItem.Type);
                }

                if (displayImage != null)
                {
                    int imageY = bounds.Y + (bounds.Height - iconWidth) / 2;
                    g.DrawImage(displayImage, bounds.X + leftPadding, imageY, iconWidth, iconWidth);
                }


                if (string.IsNullOrEmpty(resultItem.DisplayDetail))
                {
                    // Calculate maximum width available for text
                    int maxTextWidth = bounds.Width - textStartX - iconSize - 20; // 20 for right padding + icon padding
                    string truncatedTitle = TextRenderer.MeasureText(resultItem.DisplayTitle, resultFont).Width > maxTextWidth
                        ? TextRenderer.MeasureText(resultItem.DisplayTitle + "...", resultFont).Width <= maxTextWidth
                            ? resultItem.DisplayTitle + "..."
                            : resultItem.DisplayTitle.Substring(0, Math.Max(1, resultItem.DisplayTitle.Length * maxTextWidth / TextRenderer.MeasureText(resultItem.DisplayTitle, resultFont).Width - 3)) + "..."
                        : resultItem.DisplayTitle;

                    int offset = Math.Max(searchUIConfig.ResultItemHeight / 2 - (int)resultFont.Size + 2, 0);
                    g.DrawString(truncatedTitle, resultFont, textBrush, textStartX, bounds.Y + offset);
                }
                else
                {
                    // Calculate maximum width available for text
                    int maxTextWidth = bounds.Width - textStartX - iconSize - 20; // 20 for right padding + icon padding

                    string truncatedTitle = TextRenderer.MeasureText(resultItem.DisplayTitle, resultFont).Width > maxTextWidth
                        ? TextRenderer.MeasureText(resultItem.DisplayTitle + "...", resultFont).Width <= maxTextWidth
                            ? resultItem.DisplayTitle + "..."
                            : resultItem.DisplayTitle.Substring(0, Math.Max(1, resultItem.DisplayTitle.Length * maxTextWidth / TextRenderer.MeasureText(resultItem.DisplayTitle, resultFont).Width - 3)) + "..."
                        : resultItem.DisplayTitle;

                    string truncatedDetail = TextRenderer.MeasureText(resultItem.DisplayDetail, resultDetailFont).Width > maxTextWidth
                        ? TextRenderer.MeasureText(resultItem.DisplayDetail + "...", resultDetailFont).Width <= maxTextWidth
                            ? resultItem.DisplayDetail + "..."
                            : resultItem.DisplayDetail.Substring(0, Math.Max(1, resultItem.DisplayDetail.Length * maxTextWidth / TextRenderer.MeasureText(resultItem.DisplayDetail, resultDetailFont).Width - 3)) + "..."
                        : resultItem.DisplayDetail;

                    g.DrawString(truncatedTitle, resultFont, textBrush, textStartX, bounds.Y + OFFSET_Y);
                    g.DrawString(truncatedDetail, resultDetailFont, new SolidBrush(detailColor), textStartX, bounds.Y + OFFSET_Y + resultFont.GetHeight() + 2);
                }

                // Add type indicator icon on the right, but not for Commands or if images are disabled.
                // Primarily for distinguishing Album/Song when a generic image might be shown.
                if (searchUIConfig.ShowImages && resultItem.Type != ResultType.Command && 
                    (resultItem.Type == ResultType.Album || resultItem.Type == ResultType.Song || resultItem.Type == ResultType.Artist || resultItem.Type == ResultType.Playlist))
                {
                    var typeIcon = GetIcon(resultItem.Type); // Get the specific icon for Album or Song
                    if (typeIcon != null) // Ensure there's an icon to draw
                    {
                        int rightPadding = 10;
                        // Scale the icon slightly smaller for this right-side indicator
                        float indicatorIconScale = 0.66f; // 2/3 of the main iconSize
                        int indicatorIconSize = (int)(iconSize * indicatorIconScale);
                        int iconY = bounds.Y + (bounds.Height - indicatorIconSize) / 2;
                        g.DrawImage(typeIcon, bounds.Right - indicatorIconSize - rightPadding, iconY, indicatorIconSize, indicatorIconSize);
                    }
                }
            }
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
                        // Thin outer circle (record)
                        g.DrawEllipse(pen, 2, 2, width - 4, height - 4);

                        // Thick inner circle (label)
                        using (Pen thickPen = new Pen(color, lineWidth * 2)) // Thicker pen for the inner circle
                        {
                            int innerCircleSize = width / 3; // Size of the inner circle
                            g.DrawEllipse(thickPen,
                                (width - innerCircleSize) / 2, // Center horizontally
                                (height - innerCircleSize) / 2, // Center vertically
                                innerCircleSize,
                                innerCircleSize
                            );
                        }
                        break;
                    case ResultType.Song:
                        // Triangle pointing right with rounded corners
                        using (GraphicsPath path = new GraphicsPath())
                        {
                            float cornerRadius = 2f;
                            PointF[] points = {
                                new PointF(4, 2),
                                new PointF(width - 2, (height - 2) / 2),
                                new PointF(4, height - 4)
                            };

                            // Create rounded corners using curves
                            path.AddBezier(
                                points[0].X, points[0].Y + cornerRadius,  // Start point
                                points[0].X, points[0].Y,                 // Control point 1
                                points[0].X + cornerRadius, points[0].Y,  // Control point 2
                                points[0].X + cornerRadius, points[0].Y   // End point
                            );
                            path.AddLine(
                                points[0].X + cornerRadius, points[0].Y,
                                points[1].X - cornerRadius, points[1].Y - cornerRadius
                            );
                            path.AddBezier(
                                points[1].X - cornerRadius, points[1].Y - cornerRadius,
                                points[1].X, points[1].Y - cornerRadius,
                                points[1].X, points[1].Y,
                                points[1].X - cornerRadius, points[1].Y + cornerRadius
                            );
                            path.AddLine(
                                points[1].X - cornerRadius, points[1].Y + cornerRadius,
                                points[2].X + cornerRadius, points[2].Y
                            );
                            path.AddBezier(
                                points[2].X + cornerRadius, points[2].Y,
                                points[2].X, points[2].Y,
                                points[2].X, points[2].Y - cornerRadius,
                                points[2].X, points[2].Y - cornerRadius
                            );
                            path.CloseFigure();

                            g.DrawPath(pen, path);
                        }
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
                    case ResultType.Playlist:
                        // List icon (3 horizontal lines)
                        int lineSpacing = 5;
                        for (int i = 0; i < 3; i++)
                        {
                            g.DrawLine(pen,
                                3,
                                4 + i * lineSpacing,
                                width - 4,
                                4 + i * lineSpacing);
                        }
                        break;
                    case ResultType.Command:
                        // Stylized ">" for command palette
                        using (GraphicsPath path = new GraphicsPath())
                        {
                            float hPadding = width * 0.25f; // Horizontal padding
                            float vPadding = height * 0.25f; // Vertical padding

                            PointF p1 = new PointF(hPadding, vPadding); // Top-left of ">"
                            PointF p2 = new PointF(width * 0.60f, height / 2f); // Tip of ">"
                            PointF p3 = new PointF(hPadding, height - vPadding); // Bottom-left of ">"
                            
                            path.AddLine(p1, p2);
                            path.AddLine(p2, p3);
                            
                            // Optional: Add a slight "underscore" or bounding box element
                            // For now, just the ">"
                            g.DrawPath(pen, path);
                        }
                        break;
                }
            }
            return icon;
        }

        public Image GetIcon(ResultType resultType)
        {
            if (resultType == ResultType.Song)
                return songIcon;
            else if (resultType == ResultType.Album)
                return albumIcon;
            else if (resultType == ResultType.Artist)
                return artistIcon;
            else if (resultType == ResultType.Playlist)
                return playlistIcon;
            else if (resultType == ResultType.Command)
                return commandIcon;
            return null;
        }
    }
}
