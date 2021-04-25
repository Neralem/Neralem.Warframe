using System;
using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neralem.Warframe.Core.DataAcquisition.JsonConverter
{
    public class ApiPrimePartJsonConverter : JsonConverter<PrimePart>
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, PrimePart value, JsonSerializer serializer) => throw new NotImplementedException();

        public override PrimePart ReadJson(JsonReader reader, Type objectType, PrimePart existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            string id = jObject["id"]?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(id))
                return null;

            PrimePart part = new(id);
            ApiItemDeserializationHelper.ParseDefaultValues(part, jObject);

            part.Ducats = jObject["ducats"]?.ToObject<int>() ?? 0;

            return part;
        }
    }
}