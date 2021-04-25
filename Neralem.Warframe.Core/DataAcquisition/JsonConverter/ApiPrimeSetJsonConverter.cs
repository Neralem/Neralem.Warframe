using System;
using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neralem.Warframe.Core.DataAcquisition.JsonConverter
{
    public class ApiPrimeSetJsonConverter : JsonConverter<PrimeSet>
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, PrimeSet value, JsonSerializer serializer) => throw new NotImplementedException();

        public override PrimeSet ReadJson(JsonReader reader, Type objectType, PrimeSet existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            string id = jObject["id"]?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(id))
                return null;

            PrimeSet set = new PrimeSet(id);
            ApiItemDeserializationHelper.ParseDefaultValues(set, jObject);

            return set;
        }
    }
}