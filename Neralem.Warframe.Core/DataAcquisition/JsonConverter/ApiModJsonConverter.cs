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
    public class ApiModJsonConverter : JsonConverter<Mod>
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, Mod value, JsonSerializer serializer) => throw new NotImplementedException();

        public override Mod ReadJson(JsonReader reader, Type objectType, Mod existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            string id = jObject["id"]?.ToObject<string>();
            string rarity = jObject["rarity"]?.ToObject<string>();
            string[] tags = jObject["tags"]?.ToObject<string[]>();
            if (string.IsNullOrWhiteSpace(id) || tags==null)
                return null;

            Mod mod = new(id);
            ApiItemDeserializationHelper.ParseDefaultValues(mod, jObject);

            mod.MaxRank = jObject["mod_max_rank"]?.ToObject<int>() ?? 0;

            mod.ModRarity = rarity switch
            {
                "common" => ModRarity.Common,
                "uncommon" => ModRarity.Uncommon,
                "rare" => ModRarity.Rare,
                "primed" => ModRarity.Primed,
                _ => ModRarity.Undefined
            };

            if (tags.Contains("primary"))
                mod.Type = ModType.Primary;
            else if (tags.Contains("secondary"))
                mod.Type = ModType.Secondary;
            else if (tags.Contains("meele"))
                mod.Type = ModType.Meele;
            else if (tags.Contains("warframe"))
                mod.Type = ModType.Warframe;
            else
                mod.Type = ModType.Undefined;
            return mod;
        }


    }
}

