using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace MusicBeePlugin.Utils
{
    // A minimal parser for the subset of SVG path "d" commands (M, L, H, V, C, A, Z -
    // upper and lower case) needed to reproduce icons copied directly from an SVG
    // source, without pulling in a full SVG rendering library.
    public static class SvgPathParser
    {
        public static GraphicsPath Parse(string d)
        {
            var path = new GraphicsPath(FillMode.Alternate);

            PointF current = PointF.Empty;
            PointF subpathStart = PointF.Empty;
            char command = '\0';

            int i = 0;
            int n = d.Length;

            while (i < n)
            {
                char c = d[i];
                if (char.IsWhiteSpace(c) || c == ',')
                {
                    i++;
                    continue;
                }

                if (IsCommandLetter(c))
                {
                    command = c;
                    i++;
                    continue;
                }

                bool relative = char.IsLower(command);

                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                        {
                            var pt = ReadPoint(d, ref i);
                            current = relative ? Add(current, pt) : pt;
                            path.StartFigure();
                            subpathStart = current;
                            // Subsequent coordinate pairs without a new command letter act as LineTo.
                            command = relative ? 'l' : 'L';
                        }
                        break;

                    case 'L':
                        {
                            var pt = ReadPoint(d, ref i);
                            var next = relative ? Add(current, pt) : pt;
                            path.AddLine(current, next);
                            current = next;
                        }
                        break;

                    case 'H':
                        {
                            double x = ReadNumber(d, ref i);
                            var next = new PointF(relative ? current.X + (float)x : (float)x, current.Y);
                            path.AddLine(current, next);
                            current = next;
                        }
                        break;

                    case 'V':
                        {
                            double y = ReadNumber(d, ref i);
                            var next = new PointF(current.X, relative ? current.Y + (float)y : (float)y);
                            path.AddLine(current, next);
                            current = next;
                        }
                        break;

                    case 'C':
                        {
                            var p1 = ReadPoint(d, ref i);
                            var p2 = ReadPoint(d, ref i);
                            var p3 = ReadPoint(d, ref i);

                            var c1 = relative ? Add(current, p1) : p1;
                            var c2 = relative ? Add(current, p2) : p2;
                            var end = relative ? Add(current, p3) : p3;

                            path.AddBezier(current, c1, c2, end);
                            current = end;
                        }
                        break;

                    case 'A':
                        {
                            double rx = ReadNumber(d, ref i);
                            double ry = ReadNumber(d, ref i);
                            double xRotDeg = ReadNumber(d, ref i);
                            bool largeArc = ReadFlag(d, ref i);
                            bool sweep = ReadFlag(d, ref i);
                            var endRaw = ReadPoint(d, ref i);
                            var end = relative ? Add(current, endRaw) : endRaw;

                            AppendArc(path, current, end, rx, ry, xRotDeg, largeArc, sweep);
                            current = end;
                        }
                        break;

                    case 'Z':
                        path.CloseFigure();
                        current = subpathStart;
                        i++; // Z/z takes no arguments.
                        break;

                    default:
                        // Unsupported command - stop rather than loop forever.
                        return path;
                }
            }

            return path;
        }

        private static bool IsCommandLetter(char c)
        {
            switch (char.ToUpperInvariant(c))
            {
                case 'M':
                case 'L':
                case 'H':
                case 'V':
                case 'C':
                case 'A':
                case 'Z':
                    return true;
                default:
                    return false;
            }
        }

        private static PointF Add(PointF a, PointF b) => new PointF(a.X + b.X, a.Y + b.Y);

        private static PointF ReadPoint(string d, ref int i)
        {
            double x = ReadNumber(d, ref i);
            double y = ReadNumber(d, ref i);
            return new PointF((float)x, (float)y);
        }

        private static bool ReadFlag(string d, ref int i)
        {
            SkipSeparators(d, ref i);
            char c = d[i];
            i++;
            return c == '1';
        }

        private static void SkipSeparators(string d, ref int i)
        {
            while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ',')) i++;
        }

        // Numbers in compact SVG path data are often packed together with no separator
        // (e.g. "-.24.22-.504.445"), so a new number starts at a sign or at a second '.'.
        private static double ReadNumber(string d, ref int i)
        {
            SkipSeparators(d, ref i);

            int start = i;
            int n = d.Length;

            if (i < n && (d[i] == '+' || d[i] == '-')) i++;

            bool sawDot = false;
            while (i < n && (char.IsDigit(d[i]) || (d[i] == '.' && !sawDot)))
            {
                if (d[i] == '.') sawDot = true;
                i++;
            }

            if (i < n && (d[i] == 'e' || d[i] == 'E'))
            {
                i++;
                if (i < n && (d[i] == '+' || d[i] == '-')) i++;
                while (i < n && char.IsDigit(d[i])) i++;
            }

            return double.Parse(d.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        // Converts an SVG elliptical arc (endpoint parameterization) into GDI+ arc
        // segment(s), following the W3C SVG spec's endpoint-to-center conversion:
        // https://www.w3.org/TR/SVG/implnote.html#ArcConversionEndpointToCenter
        private static void AppendArc(GraphicsPath path, PointF start, PointF end, double rx, double ry, double xRotDeg, bool largeArc, bool sweep)
        {
            if (rx == 0 || ry == 0 || (start.X == end.X && start.Y == end.Y))
            {
                path.AddLine(start, end);
                return;
            }

            rx = Math.Abs(rx);
            ry = Math.Abs(ry);

            double phi = xRotDeg * Math.PI / 180.0;
            double cosPhi = Math.Cos(phi);
            double sinPhi = Math.Sin(phi);

            double dx2 = (start.X - end.X) / 2.0;
            double dy2 = (start.Y - end.Y) / 2.0;

            double x1p = (cosPhi * dx2) + (sinPhi * dy2);
            double y1p = (-sinPhi * dx2) + (cosPhi * dy2);

            double lambda = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry);
            if (lambda > 1)
            {
                double scale = Math.Sqrt(lambda);
                rx *= scale;
                ry *= scale;
            }

            double rxSq = rx * rx, rySq = ry * ry, x1pSq = x1p * x1p, y1pSq = y1p * y1p;
            double num = (rxSq * rySq) - (rxSq * y1pSq) - (rySq * x1pSq);
            double den = (rxSq * y1pSq) + (rySq * x1pSq);
            double co = den <= 0 ? 0 : Math.Sqrt(Math.Max(0, num / den));
            if (largeArc == sweep) co = -co;

            double cxp = co * (rx * y1p / ry);
            double cyp = co * (-ry * x1p / rx);

            double cx = (cosPhi * cxp) - (sinPhi * cyp) + ((start.X + end.X) / 2.0);
            double cy = (sinPhi * cxp) + (cosPhi * cyp) + ((start.Y + end.Y) / 2.0);

            double startAngle = AngleBetween(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
            double delta = AngleBetween((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);

            if (!sweep && delta > 0) delta -= 2 * Math.PI;
            if (sweep && delta < 0) delta += 2 * Math.PI;

            double startAngleDeg = startAngle * 180.0 / Math.PI;
            double deltaDeg = delta * 180.0 / Math.PI;

            if (Math.Abs(xRotDeg) < 0.001)
            {
                // Fast path: no ellipse rotation. Covers every arc used by our current icon set.
                using (var arcPath = new GraphicsPath())
                {
                    arcPath.AddArc((float)(cx - rx), (float)(cy - ry), (float)(rx * 2), (float)(ry * 2),
                        (float)startAngleDeg, (float)deltaDeg);
                    path.AddPath(arcPath, true);
                }
            }
            else
            {
                using (var arcPath = new GraphicsPath())
                using (var matrix = new Matrix())
                {
                    arcPath.AddArc(-(float)rx, -(float)ry, (float)(rx * 2), (float)(ry * 2),
                        (float)startAngleDeg, (float)deltaDeg);
                    matrix.Rotate((float)xRotDeg);
                    matrix.Translate((float)cx, (float)cy, MatrixOrder.Append);
                    arcPath.Transform(matrix);
                    path.AddPath(arcPath, true);
                }
            }
        }

        private static double AngleBetween(double ux, double uy, double vx, double vy)
        {
            double dot = (ux * vx) + (uy * vy);
            double len = Math.Sqrt(((ux * ux) + (uy * uy)) * ((vx * vx) + (vy * vy)));
            double cross = (ux * vy) - (uy * vx);

            double angle = Math.Acos(Math.Max(-1, Math.Min(1, dot / len)));
            return cross < 0 ? -angle : angle;
        }
    }
}
