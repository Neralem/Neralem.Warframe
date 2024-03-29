﻿using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace Neralem.Warframe.Core.DataStorage.JsonConverter
{
    public class LocalItemJsonConverter : JsonConverter<Item>
    {
        internal enum ItemType { Undefined, Relic, PrimePart, PrimeSet, Mod, Arcane }

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
            else if (value is Mod mod)
            {
                jObject.Add("itemType", (int)ItemType.Mod);
                jObject.Add("modType", (int)mod.Type);
                jObject.Add("modRarity", (int)mod.ModRarity);
                jObject.Add("maxRank", mod.MaxRank);
            }
            else if (value is Arcane arcane)
            {
                jObject.Add("itemType",(int)ItemType.Arcane);
                jObject.Add("arcaneRarity",(int)arcane.ArcaneRarity);
                jObject.Add("maxRank",arcane.MaxRank);
            }
            else
                jObject.Add("itemType", (int)ItemType.Undefined);

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
                    item = new Relic(id) { Tier = jObject["ducats"]?.ToObject<RelicTier>() ?? RelicTier.Undefined };
                    break;
                case ItemType.PrimePart:
                    item = new PrimePart(id);
                    PrimePart part = (PrimePart)item;
                    part.Ducats = jObject["ducats"]?.ToObject<int>() ?? 0;
                    break;
                case ItemType.PrimeSet:
                    item = new PrimeSet(id);
                    break;
                case ItemType.Mod:
                    item = new Mod(id);
                    Mod mod = (Mod) item;
                    mod.Type = jObject["modType"]?.ToObject<ModType>() ?? ModType.Undefined;
                    mod.ModRarity = jObject["modRarity"]?.ToObject<ModRarity>() ?? ModRarity.Undefined;
                    mod.MaxRank = jObject["maxRank"]?.ToObject<int>() ?? 0;
                    break;
                case ItemType.Arcane:
                    item = new Arcane(id);
                    Arcane arcane = (Arcane) item;
                    arcane.ArcaneRarity = jObject["rarity"]?.ToObject<ArcaneRarity>() ?? ArcaneRarity.Undefined;
                    arcane.MaxRank = jObject["maxRank"]?.ToObject<int>() ?? 0;
                    break;
                default:
                    item = new Item(id);
                    break;
            }

            item.Name = jObject["name"]?.ToObject<string>();
            item.UrlName = jObject["urlName"]?.ToObject<string>();
            item.MasteryLevel = jObject["masteryLevel"]?.ToObject<int>() ?? 0;

            return item;
        }
    }

    public class LocalUserJsonConverter : JsonConverter<User>
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override void WriteJson(JsonWriter writer, User value, JsonSerializer serializer)
        {
            JObject jObject = new()
            {
                { "id", value.Id },
                { "name", value.Name },
                { "reputation", value.Reputation },
                { "lastSeen", value.LastSeen },
                { "blocked", value.Blocked },
            };

            serializer.Serialize(writer, jObject);
        }

        public override User ReadJson(JsonReader reader, Type objectType, User existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);

            if (jObject["id"]?.ToObject<string>() is not string id || string.IsNullOrWhiteSpace(id) ||
                jObject["name"]?.ToObject<string>() is not string name || string.IsNullOrWhiteSpace(name) ||
                jObject["reputation"]?.ToObject<int>() is not int reputation ||
                jObject["blocked"]?.ToObject<bool>() is not bool blocked)
                return null;

            return new User(id, name)
            {
                Reputation = reputation,
                LastSeen = jObject["lastSeen"]?.ToObject<DateTime?>(serializer),
                Blocked = blocked
            };
        }
    }
}