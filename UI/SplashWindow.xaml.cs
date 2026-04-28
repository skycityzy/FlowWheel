using System.Reflection;
using System.Windows;
using System.Windows.Media.Animation;

namespace FlowWheel.UI
{
    public partial class SplashWindow : Window
    {
        private int _progressSteps = 0;
        private const int TotalSteps = 5;

        public SplashWindow()
        {
            InitializeComponent();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
            }

            // Start progress animation
            AnimateProgress();
        }

        public void SetStatus(string text)
        {
            StatusText.Text = text;
            _progressSteps++;
            AnimateProgress();
        }

        private void AnimateProgress()
        {
            if (ProgressFill == null) return;
            
            double targetWidth = (_progressSteps / (double)TotalSteps) * 200;
            var anim = new DoubleAnimation
            {
                To = targetWidth,
                Duration = System.TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressFill.BeginAnimation(FrameworkElement.WidthProperty, anim);
        }
    }
}
