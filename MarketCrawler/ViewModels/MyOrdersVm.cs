using Neralem.Warframe.Core.DataAcquisition;
using Neralem.Warframe.Core.DOMs;
using Neralem.Wpf.Mvvm;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace MarketCrawler.ViewModels
{
    public class MyOrdersVm : ViewModelBase
    {
        public MainVm MainVm { get; }

        public MyOrdersVm(MainVm mainVm) => MainVm = mainVm;

        public async Task CaptureOrdersAsync()
        {
            if (MainVm.ApiProvider.CurrentUser is null)
                return;
            IsBusy = true;
            MyOrders = await MainVm.ApiProvider.GetOwnOrdersAsync(MainVm.Items);
            IsBusy = false;
        }

        private async Task<bool> DeleteOwnOrderAsync(OrderViewModel orderViewModel)
        {
            if (MainVm.ApiProvider.CurrentUser is null) return false;
            Order delOrder = orderViewModel.Order;
            MainVm.PopupText = $@"Erfolgreich {orderViewModel.Order.Item.Name} Order gelöscht";
            MainVm.PopupVisible = true;
            return await MainVm.ApiProvider.DeleteOwnOrderAsync(delOrder);
        }

        private async Task UpdateOrderPrices()
        {
            IsBusy = true;
            foreach (var orderVm in OrderViewModels.Where(x => x.IsChecked))
            {
                await orderVm.UpdateOrder();
                await Task.Delay(333);
            }
            IsBusy = false;
        }

        public void ResetCollectionView() => CollectionViewSource.GetDefaultView(OrderViewModels)?.Refresh();

        #region Commands

        private ICommand getOrdersCommand;
        public ICommand GetOrdersCommand
        {
            get
            {
                return getOrdersCommand ??= new RelayCommand(
                    async _ => await CaptureOrdersAsync(),
                    _ => !IsBusy && MainVm.ApiProvider.CurrentUser is not null);
            }
        }

        private ICommand deleteOwnOrderCommand;
        public ICommand DeleteOwnOrderCommand
        {
            get
            {
                return deleteOwnOrderCommand ??= new RelayCommand(
                    async param =>
                    {
                        if (param is not OrderViewModel vm) return;
                        if (await DeleteOwnOrderAsync(vm))
                        {
                            MyOrders.Remove(vm.Order);
                            OrderViewModels = CreateOrderViewModels();
                        }

                    },
                    _=>!IsBusy && MyOrders!=null);

            }
        }
        private ICommand updateOrderPricesCommand;
        public ICommand UpdateOrderPricesCommand
        {
            get
            {
                return updateOrderPricesCommand ??= new RelayCommand(
                    async _ => await UpdateOrderPrices(),
                    _ => !IsBusy && (OrderViewModels?.Any(x => x.IsChecked) ?? false));
            }
        }

        #endregion

        #region Binding Properties

        private OrderCollection myOrders;
        public OrderCollection MyOrders
        {
            get => myOrders;
            set 
            {
                if (value != myOrders)
                {
                    myOrders = value;
                    OrderViewModels = CreateOrderViewModels();
                    NotifyPropertyChanged();
                    
                }
            }
        }

        private OrderViewModel[] orderViewModels;
        public OrderViewModel[] OrderViewModels
        {
            get => orderViewModels;
            private set 
            { 
                if (value != orderViewModels)
                {
                    orderViewModels = value;
                    CheckAllChecked();
                    NotifyPropertyChanged();
                }
            }
        }

        private bool isBusy;
        public bool IsBusy
        {
            get => isBusy;
            set 
            { 
                if (value != isBusy)
                {
                    isBusy = value;
                    NotifyPropertyChanged();
                    (UpdateOrderPricesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (GetOrdersCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private bool? allChecked =false;
        public bool? AllChecked
        {
            get => allChecked;
            set
            {
                if (value == true)
                {
                    allChecked = true;
                    foreach (var orders in orderViewModels.Where(x => !x.IsChecked))
                    {
                        orders.IsChecked = true;
                    }
                }
                else if (value == false)
                {
                    allChecked = false;
                    foreach (var orders in orderViewModels.Where(x => x.IsChecked))
                    {
                        orders.IsChecked = false;
                    }
                }
                else
                {
                    allChecked = null;
                }

                NotifyPropertyChanged();
            }
        }

        public OrderViewModel[] CreateOrderViewModels()
        {
            return MyOrders is null
                ? Array.Empty<OrderViewModel>()
                : MyOrders.Where(x => x.OrderType == OrderType.Sell).Select(x => new OrderViewModel(x, this)).ToArray();
            
        }
        public void CheckAllChecked()
        {
            if (orderViewModels.All(x => x.IsChecked))
                AllChecked = true;
            else if (orderViewModels.Any(x => x.IsChecked))
                AllChecked = null;
            else
                AllChecked = false;
        }

        #endregion
    }
}