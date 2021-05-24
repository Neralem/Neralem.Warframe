using System;
using System.Dynamic;

namespace Neralem.Warframe.Core.DOMs
{
    public enum OrderType { Undefined, Sell, Buy }

    public class Order : IEquatable<Order>
    {
        public string Id { get; }
        public int Quantity { get; set; }
        public int UnitPrice { get; set; }
        public int Rank { get; set; }
        public bool Visible { get; set; }
        public OrderType OrderType { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime ModificationDate { get; set; }
        public Item Item { get; set; }
        public User User { get; set; }

        public int TotalPlat => Quantity * UnitPrice;

        public double DucatsPerPlatinum
        {
            get
            {
                int ducats = Item switch
                {
                    PrimePart primePart => primePart.Ducats,
                    PrimeSet primeSet => primeSet.Ducats,
                    _ => 0
                };
                if (ducats == 0)
                    return 0;
                return (double) ducats / UnitPrice;
            }
        }

        public Order(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(id));
            Id = id;
        }

        public bool Equals(Order other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((Order) obj);
        }

        public override int GetHashCode() => (Id != null ? Id.GetHashCode() : 0);
        public static bool operator ==(Order left, Order right) => Equals(left, right);
        public static bool operator !=(Order left, Order right) => !Equals(left, right);
        public override string ToString()
        {
            string type = null;
            switch (OrderType)
            {
                case OrderType.Undefined:
                    type = "[Undefined]";
                    break;
                case OrderType.Sell:
                    type = "[Sell]";
                    break;
                case OrderType.Buy:
                    type = "[Buy]";
                    break;
            }
            return $"{type} {Quantity}x {Item} @{UnitPrice} plat from \"{User}\"";
        }
    }
}