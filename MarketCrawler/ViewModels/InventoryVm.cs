﻿using Neralem.Warframe.Core.DOMs;
using Neralem.Wpf.Mvvm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Neralem.Warframe.Core.DataAcquisition;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Neralem.Wpf.UI.Dialogs;
using System.Windows;
using System.Windows.Data;

namespace MarketCrawler.ViewModels
{
    public class InventoryVm : ViewModelBase
    {
        private ICommand addEntryCommand;
        public ICommand AddEntryCommand
        {
            get
            {
                return addEntryCommand ??= new RelayCommand(async param =>
                    {
                        if (param is not string itemName || string.IsNullOrWhiteSpace(itemName)) 
                            return;
                        if (MainVm.Items.FirstOrDefault(x => x is PrimePart or PrimeSet or Mod && x.Name.Equals(itemName)) is not { } item)
                            return;

                        await AddEntryAsync(item, 1);
                    },
                    _ => true);
            }
        }

        private ICommand removeEntryCommand;
        public ICommand RemoveEntryCommand
        {
            get
            {
                return removeEntryCommand ??= new RelayCommand(
                    param =>
                    {
                        if (param is not InventoryEntryVm entry)
                            return;

                        if (TrashEntries.Contains(entry))
                            TrashEntries.Remove(entry);
                        else if (NewEntries.Contains(entry))
                            NewEntries.Remove(entry);

                        SaveToFile(MainVm.InventoryFilename);
                    },
                    _ => true);
            }
        }

        private ICommand moveEntryCommand;
        public ICommand MoveEntryCommand
        {
            get
            {
                return moveEntryCommand ??= new RelayCommand(
                    param =>
                    {
                        if (param is not InventoryEntryVm entry)
                            return;

                        if (NewEntries.Contains(entry))
                        {
                            NewEntries.Remove(entry);
                            if (TrashEntries.FirstOrDefault(x => x.Item.Equals(entry.Item)) is InventoryEntryVm existingEntry)
                                existingEntry.Quantity += entry.Quantity;
                            else
                                TrashEntries.Add(entry);
                        }
                        else if (TrashEntries.Contains(entry))
                        {
                            TrashEntries.Remove(entry);
                            if (NewEntries.FirstOrDefault(x => x.Item.Equals(entry.Item)) is InventoryEntryVm existingEntry)
                                existingEntry.Quantity += entry.Quantity;
                            else
                                NewEntries.Add(entry);
                        }

                        SaveToFile(MainVm.InventoryFilename);
                    },
                    param => param is InventoryEntryVm entry && entry.Item is PrimePart or PrimeSet);
            }
        }

        private ICommand listItemsForAveragePriceCommand;
        public ICommand ListItemsForAveragePriceCommand
        {
            get
            {
                return listItemsForAveragePriceCommand ??= new RelayCommand(
                    async param =>
                    {
                        InventoryEntryVm[] entriesToUpload = NewEntries.Where(x => x.IsChecked).ToArray();
                        if (!entriesToUpload.Any())
                            return;

                        if (entriesToUpload.Any(x => x.Item.AveragePrice is null))
                            ExtMessageBox.Show("Keine Daten", "Es müssen erst die Durchschnittspreise erfasst werden.", MessageBoxButton.OK, MessageBoxImage.Error, param as Window);

                        
                        try
                        {
                            OrderCollection myOrders = await ApiProvider.GetOwnOrdersAsync(MainVm.Items);

                            bool updatePrices = false;

                            foreach (InventoryEntryVm entry in entriesToUpload)
                            {
                                Order order = myOrders.FirstOrDefault(x => x.Item.Equals(entry.Item));
                                await Task.Delay(333);

                                if (order is not null && order.UnitPrice != Math.Round(entry.Item.AveragePrice ?? 0))
                                {
                                    MessageBoxResult result = ExtMessageBox.Show(
                                        "Preiskonflikt",
                                        "Für mindestens eines der Items die eingestellt werden soll existiert bereits eine Order und deren Preis unterscheidet sich vom aktuellen Durchschnittspreis. Soll der Preis mit dem aktuellen Durchschnittspreis angepasst werden?",
                                        MessageBoxButton.YesNoCancel,
                                        MessageBoxImage.Question,
                                        param as Window);

                                    switch (result)
                                    {
                                        case MessageBoxResult.Cancel:
                                            return;
                                        case MessageBoxResult.Yes:
                                            updatePrices = true;
                                            break;
                                        case MessageBoxResult.No:
                                            break;
                                        default:
                                            throw new ArgumentOutOfRangeException();
                                    }

                                    break;
                                }
                            }

                            foreach (InventoryEntryVm entry in entriesToUpload)
                            {
                                Order existingOrder = myOrders.FirstOrDefault(x => x.Item == entry.Item);
                                int averagePrice = (int) Math.Round(entry.Item.AveragePrice ?? 0);
                                Order newOrder = existingOrder is null
                                    ? await ApiProvider.CreateOrderAsync(entry.Item, averagePrice, entry.Quantity)
                                    : await ApiProvider.UpdateOrderAsync(existingOrder,
                                        updatePrices ? averagePrice : existingOrder.UnitPrice,
                                        entry.Quantity + existingOrder.Quantity, existingOrder.Visible);

                                if (newOrder is not null)
                                {
                                    NewEntries.Remove(entry);
                                    SaveToFile(MainVm.InventoryFilename);
                                }

                                await Task.Delay(333);
                            }
                        }
                        catch (AccessViolationException e)
                        {
                            Debug.WriteLine(e);
                            ExtMessageBox.Show("Error", "Sie müssen eingeloggt sein, um Orders zu erstellen",
                                MessageBoxButton.OK, MessageBoxImage.Error, param as Window);
                        }
                    },
                    _ => !IsUploadingOrders && NewEntries.Any(x => x.IsChecked));
            }
        }

