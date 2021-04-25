using System;
using System.Collections.Generic;
using System.Linq;

namespace Neralem.Warframe.Core.DOMs
{
    public enum ItemRarity { Undefined, Common, Uncommon, Rare }
    public enum RelicTier { Undefined, Lith, Meso, Neo, Axi }

    public abstract class Item : IEquatable<Item>
    {
        public string Id { get; }
        public string Name { get; set; }
        public string UrlName { get; set; }
        public int MasteryLevel { get; set; }

        protected Item(string id)
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
        public PrimeSet Set { get; set; }
        public bool IsVaulted => DropsFrom.Any(x => !x.IsVaulted);

        public PrimePart(string id) : base(id) { }
        public override string ToString() => $"{Name}   [Vaulted: {IsVaulted}]";
    }

    public class PrimeSet : Item
    {
        public List<PrimePart> Parts { get; } = new();
        public int Ducats => Parts.Sum(x => x.Ducats);
        public Relic[] DropsFrom => Parts.SelectMany(x => x.DropsFrom).ToArray();
        public bool IsVaulted => Parts.Any(x => !x.IsVaulted);

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
}