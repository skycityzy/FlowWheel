using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FlowWheel.Core;
using FlowWheel.UI.Controls;
using MediaColor = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;
using Brush = System.Windows.Media.Brush;
using Point = System.Windows.Point;

namespace FlowWheel.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly ScrollEngine _engine;
        private readonly AutoScrollManager _manager;
        private WindowManager _windowManager;
        private bool _isDarkMode = false;
        private bool _isRecordingHotkey = false;
        private string _tempHotkey = "";
        private string _currentPage = "General";
        private bool _isNavigating = false;

        public SettingsWindow(ScrollEngine engine, AutoScrollManager manager, WindowManager windowManager)
        {
            InitializeComponent();
            _engine = engine;
            _manager = manager;
            _windowManager = windowManager;

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
                VersionLabel.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
            }

            InitializeValues();
            SetupNavigation();
            SetupEventHandlers();
            SetupDarkMode();
            CheckUpdateOnStartup();
        }

        private void InitializeValues()
        {
            SpeedSlider.Value = ConfigManager.Current.Sensitivity;
            DeadzoneSlider.Value = ConfigManager.Current.Deadzone;
            
            EnableToggle.IsOn = ConfigManager.Current.IsEnabled;
            StartupToggle.IsOn = ConfigManager.Current.StartupEnabled;
            SyncToggle.IsOn = ConfigManager.Current.IsSyncScrollEnabled;
            ReadingModeToggle.IsOn = ConfigManager.Current.IsReadingModeEnabled;
            
            if (ConfigManager.Current.TriggerMode == "Hold")
                RadioHoldDrag.IsChecked = true;
            else
                RadioClickToggle.IsChecked = true;

            foreach (ComboBoxItem item in TriggerKeyCombo.Items)
            {
                if (item.Tag?.ToString() == ConfigManager.Current.TriggerKey)
                {
                    item.IsSelected = true;
                    break;
                }
            }
            
            if (ConfigManager.Current.IsWhitelistMode)
            {
                RadioWhitelist.IsChecked = true;
                FilterModeHelpText.Text = "Only processes in this list will have auto-scroll enabled.";
            }
            else
            {
                RadioBlacklist.IsChecked = true;
                FilterModeHelpText.Text = "Processes in this list will be ignored (auto-scroll disabled).";
            }
            
            HotkeyInput.Text = string.IsNullOrEmpty(ConfigManager.Current.ToggleHotkey) ? "Click to set hotkey..." : ConfigManager.Current.ToggleHotkey;

            foreach (ComboBoxItem item in LanguageCombo.Items)
            {
                if (item.Tag?.ToString() == ConfigManager.Current.Language)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            RefreshBlacklist();
        }

        private void SetupNavigation()
        {
            NavGeneral.Checked += (s, e) => NavigateTo("General");
            NavBehavior.Checked += (s, e) => NavigateTo("Behavior");
            NavFilter.Checked += (s, e) => NavigateTo("Filter");
            NavShortcuts.Checked += (s, e) => NavigateTo("Shortcuts");
            NavAbout.Checked += (s, e) => NavigateTo("About");
        }

        private void SetupEventHandlers()
        {
            EnableToggle.IsOnChanged += (s, e) => 
            {
                ConfigManager.Current.IsEnabled = EnableToggle.IsOn;
                if (_manager != null) _manager.IsEnabled = EnableToggle.IsOn;
                ConfigManager.Save();
            };

            StartupToggle.IsOnChanged += (s, e) =>
            {
                ConfigManager.Current.StartupEnabled = StartupToggle.IsOn;
                SetStartup(StartupToggle.IsOn);
                ConfigManager.Save();
            };

            SyncToggle.IsOnChanged += (s, e) => 
            {
                ConfigManager.Current.IsSyncScrollEnabled = SyncToggle.IsOn;
                if (_engine != null) _engine.IsSyncEnabled = SyncToggle.IsOn;
                ConfigManager.Save();
            };

            ReadingModeToggle.IsOnChanged += (s, e) => 
            {
                ConfigManager.Current.IsReadingModeEnabled = ReadingModeToggle.IsOn;
                ConfigManager.Save();
            };

            RadioClickToggle.Checked += (s, e) => { ConfigManager.Current.TriggerMode = "Toggle"; ConfigManager.Save(); };
            RadioHoldDrag.Checked += (s, e) => { ConfigManager.Current.TriggerMode = "Hold"; ConfigManager.Save(); };

            RadioBlacklist.Checked += RadioFilterMode_Changed;
            RadioWhitelist.Checked += RadioFilterMode_Changed;
        }

        private void NavigateTo(string page)
        {
            if (PageGeneral == null || _isNavigating || _currentPage == page) return;
            
            _isNavigating = true;
            
            // 确定导航方向
            var pageOrder = new[] { "General", "Behavior", "Filter", "Shortcuts", "About" };
            int oldIndex = Array.IndexOf(pageOrder, _currentPage);
            int newIndex = Array.IndexOf(pageOrder, page);
            bool goingForward = newIndex > oldIndex;
            
            // 获取当前和目标页面
            var oldPage = GetPage(_currentPage);
            var newPage = GetPage(page);
            
            if (oldPage == null || newPage == null)
            {
                _isNavigating = false;
                return;
            }
            
            // 准备新页面位置
            var slideOffset = 50.0;
            if (goingForward)
            {
                newPage.RenderTransform = new TranslateTransform(slideOffset, 0);
                newPage.Opacity = 0;
            }
            else
            {
                newPage.RenderTransform = new TranslateTransform(-slideOffset, 0);
                newPage.Opacity = 0;
            }
            
            newPage.Visibility = Visibility.Visible;
            
            // 创建动画
            var duration = TimeSpan.FromMilliseconds(250);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            
            // 旧页面滑出动画
            var oldSlideOut = new DoubleAnimation
            {
                To = goingForward ? -slideOffset : slideOffset,
                Duration = duration,
                EasingFunction = ease
            };
            
            var oldFadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = duration,
                EasingFunction = ease
            };
            
            // 新页面滑入动画
            var newSlideIn = new DoubleAnimation
            {
                To = 0,
                Duration = duration,
                EasingFunction = ease
            };
            
            var newFadeIn = new DoubleAnimation
            {
                To = 1,
                Duration = duration,
                EasingFunction = ease
            };
            
            // 动画完成后清理
            oldSlideOut.Completed += (s, e) =>
            {
                oldPage.Visibility = Visibility.Collapsed;
                oldPage.RenderTransform = new TranslateTransform(0, 0);
                oldPage.Opacity = 1;
                _isNavigating = false;
            };
            
            // 启动动画
            if (oldPage.RenderTransform is TranslateTransform oldTransform)
            {
                oldTransform.BeginAnimation(TranslateTransform.XProperty, oldSlideOut);
            }
            oldPage.BeginAnimation(OpacityProperty, oldFadeOut);
            
            if (newPage.RenderTransform is TranslateTransform newTransform)
            {
                newTransform.BeginAnimation(TranslateTransform.XProperty, newSlideIn);
            }
            newPage.BeginAnimation(OpacityProperty, newFadeIn);
            
            _currentPage = page;
            PageTitle.Text = FindResource($"Nav{page}") as string ?? page;
        }
        
        private StackPanel? GetPage(string page)
        {
            return page switch
            {
                "General" => PageGeneral,
                "Behavior" => PageBehavior,
                "Filter" => PageFilter,
                "Shortcuts" => PageShortcuts,
                "About" => PageAbout,
                _ => null
            };
        }

        private void SetupDarkMode()
        {
            _isDarkMode = ConfigManager.Current.IsDarkMode;
            DarkModeToggle.IsOn = _isDarkMode;

            DarkModeToggle.IsOnChanged += (s, e) =>
            {
                _isDarkMode = DarkModeToggle.IsOn;
                ConfigManager.Current.IsDarkMode = _isDarkMode;
                ConfigManager.Save();

                if (DarkModeOverlay != null)
                {
                    // 获取开关在窗口中的位置
                    var togglePos = DarkModeToggle.TransformToAncestor(this).Transform(new Point(0, 0));
                    var toggleCenterX = togglePos.X + DarkModeToggle.ActualWidth / 2;
                    var toggleCenterY = togglePos.Y + DarkModeToggle.ActualHeight / 2;

                    // 设置目标颜色
                    var targetColor = _isDarkMode 
                        ? MediaColor.FromRgb(26, 26, 46) 
                        : MediaColor.FromRgb(245, 245, 247);
                    DarkModeOverlay.Background = new SolidColorBrush(targetColor);
                    
                    // 重置状态
                    DarkModeOverlay.Opacity = 1;
                    OverlayScale.ScaleX = 0;
                    OverlayScale.ScaleY = 0;

                    // 设置缩放中心为开关位置
                    OverlayScale.CenterX = toggleCenterX;
                    OverlayScale.CenterY = toggleCenterY;

                    // 计算需要覆盖整个窗口的缩放比例
                    double maxDist = Math.Max(
                        Math.Max(toggleCenterX, ActualWidth - toggleCenterX),
                        Math.Max(toggleCenterY, ActualHeight - toggleCenterY)
                    );
                    double targetScale = (maxDist * 2.5) / Math.Min(ActualWidth, ActualHeight);

                    // 使用弹性动画效果
                    var elasticEase = new ElasticEase 
                    { 
                        EasingMode = EasingMode.EaseOut,
                        Oscillations = 1,
                        Springiness = 8
                    };

                    var scaleAnim = new DoubleAnimation
                    {
                        From = 0,
                        To = targetScale,
                        Duration = TimeSpan.FromMilliseconds(700),
                        EasingFunction = elasticEase
                    };

                    var opacityAnim = new DoubleAnimation
                    {
                        From = 1,
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(300),
                        BeginTime = TimeSpan.FromMilliseconds(500),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };

                    scaleAnim.Completed += (sender, args) =>
                    {
                        ThemeManager.ApplyTheme(_isDarkMode);
                        DarkModeOverlay.ClearValue(Border.BackgroundProperty);
                    };

                    DarkModeOverlay.BeginAnimation(OpacityProperty, opacityAnim);
                    OverlayScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    OverlayScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
                else
                {
                    ThemeManager.ApplyTheme(_isDarkMode);
                }
            };
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    yield return typedChild;
                    
                foreach (var grandChild in FindVisualChildren<T>(child))
                    yield return grandChild;
            }
        }

        private async void CheckUpdateOnStartup()
        {
            try
            {
                var r = await UpdateManager.CheckForUpdatesAsync();

                if (!string.IsNullOrWhiteSpace(r.ErrorMessage)) return;

                if (r.HasUpdate)
                {
                    var notes = string.IsNullOrWhiteSpace(r.ReleaseNotes)
                        ? ""
                        : "\n\nRelease notes:\n" + Truncate(r.ReleaseNotes, 600);

                    var result = WpfMessageBox.Show(
                        $"A new version {r.LatestTag} is available!\nCurrent: v{r.CurrentVersion.Major}.{r.CurrentVersion.Minor}.{r.CurrentVersion.Build}\n\nDownload now?{notes}",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        var url = !string.IsNullOrWhiteSpace(r.AssetDownloadUrl) ? r.AssetDownloadUrl : r.ReleasePageUrl;
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = url,
                                UseShellExecute = true
                            });
                        }
                    }
                }
            }
            catch { }
        }

        private void RefreshBlacklist()
        {
            BlacklistList.ItemsSource = null;
            BlacklistList.ItemsSource = ConfigManager.Current.AppProfiles;
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void RadioFilterMode_Changed(object sender, RoutedEventArgs e)
        {
            if (RadioWhitelist == null) return;

            if (RadioWhitelist.IsChecked == true)
            {
                ConfigManager.Current.IsWhitelistMode = true;
                FilterModeHelpText.Text = "Only processes in this list will have auto-scroll enabled.";
            }
            else
            {
                ConfigManager.Current.IsWhitelistMode = false;
                FilterModeHelpText.Text = "Processes in this list will be ignored (auto-scroll disabled).";
            }
            ConfigManager.Save();
        }

        private void HotkeyInput_GotFocus(object sender, RoutedEventArgs e)
        {
            _isRecordingHotkey = true;
            HotkeyInput.Background = new SolidColorBrush(MediaColor.FromRgb(240, 248, 255));
            HotkeyInput.Text = "Press keys...";
        }

        private void HotkeyInput_LostFocus(object sender, RoutedEventArgs e)
        {
            _isRecordingHotkey = false;
            HotkeyInput.Background = (Brush)FindResource("Brush.Control.Background");
            
            if (string.IsNullOrEmpty(_tempHotkey))
                HotkeyInput.Text = ConfigManager.Current.ToggleHotkey;
        }

        private void HotkeyInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_isRecordingHotkey) return;

            e.Handled = true;

            var modifiers = new System.Collections.Generic.List<string>();
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0) modifiers.Add("Ctrl");
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0) modifiers.Add("Alt");
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0) modifiers.Add("Shift");

            var key = e.Key;
            if (key == System.Windows.Input.Key.System) key = e.SystemKey;

            if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
                key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift)
                return;

            string keyStr = key.ToString();
            string result = string.Join("+", modifiers);
            if (!string.IsNullOrEmpty(result)) result += "+";
            result += keyStr;

            _tempHotkey = result;
            HotkeyInput.Text = result;
            
            ConfigManager.Current.ToggleHotkey = result;
            ConfigManager.Save();
        }

        private void ClearHotkey_Click(object sender, RoutedEventArgs e)
        {
            ConfigManager.Current.ToggleHotkey = "";
            ConfigManager.Save();
            HotkeyInput.Text = "";
            _tempHotkey = "";
        }

        private void BrowseApp_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Select Application to Filter"
            };

            if (dialog.ShowDialog() == true)
                AddProcessFromPath(dialog.FileName);
        }

        private void BlacklistList_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        AddProcessFromPath(file);
                }
            }
        }

        private void AddProcessFromPath(string path)
        {
            try
            {
                string processName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrWhiteSpace(processName))
                {
                    _windowManager.AddProfile(processName);
                    RefreshBlacklist();
                }
            }
            catch { }
        }

        private void AddBlacklist_Click(object sender, RoutedEventArgs e)
        {
            string processName = BlacklistInput.Text.Trim();
            if (!string.IsNullOrWhiteSpace(processName))
            {
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    processName = processName.Substring(0, processName.Length - 4);

                _windowManager.AddProfile(processName);
                BlacklistInput.Text = "";
                RefreshBlacklist();
            }
        }

        private void RemoveBlacklist_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string processName)
            {
                _windowManager.RemoveProfile(processName);
                RefreshBlacklist();
            }
        }

        private void TriggerKeyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TriggerKeyCombo == null) return;

            if (TriggerKeyCombo.SelectedItem is ComboBoxItem item && item.Tag is string key)
            {
                ConfigManager.Current.TriggerKey = key;
                ConfigManager.Save();
            }
        }

        private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageCombo == null) return;

            if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string langCode)
            {
                LanguageManager.SetLanguage(langCode);
                ConfigManager.Current.Language = langCode;
                ConfigManager.Save();
            }
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_engine != null)
            {
                _engine.Sensitivity = (float)e.NewValue;
                if (SpeedValueText != null) SpeedValueText.Text = $"{_engine.Sensitivity:F1}x";
                
                ConfigManager.Current.Sensitivity = _engine.Sensitivity;
                ConfigManager.Save();
            }
        }

        private void DeadzoneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_engine != null)
            {
                _engine.Deadzone = (int)e.NewValue;
                if (DeadzoneValueText != null) DeadzoneValueText.Text = $"{_engine.Deadzone}px";

                ConfigManager.Current.Deadzone = _engine.Deadzone;
                ConfigManager.Save();
            }
        }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            try
            {
                var r = await UpdateManager.CheckForUpdatesAsync();

                if (!string.IsNullOrWhiteSpace(r.ErrorMessage))
                {
                    WpfMessageBox.Show(r.ErrorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (r.HasUpdate)
                {
                    var notes = string.IsNullOrWhiteSpace(r.ReleaseNotes)
                        ? ""
                        : "\n\nRelease notes:\n" + Truncate(r.ReleaseNotes, 1200);

                    var result = WpfMessageBox.Show(
                        $"A new version {r.LatestTag} is available!\nCurrent: v{r.CurrentVersion.Major}.{r.CurrentVersion.Minor}.{r.CurrentVersion.Build}\n\nDownload now?{notes}",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        var url = !string.IsNullOrWhiteSpace(r.AssetDownloadUrl) ? r.AssetDownloadUrl : r.ReleasePageUrl;
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = url,
                                UseShellExecute = true
                            });
                        }
                    }
                }
                else
                {
                    WpfMessageBox.Show("You are using the latest version.", "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Failed to check for updates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (btn != null) btn.IsEnabled = true;
            }
        }

        private void OpenGitHub_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/humanfirework/FlowWheel",
                UseShellExecute = true
            });
        }

        private void OpenLicense_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/humanfirework/FlowWheel/blob/main/LICENSE",
                UseShellExecute = true
            });
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "\n...(truncated)";
        }

        private void SetStartup(bool enable)
        {
            try
            {
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                
                if (key == null) return;

                string appName = "FlowWheel";
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                if (enable)
                {
                    key.SetValue(appName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(appName, false);
                }
                
                key.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set startup: {ex.Message}");
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}