using MarketCrawler.Views.Dialogs;
using Neralem.Warframe.Core.DataAcquisition;
using Neralem.Warframe.Core.DOMs;
using Neralem.Wpf.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Neralem.Wpf;
using Neralem.Wpf.UI.Dialogs;

namespace MarketCrawler.ViewModels
{
    public class MainVm : ViewModelBase
    {
        #region Commands

        private ICommand loginCommand;
        public ICommand LoginCommand
        {
            get
            {
                return loginCommand ??= new RelayCommand(
                    param =>
                    {
                        if (param is not Window window)
                            return;

                        DlgLogin loginDialog = new DlgLogin { Owner = window, DataContext = new LoginVm(ApiProvider, this) };
                        loginDialog.ShowDialog();

                        User user = (loginDialog.DataContext as LoginVm)?.User;
                        if (user is null)
                            return;

                        if (Users.FirstOrDefault(x => x.Equals(user)) is User knownUser)
                        {
                            if (!ReferenceEquals(user, knownUser))
                            {
                                ApiProvider.CurrentUser = knownUser;
                                foreach (Order order in user.Orders)
                                {
                                    order.User = knownUser;
                                    knownUser.Orders.Add(order);
                                }
                                user.Orders.Clear();
                            }
                        }
                        else
                            Users.Add(user);
                    },
                    _ => true);
            }
        }

        private ICommand openItemInMarketCommand;
        public ICommand OpenItemInMarketCommand
        {
            get
            {
                return openItemInMarketCommand ??= new RelayCommand(
                    param =>
                    {
                        if (param is not Item item)
                            return;

                        Process myProcess = new()
                        {
                            StartInfo =
                            {
                                UseShellExecute = true,
                                FileName = $"https://warframe.market/items/{item.UrlName}"
                            }
                        };
                        myProcess.Start();
                    },
                    _ => true);
            }
        }

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
                            CancellationTokenSource updateCtr = new();
                            Progress<ItemUpdateProgress> progress = new();
                            Task<ItemCollection> getItemsTask =
                                ApiProvider.GetItemCatalogFromApiAsync(progress, updateCtr.Token, Items,
                                    UpdateAllItems);
                            ItemsUpdateProgressDialog updateProgressDialog =
                                new() {Owner = Application.Current.MainWindow};
                            ItemsUpdateProgressVm progressVm = new(progress, updateCtr)
                                {Closeable = updateProgressDialog};
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
                        catch (TaskCanceledException)
                        {
                        }
                        catch (HttpRequestException)
                        {
                            ExtMessageBox.Show("Warframe Market Unavailable", "Warframe Market antwortet nicht.",
                                MessageBoxButton.OK, MessageBoxImage.Asterisk);}

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
                                OrderCollection orderForItem = await ApiProvider.GetOrdersForItemAsync(item, Users);

                                if (orderForItem is null)
                                    itemsFailed++;
                                else
                                {
                                    newOrders.AddRange(orderForItem);
                                    itemsDone++;
                                }
#if DEBUG
                                if (itemsDone >= 10)
                                    break;
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

                        foreach (Item item in Items)
                            item.Orders = Orders.Where(x => x.Item == item).ToArray();

                        var c = InventoryVm.TrashEntries;
                        InventoryVm.TrashEntries = null;
                        InventoryVm.TrashEntries = c;
                        c = InventoryVm.NewEntries;
                        InventoryVm.NewEntries = null;
                        InventoryVm.NewEntries = c;

                        FilteredItems = FilterItems();
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
        private ICommand searchOrderCommand;
        public ICommand SearchOrderCommand
        {
            get
            {
                return searchOrderCommand ??= new RelayCommand(
                     param =>
                     {
                         if (param is not string str) return;
                         OrderSearchString = str;
                     },
                    _ => true);
            }
        }

        private ICommand searchDeleteCommand;
        public ICommand SearchDeleteCommand
        {
            get
            {
                return searchDeleteCommand ??= new RelayCommand(
                     _ =>
                     {
                         ItemSearchString = string.Empty;
                         OrderSearchString = string.Empty;
                     },
                    _ => true);
            }
        }
        #endregion

