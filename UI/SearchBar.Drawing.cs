using MusicBeePlugin.Config;
using MusicBeePlugin.Services;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MusicBeePlugin.UI
{
    public partial class SearchBar
    {
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