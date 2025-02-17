using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MusicBeePlugin.UI
{
    public class OverlayForm : Form
    {
        private double _targetOpacity;
        private double _fadeDurationSeconds;
        private System.Windows.Forms.Timer _fadeTimer;
        private double _opacityIncrement;

        public OverlayForm(Control targetControl, double opacity, double fadeDurationSeconds)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Opacity = 0;
            ShowInTaskbar = false;
            Size = targetControl.Size;
            Location = targetControl.PointToScreen(Point.Empty);

            _targetOpacity = opacity;
            _fadeDurationSeconds = fadeDurationSeconds;

            if (_fadeDurationSeconds > 0)
            {
                _fadeTimer = new System.Windows.Forms.Timer();
                _fadeTimer.Interval = 30;
                _fadeTimer.Tick += FadeTimer_Tick;
                _opacityIncrement = (_targetOpacity / (_fadeDurationSeconds * 1000.0 / _fadeTimer.Interval));
            }
            else
            {
                Opacity = _targetOpacity;
            }

            if (_fadeTimer != null)
            {
                _fadeTimer.Start();
            }

            targetControl.LocationChanged += (sender, e) =>
            {
                Location = targetControl.PointToScreen(Point.Empty);
            };

            targetControl.SizeChanged += (sender, e) =>
            {
                Size = targetControl.Size;
            };
        }

        private void FadeTimer_Tick(object sender, EventArgs e)
        {
            Opacity += _opacityIncrement;

            if (Opacity >= _targetOpacity)
            {
                Opacity = _targetOpacity;
                _fadeTimer.Stop();
                _fadeTimer.Dispose();
                _fadeTimer = null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (_fadeTimer != null))
            {
                _fadeTimer.Stop();
                _fadeTimer.Dispose();
                _fadeTimer = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_fadeTimer != null)
            {
                _fadeTimer.Stop();
                _fadeTimer.Dispose();
                _fadeTimer = null;
            }
            base.OnHandleDestroyed(e);
        }
    }
}