        private readonly Progress<OrdersUpdateProgress> _ordersUpdateProgress = new();
        public static string ItemsFilename => "Items.json";
        public static string UsersFilename => "Users.json";
        public static string InventoryFilename => "Inventory.json";
        public MarketApiProvider ApiProvider { get; } = new();
        public InventoryVm InventoryVm { get; }
        public MyOrdersVm MyOrdersVm { get; }
        public bool UpdateAllItems { get; set; } = false;
        public Debouncer ToolTipDebouncer { get; } = new();

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
                    NotifyPropertyChanged();
                }
            }
        }

        private string popupText;
        public string PopupText
        {
            get => popupText;
            set 
            { 
                if (value != popupText)
                {
                    popupText = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private bool popupVisible;
        public bool PopupVisible
        {
            get => popupVisible;
            set 
            { 
                if (value != popupVisible)
                {
                    popupVisible = value;
                    NotifyPropertyChanged();

                    if (value)
                        ToolTipDebouncer.Debounce(TimeSpan.FromSeconds(2), _ => PopupVisible = false);
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
                    NotifyPropertyChanged();
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
                    FilteredItems = FilterItems();
                    NotifyPropertyChanged();
                }
            }
        }

        private ItemCollection filteredItems;
        public ItemCollection FilteredItems
        {
            get => filteredItems;
            set 
            { 
                if (value != filteredItems)
                {
                    filteredItems = value;
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
                }
            }
        }

        private Debouncer SearchDebouncer { get; } = new ();
        private string orderSearchString = string.Empty;
        public string OrderSearchString
        {
            get => orderSearchString;
            set 
            { 
                if (value != orderSearchString)
                {
                    orderSearchString = value;
                    NotifyPropertyChanged();
                    SearchDebouncer.Debounce(TimeSpan.FromMilliseconds(250), _ => FilteredOrders = FilterOrders());
                }
            }
        }

        private string itemSearchString = string.Empty;
        public string ItemSearchString
        {
            get => itemSearchString;
            set
            {
                if (value != itemSearchString)
                {
                    itemSearchString = value;
                    NotifyPropertyChanged();
                    SearchDebouncer.Debounce(TimeSpan.FromMilliseconds(250), _ => FilteredItems = FilterItems());
                }
            }
        }
        #endregion

        public MainVm()
        {
            Items = File.Exists(ItemsFilename) ? ItemCollection.FromFile(ItemsFilename) : new ItemCollection();
            Users = File.Exists(UsersFilename) ? UserCollection.FromFile(UsersFilename) : new UserCollection();
            InventoryVm = new InventoryVm(this, ApiProvider);
            InventoryVm.LoadFromFile(InventoryFilename);
            MyOrdersVm = new MyOrdersVm(this);
            _ordersUpdateProgress.ProgressChanged += (_, progress) => OrdersUpdateProgress = progress;
        }

        private OrderCollection FilterOrders()
        {
            if (Orders is null)
                return new OrderCollection();

            IEnumerable<Order> filtered = Orders
                .Where(x => !x.User.Blocked)
                .Where(x => x.Visible)
                .Where(x => x.User.OnlineStatus == OnlineStatus.Ingame)
                .Where(x => x.OrderType == OrderType.Sell)
                .Where(x => x.Quantity <= 20);

            if (!string.IsNullOrWhiteSpace(OrderSearchString))
                filtered = filtered.Where(x => x.Item.Name.Contains(OrderSearchString, StringComparison.InvariantCultureIgnoreCase) || x.User.Name.Contains(OrderSearchString, StringComparison.InvariantCultureIgnoreCase));

            return new OrderCollection(filtered.ToArray());
        }

        private ItemCollection FilterItems()
        {
            IEnumerable<Item> filtered = Items.Where(x => x is PrimePart);
            
            if (!string.IsNullOrWhiteSpace(ItemSearchString))
                filtered = filtered.Where(x=> x.Name.Contains(ItemSearchString, StringComparison.InvariantCultureIgnoreCase));
            return new ItemCollection(filtered.ToArray());
        }
    }
}