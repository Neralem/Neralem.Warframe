using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Neralem.Warframe.Core.DataAcquisition.JsonConverter
{
    class ApiArcaneJsonConverter : JsonConverter<Arcane>
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, Arcane value, JsonSerializer serializer) => throw new NotImplementedException();

        public override Arcane ReadJson(JsonReader reader, Type objectType, Arcane existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            string id = jObject["id"]?.ToObject<string>();
            string rarity = jObject["rarity"]?.ToObject<string>();
            string[] tags = jObject["tags"]?.ToObject<string[]>();
            if (string.IsNullOrWhiteSpace(id) || tags == null)
                return null;

            Arcane arcane = new(id);
            ApiItemDeserializationHelper.ParseDefaultValues(arcane, jObject);

            arcane.MaxRank = jObject["mod_max_rank"]?.ToObject<int>() ?? 0;

            arcane.ArcaneRarity = rarity switch
            {
                "common" => ArcaneRarity.Common,
                "uncommon" => ArcaneRarity.Uncommon,
                "rare" => ArcaneRarity.Rare,
                "primed" => ArcaneRarity.Legendary,
                _ => ArcaneRarity.Undefined
            };

            
            return arcane;
        }

    }
}
