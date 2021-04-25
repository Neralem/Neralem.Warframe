using System.Collections;
using System.Collections.Generic;
using System.IO;
using Neralem.Warframe.Core.DataStorage.JsonConverter;
using Newtonsoft.Json;

namespace Neralem.Warframe.Core.DOMs
{
    public class ItemCollection : ICollection<Item>
    {
        private List<Item> InternalItems { get; } = new();

        public Item this[int i]
        {
            get => InternalItems[i];
            set => InternalItems[i] = value;
        }

        public ItemCollection() { }
        public ItemCollection(IEnumerable<Item> items) => AddRange(items);

        public void AddRange(IEnumerable<Item> items) => InternalItems.AddRange(items);

        public static ItemCollection FromFile(string filename)
        {
            string data = File.ReadAllText(filename);
            return JsonConvert.DeserializeObject<ItemCollection>(data, new ItemCollectionJsonConverter());
        }

        public void ToFile(string filename) => File.WriteAllText(filename, JsonConvert.SerializeObject(this, Formatting.Indented, new ItemCollectionJsonConverter()));

        #region Implementation of ICollection

        public IEnumerator<Item> GetEnumerator() => InternalItems.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(Item item) => InternalItems.Add(item);
        public void Clear() => InternalItems.Clear();
        public bool Contains(Item item) => InternalItems.Contains(item);
        public void CopyTo(Item[] array, int arrayIndex) => InternalItems.CopyTo(array, arrayIndex);
        public bool Remove(Item item) => InternalItems.Remove(item);
        public int Count => InternalItems.Count;
        public bool IsReadOnly => false;

        #endregion
    }
}