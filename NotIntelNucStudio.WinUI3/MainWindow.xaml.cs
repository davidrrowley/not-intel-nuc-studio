using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NotIntelNucStudio.WinUI3.Views;
using System;

namespace NotIntelNucStudio.WinUI3
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Not Intel NUC Studio";
            
            // Navigate to the main page on startup
            ContentFrame.Navigate(typeof(MainPage));
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception($"Failed to load Page {e.SourcePageType.Name}");
        }
    }
}
