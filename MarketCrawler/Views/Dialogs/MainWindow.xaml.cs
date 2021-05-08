using MarketCrawler.Properties;
using MarketCrawler.ViewModels;
using System.ComponentModel;

namespace Neralem.Warframe.MarketCrawler.Views.Dialogs
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainVm();
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e) => Settings.Default.Save();
    }
}