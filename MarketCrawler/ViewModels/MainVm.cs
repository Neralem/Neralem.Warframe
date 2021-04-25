using MarketCrawler.Views.Dialogs;
using Neralem.Warframe.Core.DataAcquisition;
using Neralem.Warframe.Core.DOMs;
using Neralem.Wpf.Mvvm;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MarketCrawler.ViewModels
{
    public class MainVm : ViewModelBase
    {
        #region Commands

        private ICommand updateItemsCommand;
        public ICommand UpdateItemsCommand
        {
            get
            {
                return updateItemsCommand ??= new RelayCommand(
                    async _ =>
                    {
                        try
                        {
                            CancellationTokenSource updateCtr = new CancellationTokenSource();
                            Progress<ItemUpdateProgress> progress = new();
                            Task<ItemCollection> getItemsTask = ApiProvider.GetItemCatalogFromApiAsync(progress, updateCtr.Token, Items, UpdateAllItems);
                            ItemsUpdateProgressDialog updateProgressDialog = new() { Owner = Application.Current.MainWindow };
                            ItemsUpdateProgressViewModel progressVm = new(progress, updateCtr) { Closeable = updateProgressDialog };
                            updateProgressDialog.DataContext = progressVm;
                            updateProgressDialog.ShowDialog();
                            ItemCollection newItems = await getItemsTask;
                            if (newItems?.Any() ?? false)
                            {
                                if (!UpdateAllItems)
                                {
                                    Items.AddRange(newItems);
                                    Items = new ItemCollection(Items.OrderBy(x => x.Name));
                                }
                                else
                                    Items = new ItemCollection(newItems.OrderBy(x => x.Name));
                            }
                            await ApiProvider.SetVaultedRelics(Items.OfType<Relic>().ToArray());
                            Items.ToFile(ItemsFilename);
                        }
                        catch (TaskCanceledException) { }
                    },
                    _ => true);
            }
        }

        private ICommand updateOrdersCommand;
        public ICommand UpdateOrdersCommand
        {
            get
            {
                return updateOrdersCommand ??= new RelayCommand(
                    async _ =>
                    {
                        IsDownloadingOrders = true;

                        OrderCollection newOrders = new();
                        UserCollection newUsers = Users ?? new UserCollection();
                        Item[] itemsToScanFor = Items.Where(x => x is PrimePart or PrimeSet).ToArray();
                        
                        int itemsFailed = 0, itemsDone = 0;
                        try
                        {
                            foreach (Item item in itemsToScanFor)
                            {
                                (_ordersUpdateProgress as IProgress<OrdersUpdateProgress>).Report(new OrdersUpdateProgress
                                {
                                    CurrentItemName = item.Name,
                                    ItemsFailed = itemsFailed,
                                    ItemsScanned = itemsDone,
                                    TotalItemsToScan = itemsToScanFor.Length
                                });
                                OrderCollection orderForItem = await ApiProvider.GetOrdersForItemJsonFromApiAsync(item, newUsers, OnlineStatus.Undefined);

                                if (orderForItem is null)
                                    itemsFailed++;
                                else
                                {
                                    newOrders.AddRange(orderForItem);
                                    itemsDone++;
                                }
                                await Task.Delay(333);
                            }

                            foreach (User user in newUsers)
                                user.Orders.Clear();

                            foreach (Order order in newOrders)
                                order.User.Orders.Add(order);
                        }
                        finally { IsDownloadingOrders = false; }

                        Orders = newOrders;
                        Users = newUsers;
                    },
                    _ => !IsDownloadingOrders);
            }
        }

        #endregion

        private readonly Progress<OrdersUpdateProgress> _ordersUpdateProgress = new();
        private static string ItemsFilename => "Items.json";
        public MarketApiProvider ApiProvider { get; } = new();
        public bool UpdateAllItems { get; set; } = false;

        public MainVm()
        {
            Items = File.Exists(ItemsFilename) ? ItemCollection.FromFile(ItemsFilename) : new ItemCollection();
            _ordersUpdateProgress.ProgressChanged += (_, progress) => OrdersUpdateProgress = progress;
        }

        #region Binding Properties

        private string title = "Market Crawler";
        public string Title
        {
            get => title;
            set 
            { 
                if (value != title)
                {
                    title = value;
                    OnPropertyChanged();
                }
            }
        }

        private OrdersUpdateProgress ordersUpdateProgress;
        public OrdersUpdateProgress OrdersUpdateProgress
        {
            get => ordersUpdateProgress;
            set 
            { 
                if (value != ordersUpdateProgress)
                {
                    ordersUpdateProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        private ItemCollection items;
        public ItemCollection Items
        {
            get => items;
            set 
            { 
                if (value != items)
                {
                    items = value;
                    OnPropertyChanged();
                }
            }
        }

        private OrderCollection filteredOrders;
        public OrderCollection FilteredOrders
        {
            get => filteredOrders;
            set 
            { 
                if (value != filteredOrders)
                {
                    filteredOrders = value;
                    OnPropertyChanged();
                    Title = $"Market Crawler - {Orders.Count} Orders ({FilteredOrders.Count}) Shown";
                }
            }
        }

        private OrderCollection orders;
        public OrderCollection Orders
        {
            get => orders;
            set 
            { 
                if (value != orders)
                {
                    orders = value;
                    OnPropertyChanged();
                    FilteredOrders = new OrderCollection(Orders.Where(x => x.User.OnlineStatus == OnlineStatus.Ingame && x.Quantity <= 20).ToArray());
                }
            }
        }

        private UserCollection users;
        public UserCollection Users
        {
            get => users;
            set 
            { 
                if (value != users)
                {
                    users = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool isDownloadingOrders;
        public bool IsDownloadingOrders
        {
            get => isDownloadingOrders;
            set 
            { 
                if (value != isDownloadingOrders)
                {
                    isDownloadingOrders = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion
    }
}