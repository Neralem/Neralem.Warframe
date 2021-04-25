using System;
using System.Text.RegularExpressions;
using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neralem.Warframe.Core.DataAcquisition.JsonConverter
{
    public class ApiRelicJsonConverter : JsonConverter<Relic>
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, Relic value, JsonSerializer serializer) => throw new NotImplementedException();

        public override Relic ReadJson(JsonReader reader, Type objectType, Relic existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            string id = jObject["id"]?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(id))
                return null;

            RelicTier tier;

            Relic relic = new(id);
            ApiItemDeserializationHelper.ParseDefaultValues(relic, jObject);

            Match match = Regex.Match(relic.Name, @"^(\w+)\s");
            if (!match.Success)
                return null;

            switch (match.Groups[1].Value.ToLower())
            {
                case "lith":
                    tier = RelicTier.Lith;
                    break;
                case "meso":
                    tier = RelicTier.Meso;
                    break;
                case "neo":
                    tier = RelicTier.Neo;
                    break;
                case "axi":
                    tier = RelicTier.Axi;
                    break;
                default:
                    return null;
            }

            relic.Tier = tier;
            
            return relic;
        }
    }
}