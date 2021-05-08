﻿using Neralem.Warframe.Core.DOMs;
using Neralem.Wpf.Mvvm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MarketCrawler.ViewModels
{
    public class InventoryVm : ViewModelBase
    {
        private ICommand addEntryCommand;
        public ICommand AddEntryCommand
        {
            get
            {
                return addEntryCommand ??= new RelayCommand(
                    _ =>
                    {
                        if (MainVm.Items.FirstOrDefault(x => x is PrimePart or PrimeSet && x.Name.Equals(ItemText)) is not Item item)
                            return;

                        if (NewEntries.Concat(TrashEntries).FirstOrDefault(x => x.Item.Equals(item)) is InventoryEntryVm entry)
                            entry.Quantity++;
                        else
                            NewEntries.Add(new InventoryEntryVm(this, item));

                        ItemText = "";

                        SaveToFile(MainVm.InventoryFilename);
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
                    _ => true);
            }
        }

        public MainVm MainVm { get; }

        private ObservableCollection<InventoryEntryVm> newEntries = new();
        public ObservableCollection<InventoryEntryVm> NewEntries
        {
            get => newEntries;
            set 
            { 
                if (value != newEntries)
                {
                    newEntries = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private ObservableCollection<InventoryEntryVm> trashEntries = new();
        public ObservableCollection<InventoryEntryVm> TrashEntries
        {
            get => trashEntries;
            set 
            { 
                if (value != trashEntries)
                {
                    trashEntries = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public InventoryVm(MainVm mainVm) => MainVm = mainVm;

        public IEnumerable<string> ItemNames => MainVm.Items.Select(x => x.Name);

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