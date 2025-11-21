using System;
using System.Drawing;
using System.Windows.Forms;

namespace MusicBeePlugin.UI
{
    public class OverlayForm : Form
    {
        private double _targetOpacity;
        private System.Windows.Forms.Timer _fadeTimer;
        private double _opacityIncrement;
        private Control _targetControl;

        protected override bool ShowWithoutActivation => true;

        public OverlayForm(Control targetControl, double opacity, double fadeDurationSeconds)
        {
            _targetControl = targetControl;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Opacity = 0;
            ShowInTaskbar = false;

            // Get initial bounds safely from the target control's thread
            Rectangle bounds;
            if (_targetControl.InvokeRequired)
            {
                bounds = (Rectangle)_targetControl.Invoke(new Func<Rectangle>(() =>
                    new Rectangle(_targetControl.PointToScreen(Point.Empty), _targetControl.Size)));
            }
            else
            {
                bounds = new Rectangle(_targetControl.PointToScreen(Point.Empty), _targetControl.Size);
            }

            Size = bounds.Size;
            Location = bounds.Location;

            _targetOpacity = opacity;

            if (fadeDurationSeconds > 0)
            {
                _fadeTimer = new System.Windows.Forms.Timer();
                _fadeTimer.Interval = 30;
                _fadeTimer.Tick += FadeTimer_Tick;
                _opacityIncrement = (_targetOpacity / (fadeDurationSeconds * 1000.0 / _fadeTimer.Interval));
                _fadeTimer.Start();
            }
            else
            {
                Opacity = _targetOpacity;
            }

            // Subscribe to events on target control's thread
            if (_targetControl.InvokeRequired)
            {
                _targetControl.BeginInvoke(new Action(() =>
                {
                    _targetControl.LocationChanged += TargetControl_LocationChanged;
                    _targetControl.SizeChanged += TargetControl_SizeChanged;
                }));
            }
            else
            {
                _targetControl.LocationChanged += TargetControl_LocationChanged;
                _targetControl.SizeChanged += TargetControl_SizeChanged;
            }
        }

        private void TargetControl_LocationChanged(object sender, EventArgs e)
        {
            // Runs on target control thread
            if (IsDisposed || _targetControl == null || _targetControl.IsDisposed) return;
            try
            {
                var loc = _targetControl.PointToScreen(Point.Empty);
                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed) Location = loc;
                }));
            }
            catch { }
        }

        private void TargetControl_SizeChanged(object sender, EventArgs e)
        {
            // Runs on target control thread
            if (IsDisposed || _targetControl == null || _targetControl.IsDisposed) return;
            try
            {
                var sz = _targetControl.Size;
                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed) Size = sz;
                }));
            }
            catch { }
        }

        private void FadeTimer_Tick(object sender, EventArgs e)
        {
            double newOpacity = Opacity + _opacityIncrement;
            if (newOpacity >= _targetOpacity)
            {
                Opacity = _targetOpacity;
                _fadeTimer.Stop();
                _fadeTimer.Dispose();
                _fadeTimer = null;
            }
            else
            {
                Opacity = newOpacity;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!e.Cancel)
            {
                this.Visible = false;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_fadeTimer != null)
                {
                    _fadeTimer.Stop();
                    _fadeTimer.Dispose();
                    _fadeTimer = null;
                }

                if (_targetControl != null)
                {
                    // Unsubscribe on target control thread to avoid cross-thread operation exceptions or leaks
                    var ctrl = _targetControl;
                    if (ctrl.InvokeRequired)
                    {
                        try
                        {
                            ctrl.BeginInvoke(new Action(() =>
                            {
                                ctrl.LocationChanged -= TargetControl_LocationChanged;
                                ctrl.SizeChanged -= TargetControl_SizeChanged;
                            }));
                        }
                        catch { }
                    }
                    else
                    {
                        ctrl.LocationChanged -= TargetControl_LocationChanged;
                        ctrl.SizeChanged -= TargetControl_SizeChanged;
                    }
                    _targetControl = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}