using System.Linq;
using MarketCrawler.ViewModels;

namespace Neralem.Warframe.MarketCrawler.Views.Dialogs
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainVm();

        }

    }
}