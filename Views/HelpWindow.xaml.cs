using System;
using System.Windows;
using System.Windows.Navigation;
using PautaDinamicaApp.ViewModels;

namespace PautaDinamicaApp.Views
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
            this.Loaded += HelpWindow_Loaded;
        }

        private void HelpWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HelpViewModel vm)
            {
                HelpBrowser.NavigateToString(vm.HtmlContent);
            }
        }

        private void WebBrowser_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            // Open links in external browser instead of inside the window
            if (e.Uri != null && e.Uri.Scheme.StartsWith("http"))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Cancel = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
