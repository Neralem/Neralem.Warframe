﻿using System;
using System.Diagnostics;
using System.Windows.Input;
using Neralem.Warframe.Core.DOMs;
using Neralem.Wpf.Mvvm;

namespace MarketCrawler.ViewModels
{
    public class InventoryEntryVm : ViewModelBase
    {
        private ICommand openItemInMarketCommand;
        public ICommand OpenItemInMarketCommand
        {
            get
            {
                return openItemInMarketCommand ??= new RelayCommand(
                    _ =>
                    {
                        Process myProcess = new()
                        {
                            StartInfo =
                            {
                                UseShellExecute = true,
                                FileName = $"https://warframe.market/items/{Item.UrlName}"
                            }
                        };
                        myProcess.Start();
                    },
                    _ => true);
            }
        }

        private ICommand incrementQuantityCommand;
        public ICommand IncrementQuantityCommand
        {
            get
            {
                return incrementQuantityCommand ??= new RelayCommand(
                    _ => Quantity++,
                    _ => true);
            }
        }

        private ICommand decrementQuantityCommand;
        public ICommand DecrementQuantityCommand
        {
            get
            {
                return decrementQuantityCommand ??= new RelayCommand(
                    _ => Quantity--,
                    _ => Quantity > 1);
            }
        }

        public InventoryVm Inventory { get; }
        public Item Item { get; }

        private int quantity = 1;
        public int Quantity
        {
            get => quantity;
            set 
            { 
                if (value != quantity)
                {
                    quantity = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public InventoryEntryVm(InventoryVm inventory, Item item)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Item = item ?? throw new ArgumentNullException(nameof(item));
        }

        public override string ToString() => $"{Quantity}x {Item.Name}";
    }
}