using MarketCrawler.Views.Dialogs;
using Neralem.Warframe.Core.DataAcquisition;
using Neralem.Warframe.Core.DOMs;
using Neralem.Wpf.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
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

                        UpdateOrdersCommand.Execute(null);
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
                        Item[] itemsToScanFor = Items.Where(x => x is PrimePart { Ducats: > 15 } or PrimeSet).ToArray();
                        
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
                                OrderCollection orderForItem = await ApiProvider.GetOrdersForItemJsonFromApiAsync(item, Users, OnlineStatus.Undefined);

                                if (orderForItem is null)
                                    itemsFailed++;
                                else
                                {
                                    newOrders.AddRange(orderForItem);
                                    itemsDone++;
                                }
#if DEBUG
                                if (itemsDone >= 10)
                                {
                                    break;
                                }
#endif
                                await Task.Delay(100);
                            }

                            foreach (User user in Users)
                                user.Orders.Clear();

                            foreach (Order order in newOrders)
                                order.User.Orders.Add(order);

                            Users.ToFile(UsersFilename);
                        }
                        finally { IsDownloadingOrders = false; }

                        Orders = newOrders;
                    },
                    _ => !IsDownloadingOrders);
            }
        }

        private ICommand whisperUserCommand;
        public ICommand WhisperUserCommand
        {
            get
            {
                return whisperUserCommand ??= new RelayCommand(
                    param =>
                    {
                        if (param is not Order order)
                            return;

                        Clipboard.SetText($"/w {order.User.Name} Hi! I want to buy your {order.Item.Name} for {order.UnitPrice} :platinum:");
                    },
                    _ => true);
            }
        }

        private ICommand whisperUserBuyAllCommand;
        public ICommand WhisperUserBuyAllCommand
        {
            get
            {
                return whisperUserBuyAllCommand ??= new RelayCommand(
                    param =>
                    {
                        if (param is not Order order)
                            return;

                        Clipboard.SetText(order.Quantity == 2
                            ? $"/w {order.User.Name} Hi! I want to buy your {order.Item.Name} for {order.UnitPrice} :platinum: each. I want both, so...let me do the Math... {order.TotalPlat} :platinum:, right?"
                            : $"/w {order.User.Name} Hi! I want to buy your {order.Item.Name} for {order.UnitPrice} :platinum: each. I want all {order.Quantity}, so...let me do the Math... {order.TotalPlat} :platinum:, right?");
                    },
                    _ => true);
            }
        }
        private ICommand searchCommand;
        public ICommand SearchCommand
        {
            get
            {
                return searchCommand ??= new RelayCommand(
                     _ =>
                    {
                        
                        FilteredOrders = FilterOrders();
                    },
                    _ => true);
            }
        }

        #endregion

        private readonly Progress<OrdersUpdateProgress> _ordersUpdateProgress = new();
        private static string ItemsFilename => "Items.json";
        private static string UsersFilename => "Users.json";
        public MarketApiProvider ApiProvider { get; } = new();
        public bool UpdateAllItems { get; set; } = false;

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
                    Title = FilteredOrders is not null && Orders is not null ? $"Market Crawler - {Orders.Count} Orders ({FilteredOrders.Count}) Shown" : "Market Crawler";
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
                    FilteredOrders = FilterOrders();
                    CollectionViewSource.GetDefaultView(FilteredOrders).SortDescriptions.Add(new SortDescription(nameof(Order.DucatsPerPlatinum), ListSortDirection.Descending));
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
        private ObservableCollection<Item> comboBoxItemsSuggestion = new ObservableCollection<Item>(); 
        public ObservableCollection<Item> ComboBoxItemsSuggestion
        {
            get => comboBoxItemsSuggestion;
            set
            {
                comboBoxItemsSuggestion = value;

            }
        }

        private string searchString = string.Empty;
        public string SearchString
        {
            get => searchString;
            set 
            { 
                if (value != searchString)
                {
                    searchString = value;
                    Item[] temp = items.Where(x => x.Name.ToLower().Contains(searchString.ToLower())).ToArray();
                    foreach(Item it in temp)
                    {
                        if(it != null)
                        {
                            ComboBoxItemsSuggestion?.Add(it);
                        }
                    }
                    OnPropertyChanged();
                    //FilteredOrders = FilterOrders();
                }
            }
        }
    
        #endregion

        public MainVm()
        {
            Items = File.Exists(ItemsFilename) ? ItemCollection.FromFile(ItemsFilename) : new ItemCollection();
            Users = File.Exists(UsersFilename) ? UserCollection.FromFile(UsersFilename) : new UserCollection();
            _ordersUpdateProgress.ProgressChanged += (_, progress) => OrdersUpdateProgress = progress;
        }

        private OrderCollection FilterOrders()
        {
            if (Orders is null)
                return new OrderCollection();

            IEnumerable<Order> filtered = Orders
                .Where(x => x.User.OnlineStatus == OnlineStatus.Ingame)
                .Where(x => x.OrderType == OrderType.Sell)
                .Where(x => x.Quantity <= 20);

            if (!string.IsNullOrWhiteSpace(SearchString))
                filtered = filtered.Where(x => x.Item.Name.Contains(SearchString, StringComparison.InvariantCultureIgnoreCase) || x.User.Name.Contains(SearchString, StringComparison.InvariantCultureIgnoreCase));

            return new OrderCollection(filtered.ToArray());
        }
    }
}