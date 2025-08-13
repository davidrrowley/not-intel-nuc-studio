using Microsoft.UI.Xaml;

namespace NucLedController.WinUI3
{
    public partial class App : Application
    {
        public App()
        {
            // No need to call InitializeComponent() in WinUI 3 App.xaml.cs unless you have defined XAML resources
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }

        private Window m_window;
    }
}
