using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using FlowWheel.Core;

namespace FlowWheel.UI
{
    public partial class OverlayWindow : Window
    {
        private RotateTransform? _wheelRotate;
        private double _currentRotation = 0;
        private double _rotationSpeed = 0;
        private bool _isSpinning = false;
        private System.Diagnostics.Stopwatch? _spinTimer;

        public OverlayWindow()
        {
            InitializeComponent();
            LoadCustomIcon();
            _spinTimer = System.Diagnostics.Stopwatch.StartNew();
            CompositionTarget.Rendering += OnRendering;
        }

        private void LoadCustomIcon()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "anchor.png");
                if (File.Exists(path))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    CustomAnchorImage.Source = bitmap;
                    CustomAnchorImage.Visibility = Visibility.Visible;
                    WheelIndicator.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load custom icon: {ex.Message}");
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW);
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (_isSpinning && SpinningWheel != null)
            {
                var dt = _spinTimer?.Elapsed.TotalSeconds ?? 0;
                _spinTimer?.Restart();
                
                _currentRotation += _rotationSpeed * dt;
                if (SpinningWheel.RenderTransform is RotateTransform rt)
                {
                    rt.Angle = _currentRotation;
                }
            }
        }

        public void ShowAnchor(double x, double y)
        {
            Canvas.SetLeft(Anchor, x - Anchor.Width / 2);
            Canvas.SetTop(Anchor, y - Anchor.Height / 2);
            Anchor.Visibility = Visibility.Visible;
            
            // Reset
            WheelIndicator.Opacity = 1.0;
            if (CustomAnchorImage.Visibility == Visibility.Visible) 
                CustomAnchorImage.Opacity = 1.0;
            
            ReadingIcon.Visibility = Visibility.Collapsed;
            OuterRing.Visibility = Visibility.Visible;
            SpinningWheel.Visibility = Visibility.Visible;
            
            // Reset arrows
            ArrowUp.Visibility = Visibility.Collapsed;
            ArrowDown.Visibility = Visibility.Collapsed;
            ArrowLeft.Visibility = Visibility.Collapsed;
            ArrowRight.Visibility = Visibility.Collapsed;

            // Start subtle idle spin
            _rotationSpeed = 30; // Slow idle rotation
            _isSpinning = true;

            this.Show();
        }

        public void SetReadingMode(bool enabled)
        {
            if (enabled)
            {
                OuterRing.Visibility = Visibility.Collapsed;
                SpinningWheel.Visibility = Visibility.Collapsed;
                ReadingIcon.Visibility = Visibility.Visible;
                _isSpinning = false;
                UpdateDirection(false, false, false, false);
            }
            else
            {
                ReadingIcon.Visibility = Visibility.Collapsed;
                OuterRing.Visibility = Visibility.Visible;
                SpinningWheel.Visibility = Visibility.Visible;
                _isSpinning = true;
            }
        }

        public void UpdateDirection(bool up, bool down, bool left, bool right)
        {
            ArrowUp.Visibility = up ? Visibility.Visible : Visibility.Collapsed;
            ArrowDown.Visibility = down ? Visibility.Visible : Visibility.Collapsed;
            ArrowLeft.Visibility = left ? Visibility.Visible : Visibility.Collapsed;
            ArrowRight.Visibility = right ? Visibility.Visible : Visibility.Collapsed;
            
            // Adjust spin speed based on direction
            if (up || down)
            {
                _rotationSpeed = down ? 180 : -180; // Spin faster when scrolling
            }
            else
            {
                _rotationSpeed = 30; // Slow idle
            }
        }

        public void UpdateDistance(double distance)
        {
            double opacity = 1.0;

            if (distance < 60)
            {
                // Fade out when close
                opacity = 0.3 + (distance / 60.0) * 0.7;
            }
            else if (distance > 150)
            {
                // Fade out when far
                opacity = 1.0 - (distance - 150) / 400.0;
                if (opacity < 0.3) opacity = 0.3;
            }
            
            WheelIndicator.Opacity = opacity;
            if (CustomAnchorImage.Visibility == Visibility.Visible) 
                CustomAnchorImage.Opacity = opacity;
        }

        public void HideAnchor()
        {
            _isSpinning = false;
            Anchor.Visibility = Visibility.Collapsed;
            this.Hide();
        }
    }
}