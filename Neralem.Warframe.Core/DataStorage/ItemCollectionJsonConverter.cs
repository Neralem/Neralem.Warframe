using System;
using System.Collections.Generic;
using System.Linq;
using Neralem.Warframe.Core.DataAcquisition;
using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neralem.Warframe.Core.DataStorage.JsonConverter
{
    public class ItemCollectionJsonConverter : JsonConverter<ItemCollection>
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override void WriteJson(JsonWriter writer, ItemCollection value, JsonSerializer serializer)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));
            serializer.Converters.Add(new LocalItemJsonConverter());
            serializer.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            serializer.Serialize(writer, JArray.FromObject(value.ToArray(), serializer));
        }

        public override ItemCollection ReadJson(JsonReader reader, Type objectType, ItemCollection existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JArray itemJArray = JArray.Load(reader);
            Dictionary<Relic, Dictionary<string, ItemRarity>> primePartDropTables = new();
            Dictionary<PrimeSet, string[]> primeSetPartTables = new();
            if (itemJArray is null)
                throw new JsonReaderException();
            ItemCollection items = new();
            foreach (JToken jItem in itemJArray)
            {
                Item item = jItem.ToObject<Item>(new JsonSerializer { Converters = { new LocalItemJsonConverter() } });

                if (item is Relic relic && jItem["drops"]?.ToObject<Dictionary<string, ItemRarity>>() is Dictionary<string, ItemRarity> dropTable)
                    primePartDropTables.Add(relic, dropTable);
                else if (item is PrimeSet primeSet && jItem["parts"]?.ToObject<string[]>() is string[] setPartTable)
                    primeSetPartTables.Add(primeSet, setPartTable);

                items.Add(item);
            }

            SetupRelicDropTable(items.OfType<PrimePart>().ToArray(), primePartDropTables);
            MarketApiProvider.SetupPrimeSetPartTable(items, primeSetPartTables.ToDictionary(x => x.Key.Id, y => y.Value));

            return items;
        }

        private static void SetupRelicDropTable(PrimePart[] primeParts, Dictionary<Relic, Dictionary<string, ItemRarity>> primePartRelicDropTables)
        {
            foreach (var (relic, partIdsAndRarities) in primePartRelicDropTables)
            {
                foreach (var (partId, itemRarity) in partIdsAndRarities)
                {
                    if (primeParts.FirstOrDefault(x => x.Id.Equals(partId)) is not PrimePart primePart)
                        continue;
                    relic.Drops.Add(primePart, itemRarity);
                    primePart.DropsFrom.Add(relic);
                }
            }
        }
    }
}