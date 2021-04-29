namespace MarketCrawler.Models
{
    public record BlockedUser
    {
        public string Id { get; }

        public string Name { get; }

        public BlockedUser(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}