        public MainVm MainVm { get; }
        public MarketApiProvider ApiProvider { get; }

        private ObservableCollection<InventoryEntryVm> newEntries;
        public ObservableCollection<InventoryEntryVm> NewEntries
        {
            get => newEntries;
            set 
            { 
                if (value != newEntries)
                {
                    if (newEntries != null)
                        newEntries.CollectionChanged -=NewEntriesOnCollectionChanged;
                    newEntries = value;
                    if (newEntries != null)
                        newEntries.CollectionChanged += NewEntriesOnCollectionChanged;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(NewItemsPlat));
                }
            }
        }

        private void NewEntriesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => NotifyPropertyChanged(nameof(NewItemsPlat));

        private ObservableCollection<InventoryEntryVm> trashEntries;
        public ObservableCollection<InventoryEntryVm> TrashEntries
        {
            get => trashEntries;
            set 
            { 
                if (value != trashEntries)
                {
                    if (trashEntries != null)
                        trashEntries.CollectionChanged -= TrashEntriesOnCollectionChanged;
                    trashEntries = value;
                    if (trashEntries != null)
                        trashEntries.CollectionChanged += TrashEntriesOnCollectionChanged;
                    NotifyPropertyChanged();
                }
            }
        }

        private void TrashEntriesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => NotifyPropertyChanged(nameof(TrashItemsDucats));

        public InventoryVm(MainVm mainVm, MarketApiProvider apiProvider)
        {
            MainVm = mainVm;
            ApiProvider = apiProvider;
            NewEntries = new ObservableCollection<InventoryEntryVm>();
            TrashEntries = new ObservableCollection<InventoryEntryVm>();
        }

        public void ResetCollectionView()
        {
            CollectionViewSource.GetDefaultView(NewEntries)?.Refresh();
            CollectionViewSource.GetDefaultView(TrashEntries)?.Refresh();
            NotifyPropertyChanged(nameof(NewItemsPlat));
        }

        public IEnumerable<string> ItemNames => MainVm.Items.Select(x => x.Name);

        public int NewItemsPlat => NewEntries?.Sum(x => (int)(x.Item.AveragePrice ?? 0) * x.Quantity) ?? 0;

        public int TrashItemsDucats =>
            TrashEntries.Where(x => x.Item is PrimePart).Sum(x => (x.Item as PrimePart)?.Ducats * x.Quantity) +
            TrashEntries.Where(x => x.Item is PrimeSet).Sum(x => (x.Item as PrimeSet)?.Ducats * x.Quantity) ?? 0;

