using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NucLedController.WinUI3.Views;
using System;

namespace NucLedController.WinUI3
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Intel NUC Software Studio";
            
            // Navigate to the main page on startup
            ContentFrame.Navigate(typeof(MainPage));
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception($"Failed to load Page {e.SourcePageType.Name}");
        }
    }
}
