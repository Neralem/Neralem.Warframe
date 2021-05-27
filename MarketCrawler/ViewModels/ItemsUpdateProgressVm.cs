using Neralem.Warframe.Core.DOMs;
using Neralem.Wpf.Mvvm;
using Neralem.Wpf.Mvvm.Interfaces;
using System;
using System.Threading;
using System.Windows.Input;

namespace MarketCrawler.ViewModels
{
    public class ItemsUpdateProgressVm : ViewModelBase
    {
        private ICommand cancelUpdateCommand;
        public ICommand CancelUpdateCommand
        {
            get
            {
                return cancelUpdateCommand ??= new RelayCommand(
                    _ =>
                    {
                        CancellationTokenSource.Cancel();
                        Closeable.CloseIt(null);
                    },
                    _ => true);
            }
        }

        private ItemUpdateProgress progress;
        public ItemUpdateProgress Progress
        {
            get => progress;
            private set 
            { 
                if (value != progress)
                {
                    progress = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(TotalItemCount));
                    NotifyPropertyChanged(nameof(ProgressPercentage));
                    NotifyPropertyChanged(nameof(TotalItemsToDownload));
                    NotifyPropertyChanged(nameof(TotalItemCount));

                    if (progress.Done)
                        Closeable.CloseIt(null);
                }
            }
        }

        public int TotalItemsToDownload => progress is null ? 0 : progress.ItemsLeft + TotalItemCount;
        public int TotalItemCount => progress is null ? 0 :
            Progress.MiscCount + Progress.PrimePartCount + progress.PrimeSetCount+ progress.ModCount+ progress.ArcaneCount + progress.RelicCount + Progress.ItemsFailed;

        public double ProgressPercentage => (double)TotalItemCount / TotalItemsToDownload;

        public CancellationTokenSource CancellationTokenSource { get; }
        public ICloseable Closeable { get; init; }

        public ItemsUpdateProgressVm(Progress<ItemUpdateProgress> progress, CancellationTokenSource cts)
        {
            CancellationTokenSource = cts;
            progress.ProgressChanged += ProgressOnProgressChanged;
        }

        private void ProgressOnProgressChanged(object sender, ItemUpdateProgress e) => Progress = e;
    }
}