using System.Collections;
using System.Collections.Generic;

namespace Neralem.Warframe.Core.DOMs
{
    public class OrderCollection : ICollection<Order>
    {
        private List<Order> InternalOrders { get; } = new();

        public Order this[int i]
        {
            get => InternalOrders[i];
            set => InternalOrders[i] = value;
        }

        public OrderCollection() { }
        public OrderCollection(IEnumerable<Order> orders) => AddRange(orders);

        public void AddRange(IEnumerable<Order> orders) => InternalOrders.AddRange(orders);

        #region Implementation of ICollection

        public IEnumerator<Order> GetEnumerator() => InternalOrders.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(Order order) => InternalOrders.Add(order);
        public void Clear() => InternalOrders.Clear();
        public bool Contains(Order order) => InternalOrders.Contains(order);
        public void CopyTo(Order[] array, int arrayIndex) => InternalOrders.CopyTo(array, arrayIndex);
        public bool Remove(Order order) => InternalOrders.Remove(order);
        public int Count => InternalOrders.Count;
        public bool IsReadOnly => false;

        #endregion
    }
}