        private string itemText;
        public string ItemText
        {
            get => itemText;
            set 
            { 
                if (value != itemText)
                {
                    itemText = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private bool isUploadingOrders;
        public bool IsUploadingOrders
        {
            get => isUploadingOrders;
            set 
            { 
                if (value != isUploadingOrders)
                {
                    isUploadingOrders = value;
                    NotifyPropertyChanged();
                }
            }
        }
        private bool? allChecked=false;
        public bool? AllChecked
        {
            get => allChecked;
            set
            {
                
                if (value ==true)
                {
                    allChecked = true;
                    foreach (var items in NewEntries.Where(x => !x.IsChecked))
                    {
                        items.IsChecked = true;
                    }
                }
                else if(value ==false)
                {
                    allChecked = false;
                    foreach (var items in NewEntries.Where(x => x.IsChecked))
                    {
                        items.IsChecked = false;
                    }
                }
                else
                {
                    allChecked = null;
                }

                NotifyPropertyChanged();
            }
        }

        public void CheckAllChecked()
        {
            if (NewEntries.All(x=>x.IsChecked))
                AllChecked = true;
            else if (NewEntries.Any(x => x.IsChecked))
                AllChecked = null;
            else
                AllChecked=false;
        }

        public async Task AddEntryAsync(Item item, int amount)
        {
            if (MainVm.ApiProvider.CurrentUser is not null)
            {
                while (!MainVm.MyOrdersVm.GetOrdersCommand.CanExecute(null))
                    await Task.Delay(500);

                await MainVm.MyOrdersVm.CaptureOrdersAsync();

                OrderViewModel orderVm = MainVm.MyOrdersVm.OrderViewModels.FirstOrDefault(x => x.Order.Item == item);

                if (orderVm is not null)
                {
                    await MainVm.ApiProvider.UpdateOrderAsync(orderVm.Order, orderVm.UnitPrice, orderVm.Quantity + amount, orderVm.Visible);
                    orderVm.Quantity += amount;
                    ItemText = "";

                    MainVm.PopupText = $"{amount}x \"{item.Name}\" zu bestehender Order hinzugefügt";
                    MainVm.PopupVisible = true;
                    return;
                }
            }

            if (NewEntries.Concat(TrashEntries).FirstOrDefault(x => x.Item.Equals(item)) is { } entry)
                entry.Quantity++;
            else
                NewEntries.Add(new InventoryEntryVm(this, item) { Quantity = amount });

            ItemText = "";

            MainVm.PopupText = $"{amount}x \"{item.Name}\" hinzugefügt";
            MainVm.PopupVisible = true;

            SaveToFile(MainVm.InventoryFilename);
        }

        public void SaveToFile(string filename)
        {
            JObject jObject = new();

            jObject.Add("trashEntries", JArray.FromObject(TrashEntries.Select(x => new { itemId = x.Item.Id, quantity = x.Quantity } )));
            jObject.Add("newEntries", JArray.FromObject(NewEntries.Select(x => new { itemId = x.Item.Id, quantity = x.Quantity } )));

            File.WriteAllText(filename, JsonConvert.SerializeObject(jObject));
        }

        public void LoadFromFile(string filename)
        {
            InventoryEntryVm ParseEntry(JToken jEntry)
            {
                if (jEntry["itemId"].ToObject<string>() is string id && !string.IsNullOrWhiteSpace(id) && MainVm.Items.FirstOrDefault(x => x.Id.Equals(id)) is Item item && jEntry["quantity"].ToObject<int>() is var quantity and > 0)
                    return new InventoryEntryVm(this, item) {Quantity = quantity};

                return null;
            }

            if (!File.Exists(filename))
                return;

            JObject jObject = JObject.Parse(File.ReadAllText(filename));

            if (jObject["trashEntries"]?.ToObject<JArray>() is JArray trashEntriesJArray)
            {
                foreach (JToken trashEntry in trashEntriesJArray)
                    if (ParseEntry(trashEntry) is InventoryEntryVm entry)
                        TrashEntries.Add(entry);
            }

            if (jObject["newEntries"]?.ToObject<JArray>() is JArray newEntriesJArray)
            {
                foreach (JToken newEntry in newEntriesJArray)
                    if (ParseEntry(newEntry) is InventoryEntryVm entry)
                        NewEntries.Add(entry);
            }
        }
    }
}