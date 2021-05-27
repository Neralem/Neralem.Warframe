using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Neralem.Warframe.Core.DOMs
{
    public enum ItemRarity { Undefined, Common, Uncommon, Rare }
    public enum RelicTier { Undefined, Lith, Meso, Neo, Axi }
    public enum ModType { Undefined, Meele, Primary, Secondary, Warframe }
    public enum ModRarity { Undefined, Common, Uncommon, Rare, Primed }
    public enum ArcaneRarity { Undefined, Common, Uncommon, Rare, Legendary }
    public class Item : IEquatable<Item>
    {
        public string Id { get; }
        public string Name { get; set; }
        public string UrlName { get; set; }
        public int MasteryLevel { get; set; }
        public Order[] Orders { get; set; }
        
        
        public virtual double? AveragePrice
        {
            get
            {
                if (Orders is null)
                    return null;

                Order[] orders = Orders
                    .Where(x => x.OrderType == OrderType.Sell)
                    .Where(x => x.Visible)
                    .Where(x => x.User.OnlineStatus is OnlineStatus.Online or OnlineStatus.Ingame)
                    .OrderByDescending(x => x.User.OnlineStatus)
                    .ThenBy(x => x.UnitPrice)
                    .Take(6)
                    .ToArray();

                if (!orders.Any())
                    return null;

                return orders.Select(x => x.UnitPrice).Average();
            }
        }
        
        public Item(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));
            Id = id;
        }

        public bool Equals(Item other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((Item) obj);
        }

        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(Item left, Item right) => Equals(left, right);
        public static bool operator !=(Item left, Item right) => !Equals(left, right);
        public override string ToString() => Name;
    }

    public class PrimePart : Item
    {
        public List<Relic> DropsFrom { get; } = new();
        public int Ducats { get; set; }
        public bool IsVaulted => DropsFrom.All(x => x.IsVaulted);
        public PrimeSet Set { get; set; }
        public PrimePart(string id) : base(id) { }
        public override string ToString() => $"{Name}   [Vaulted: {IsVaulted}]";
    }

    public class PrimeSet : Item
    {
        public List<PrimePart> Parts { get; } = new();
        public int Ducats => Parts.Sum(x => x.Ducats);
        public Relic[] DropsFrom => Parts.SelectMany(x => x.DropsFrom).ToArray();
        public bool IsVaulted => Parts.Any(x => x.IsVaulted);

        public PrimeSet(string id) : base(id) { }
        public override string ToString() => $"[Set]   {Name}   [Vaulted: {IsVaulted}]";
    }

    public class Relic : Item
    {
        public bool IsVaulted { get; set; }
        public Dictionary<Item, ItemRarity> Drops { get; } = new();
        public RelicTier Tier { get; set; }
        public Relic(string id) : base(id) { }
        public override string ToString() => $"{Name}   [Vaulted: {IsVaulted}]";
    }

    
    public class Mod : Item
    {
        public ModType Type { get; set; }
        public ModRarity ModRarity { get; set; }
        public int MaxRank { get; set; }
        
        public Mod(string id) : base(id){}
        public override string ToString() => $"{Name}  [Type: {Type}] [Rarity: {ModRarity}] [MaxRank: {MaxRank}]";
        public override double? AveragePrice
        {
            get
            {
                if (Orders is null)
                    return null;

                Order[] orders = Orders
                    .Where(x => x.OrderType == OrderType.Sell)
                    .Where(x => x.Visible)
                    .Where(x => x.User.OnlineStatus is OnlineStatus.Online or OnlineStatus.Ingame)
                    .Where(x => x.Rank == 0)
                    .OrderByDescending(x => x.User.OnlineStatus)
                    .ThenBy(x => x.UnitPrice)
                    .Take(6)
                    .ToArray();

                if (!orders.Any())
                    return null;

                return orders.Select(x => x.UnitPrice).Average();
            }
        }
    }


    public class Arcane : Item
    {
        public Arcane(string id) : base(id){}
        public ArcaneRarity ArcaneRarity { get; set; }
        public int MaxRank { get; set; }
        public override string ToString() => $"{Name}  [Rarity: {ArcaneRarity}] [MaxRank: {MaxRank}]";
        public override double? AveragePrice
        {
            get
            {
                if (Orders is null)
                    return null;

                Order[] orders = Orders
                    .Where(x => x.OrderType == OrderType.Sell)
                    .Where(x => x.Visible)
                    .Where(x => x.User.OnlineStatus is OnlineStatus.Online or OnlineStatus.Ingame)
                    .Where(x => x.Rank == 0)
                    .OrderByDescending(x => x.User.OnlineStatus)
                    .ThenBy(x => x.UnitPrice)
                    .Take(6)
                    .ToArray();

                if (!orders.Any())
                    return null;

                return orders.Select(x => x.UnitPrice).Average();
            }
        }

    }
}