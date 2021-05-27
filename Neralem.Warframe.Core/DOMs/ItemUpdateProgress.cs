namespace Neralem.Warframe.Core.DOMs
{
    public class ItemUpdateProgress
    {
        public bool Done { get; init; }

        public int ItemsLeft { get; init; }
        public int ItemsFailed { get; init; }
        public string CurrentItemUrlName { get; init; }

        public int RelicCount { get; init; }
        public int PrimePartCount { get; init; }
        public int PrimeSetCount { get; init; }
        public int ModCount { get; init; }
        public int ArcaneCount { get; init; }
        public int MiscCount { get; init; }
    }
}