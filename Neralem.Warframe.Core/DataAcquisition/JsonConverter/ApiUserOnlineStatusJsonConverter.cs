using System;
using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neralem.Warframe.Core.DataAcquisition.JsonConverter
{
    public class ApiUserOnlineStatusJsonConverter : JsonConverter<OnlineStatus>
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, OnlineStatus value, JsonSerializer serializer) => throw new NotImplementedException();

        public override OnlineStatus ReadJson(JsonReader reader, Type objectType, OnlineStatus existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string str = reader.Value as string;
            if (string.IsNullOrWhiteSpace(str))
                return OnlineStatus.Undefined;

            return str.ToLower() switch
            {
                "offline" => OnlineStatus.Offline,
                "online" => OnlineStatus.Online,
                "ingame" => OnlineStatus.Ingame,
                _ => OnlineStatus.Undefined
            };
        }
    }
}