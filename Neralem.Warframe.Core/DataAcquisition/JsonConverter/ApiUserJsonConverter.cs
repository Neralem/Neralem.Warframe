using System;
using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neralem.Warframe.Core.DataAcquisition.JsonConverter
{
    public class ApiUserJsonConverter : JsonConverter<User>
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, User value, JsonSerializer serializer) => throw new NotImplementedException();

        public override User ReadJson(JsonReader reader, Type objectType, User existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            serializer.Converters.Add(new ApiUserOnlineStatusJsonConverter());
            string id = jObject["id"]?.ToObject<string>();
            string name = jObject["ingame_name"]?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                return null;

            if (jObject["reputation"]?.ToObject<int>() is not int reputation || 
                jObject["status"]?.ToObject<OnlineStatus>() is not OnlineStatus onlineStatus || onlineStatus == OnlineStatus.Undefined)
                return null;

            return new User(id, name)
            {
                Reputation = reputation,
                LastSeen = jObject["last_seen"]?.ToObject<DateTime?>(),
                OnlineStatus = onlineStatus
            };
        }
    }
}