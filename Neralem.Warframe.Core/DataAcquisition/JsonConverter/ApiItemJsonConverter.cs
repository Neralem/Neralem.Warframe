using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace Neralem.Warframe.Core.DataAcquisition.JsonConverter
{
    public class ApiItemJsonConverter : JsonConverter<Item>
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, Item value, JsonSerializer serializer) => throw new NotImplementedException();

        public override Item ReadJson(JsonReader reader, Type objectType, Item existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            string name = jObject["en"]?["item_name"]?.ToObject<string>();
            string urlName = jObject["url_name"]?.ToObject<string>();
            string[] tags = jObject["tags"]?.ToObject<string[]>();

            if (name is null || urlName is null || tags is null)
                return null;

            Item item = null;
            if (tags.Any(x => x.ToLower().Equals("relic"))) // it's a relic
                item = jObject.ToObject<Relic>(new JsonSerializer { Converters = { new ApiRelicJsonConverter() } });
            else if (urlName.Contains("_prime")) // prime part or set
            {
                bool? isSetRoot = jObject["set_root"]?.ToObject<bool>();
                if (isSetRoot is null)
                    return null;
                item = isSetRoot == true
                    ? jObject.ToObject<PrimeSet>(new JsonSerializer { Converters = { new ApiPrimeSetJsonConverter() } })
                    : jObject.ToObject<PrimePart>(new JsonSerializer { Converters = { new ApiPrimePartJsonConverter() } });
            }
            else if (tags.Contains("mod")) // mod or not mod thats the question?.
            {
                item = jObject.ToObject<Mod>(new JsonSerializer { Converters = { new ApiModJsonConverter() } });
            }
            else
            {
                string id = jObject["id"]?.ToObject<string>();
                item = new Item(id);
                ApiItemDeserializationHelper.ParseDefaultValues(item, jObject);
            }

            return item;
        }
    }
}