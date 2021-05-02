using System;
using System.Linq;
using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neralem.Warframe.Core.DataStorage.JsonConverter
{
    public class UserCollectionJsonConverter : JsonConverter<UserCollection>
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override void WriteJson(JsonWriter writer, UserCollection value, JsonSerializer serializer)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));
            serializer.Converters.Add(new LocalUserJsonConverter());
            serializer.Serialize(writer, JArray.FromObject(value.ToArray(), serializer));
        }

        public override UserCollection ReadJson(JsonReader reader, Type objectType, UserCollection existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JArray userJArray = JArray.Load(reader);
            serializer.Converters.Add(new LocalUserJsonConverter());

            UserCollection userCollection = new UserCollection();

            foreach (JToken jUser in userJArray)
            {
                User user = jUser.ToObject<User>(serializer);

                if (user is not null)
                    userCollection.Add(user);
            }

            return userCollection;
        }
    }
}