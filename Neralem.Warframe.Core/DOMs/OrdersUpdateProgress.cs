namespace Neralem.Warframe.Core.DOMs
{
    public class OrdersUpdateProgress
    {
        public int ItemsLeft => TotalItemsToScan - ItemsScanned + ItemsFailed;
        public int ItemsScanned { get; init; }
        public int ItemsFailed { get; init; }
        public int TotalItemsToScan { get; set; }
        public double PercentageDone => (double) (ItemsScanned + ItemsFailed) / TotalItemsToScan;
        public string CurrentItemName { get; init; }
    }
}