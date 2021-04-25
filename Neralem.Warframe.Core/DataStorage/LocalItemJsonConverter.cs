using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neralem.Warframe.Core.DataAcquisition;

namespace Neralem.Warframe.Core.DataStorage.JsonConverter
{
    public class LocalItemJsonConverter : JsonConverter<Item>
    {
        internal enum ItemType { Undefined, Relic, PrimePart, PrimeSet }

        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override void WriteJson(JsonWriter writer, Item value, JsonSerializer serializer)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            JObject jObject = new()
            {
                {"id", value.Id},
                {"name", value.Name},
                {"urlName", value.UrlName},
                {"masteryLevel", value.MasteryLevel},
            };

            if (value is Relic relic)
            {
                jObject.Add("itemType", (int)ItemType.Relic);
                jObject.Add("tier", (int)relic.Tier);
                jObject.Add("isVaulted", relic.IsVaulted);
                jObject.Add("drops", JObject.FromObject(relic.Drops.ToDictionary(x => x.Key.Id, y => (int)y.Value)));
            }
            else if (value is PrimePart primePart)
            {
                jObject.Add("itemType", (int)ItemType.PrimePart);
                jObject.Add("ducats", primePart.Ducats);
                jObject.Add("dropsFrom", JArray.FromObject(primePart.DropsFrom.Select(x => x.Id).ToArray()));
                jObject.Add("set", primePart.Set?.Id);
            }
            else if (value is PrimeSet primeSet)
            {
                jObject.Add("itemType", (int)ItemType.PrimeSet);
                jObject.Add("parts", JArray.FromObject(primeSet.Parts.Select(x => x.Id).ToArray()));
            }

            serializer.Serialize(writer, jObject);
        }

        public override Item ReadJson(JsonReader reader, Type objectType, Item existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            string id = jObject["id"]?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidDataException();
            ItemType type = jObject["itemType"]?.ToObject<ItemType>() ?? ItemType.Undefined;
            Item item;

            switch (type)
            {
                case ItemType.Relic:
                    item = new Relic(id);
                    break;
                case ItemType.PrimePart:
                    item = new PrimePart(id);
                    PrimePart part = (PrimePart)item;
                    part.Ducats = jObject["ducats"]?.ToObject<int>() ?? 0;
                    break;
                case ItemType.PrimeSet:
                    item = new PrimeSet(id);
                    break;
                default:
                    throw new InvalidDataException();
            }

            item.Name = jObject["name"]?.ToObject<string>();
            item.UrlName = jObject["urlName"]?.ToObject<string>();
            item.MasteryLevel = jObject["masteryLevel"]?.ToObject<int>() ?? 0;

            return item;
        }
    }

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
                {
                    primePartDropTables.Add(relic, dropTable);
                }
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