using System;
using Neralem.Warframe.Core.DOMs;
using Neralem.Wpf.Mvvm;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MarketCrawler.ViewModels
{
    public class OrderViewModel : ViewModelBase
    {
        public Order Order { get; }
        public MyOrdersVm MyOrdersVm { get; }

        public OrderViewModel(Order order, MyOrdersVm myOrdersVm)
        {
            Order = order;
            MyOrdersVm = myOrdersVm;
        }

        protected override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IsModified) && e.PropertyName != nameof(IsChecked))
                IsModified = true;
        }

        public async Task UpdateOrder()
        {
            if (Order.Item.AveragePrice is null)
                return;
            int price = (int)Math.Round(Order.Item.AveragePrice ?? 0);
            if (price == UnitPrice)
                return;
            Order order = await MyOrdersVm.MainVm.ApiProvider.UpdateOrderAsync(Order, price, Quantity, Visible);
            if (order is not null)
                UnitPrice = order.UnitPrice;
        }

        #region Commands

        private ICommand incrementQuantityCommand;
        public ICommand IncrementQuantityCommand => incrementQuantityCommand ??= new RelayCommand(_ => Quantity++);

        private ICommand decrementQuantityCommand;
        public ICommand DecrementQuantityCommand => decrementQuantityCommand ??= new RelayCommand(_ => Quantity--, _ => Quantity > 1);

        private ICommand updateOrderCommand;
        public ICommand UpdateOrderCommand
        {
            get
            {
                return updateOrderCommand ??= new RelayCommand(
                    async _ =>
                    {
                        MyOrdersVm.IsBusy = true;
                        await UpdateOrder();
                        MyOrdersVm.IsBusy = false;
                    },
                    _ => !MyOrdersVm.IsBusy);
            }
        }

        #endregion

        #region Binding Properties

        public int Quantity
        {
            get => Order.Quantity;
            set 
            { 
                if (value != Order.Quantity)
                {
                    Order.Quantity = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int UnitPrice
        {
            get => Order.UnitPrice;
            set 
            { 
                if (value != Order.UnitPrice)
                {
                    Order.UnitPrice = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool Visible
        {
            get => Order.Visible;
            set 
            { 
                if (value != Order.Visible)
                {
                    Order.Visible = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private bool isChecked = true;
        public bool IsChecked
        {
            get => isChecked;
            set 
            { 
                if (value != isChecked)
                {
                    isChecked = value;
                    MyOrdersVm.CheckAllChecked();
                    NotifyPropertyChanged();
                }
            }
        }

        #endregion
    }
}