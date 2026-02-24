using System;
using System.Linq;
using System.Windows;

namespace FlowWheel.Core
{
    public static class ThemeManager
    {
        public static void ApplyTheme(bool isDark)
        {
            var appResources = System.Windows.Application.Current.Resources;
            var mergedDictionaries = appResources.MergedDictionaries;

            // Find the existing theme dictionary (if any)
            var themeDictionary = mergedDictionaries.FirstOrDefault(d => 
                d.Source != null && (d.Source.OriginalString.Contains("Themes/Light.xaml") || 
                                     d.Source.OriginalString.Contains("Themes/Dark.xaml")));

            // Create the new theme dictionary
            var newThemeSource = new Uri(isDark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);
            var newThemeDictionary = new ResourceDictionary { Source = newThemeSource };

            // Replace or add
            if (themeDictionary != null)
            {
                mergedDictionaries.Remove(themeDictionary);
            }
            mergedDictionaries.Add(newThemeDictionary);
            
            // Update Config
            if (ConfigManager.Current.IsDarkMode != isDark)
            {
                ConfigManager.Current.IsDarkMode = isDark;
                ConfigManager.Save();
            }
        }
    }
}
