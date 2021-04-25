using System;
using System.Collections.Generic;

namespace Neralem.Warframe.Core.DOMs
{
    public enum OnlineStatus { Undefined, Offline, Online, Ingame }

    public class User : IEquatable<User>
    {
        public string Id { get; }
        public string Name { get; set; }
        public OnlineStatus OnlineStatus { get; set; }
        public int Reputation { get; set; }
        public DateTime? LastSeen { get; set; }
        public List<Order> Orders { get; } = new();

        public User(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
            Id = id;
            Name = name;
        }

        public bool Equals(User other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((User) obj);
        }

        public override int GetHashCode() => (Id != null ? Id.GetHashCode() : 0);
        public static bool operator ==(User left, User right) => Equals(left, right);
        public static bool operator !=(User left, User right) => !Equals(left, right);
        public override string ToString() => Name;
    }
}