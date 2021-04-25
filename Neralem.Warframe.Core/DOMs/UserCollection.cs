using System.Collections;
using System.Collections.Generic;

namespace Neralem.Warframe.Core.DOMs
{
    public class UserCollection : ICollection<User>
    {
        private List<User> InternalUsers { get; } = new();

        public User this[int i]
        {
            get => InternalUsers[i];
            set => InternalUsers[i] = value;
        }

        public UserCollection() { }
        public UserCollection(IEnumerable<User> users) => AddRange(users);

        public void AddRange(IEnumerable<User> users) => InternalUsers.AddRange(users);

        #region Implementation of ICollection

        public IEnumerator<User> GetEnumerator() => InternalUsers.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(User user) => InternalUsers.Add(user);
        public void Clear() => InternalUsers.Clear();
        public bool Contains(User user) => InternalUsers.Contains(user);
        public void CopyTo(User[] array, int arrayIndex) => InternalUsers.CopyTo(array, arrayIndex);
        public bool Remove(User user) => InternalUsers.Remove(user);
        public int Count => InternalUsers.Count;
        public bool IsReadOnly => false;

        #endregion
    }
}