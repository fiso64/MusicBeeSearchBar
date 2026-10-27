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
        private readonly object _subscriptionLock = new object();
        private bool _subscriptionsDisposed;

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000;
                const int WS_EX_TOOLWINDOW = 0x00000080;
                var parameters = base.CreateParams;
                parameters.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return parameters;
            }
        }

        // Clicking the backdrop must not activate it or move it above the search bar.
        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEACTIVATE = 0x0021;
            const int MA_NOACTIVATE = 3;
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }
            base.WndProc(ref m);
        }

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

            // Capture the control: disposal may clear the field before this callback runs.
            var target = _targetControl;
            Action subscribe = () =>
            {
                lock (_subscriptionLock)
                {
                    if (_subscriptionsDisposed || target.IsDisposed) return;
                    target.LocationChanged += TargetControl_LocationChanged;
                    target.SizeChanged += TargetControl_SizeChanged;
                }
            };
            if (target.InvokeRequired)
                target.BeginInvoke(subscribe);
            else
                subscribe();
        }

        private void TargetControl_LocationChanged(object sender, EventArgs e)
        {
            // Runs on target control thread
            var target = sender as Control;
            if (IsDisposed || Disposing || target == null || target.IsDisposed) return;
            try
            {
                var loc = target.PointToScreen(Point.Empty);
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
            var target = sender as Control;
            if (IsDisposed || Disposing || target == null || target.IsDisposed) return;
            try
            {
                var sz = target.Size;
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
                    Control ctrl;
                    lock (_subscriptionLock)
                    {
                        _subscriptionsDisposed = true;
                        ctrl = _targetControl;
                        _targetControl = null;
                    }
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
                }
            }
            base.Dispose(disposing);
        }
    }
}
