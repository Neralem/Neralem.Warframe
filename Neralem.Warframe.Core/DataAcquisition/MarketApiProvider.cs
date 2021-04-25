using Neralem.Warframe.Core.DataAcquisition.JsonConverter;
using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Neralem.Warframe.Core.DataAcquisition
{
    public class MarketApiProvider
    {
        private readonly WebClient client = new();
        private readonly string baseEndpoint = "https://api.warframe.market/v1/";

        #region Items Update

        public async Task<string> GetItemsJsonFromApiAsync() => await client.DownloadStringTaskAsync(baseEndpoint + "items");

        public async Task<string> GetItemDetailsJsonFromApiAsync(string itemUrlName, int maxRetries = 2)
        {
            int tries = 0;
            bool success = false;
            while (!success && tries <= maxRetries)
            {
                try
                {
                    string itemDetailJson = await client.DownloadStringTaskAsync(baseEndpoint + "items/" + itemUrlName);
                    success = !string.IsNullOrWhiteSpace(itemDetailJson);
                    if (success)
                        return itemDetailJson;
                }
                catch (WebException) { await Task.Delay(333); tries++; }
            }

            throw new WebException();
        }

        public async Task<ItemCollection> GetItemCatalogFromApiAsync(IProgress<ItemUpdateProgress> progress, CancellationToken ct, ItemCollection existingItems = null, bool updateExisting = true)
        {
            ItemCollection items = new();
            JsonSerializer itemJsonSerializer = new() { Converters = { new ApiItemJsonConverter() } };
            Dictionary<string, string[]> primeSetPartsTable = new();
            Dictionary<PrimePart, string[]> itemDropRelicTable = new();

            string itemsJson = await GetItemsJsonFromApiAsync();
            JObject jObject = JObject.Parse(itemsJson);
            JArray jItems = jObject["payload"]?["items"]?.ToObject<JArray>();
            if (jItems is null || !jItems.Any())
                throw new InvalidDataException();

            (string id, string urlName)[] itemsIds = jItems
                .Select(x => (x["id"].ToObject<string>(), x["url_name"].ToObject<string>()))
                .Where(x => x.Item2.EndsWith("_relic") && !x.Item2.StartsWith("requiem_") || x.Item2.Contains("_prime"))
                .ToArray();
            if (!updateExisting && existingItems != null)
                itemsIds = itemsIds
                    .Where(x => !existingItems.Select(item => item.Id).Contains(x.id))
                    .ToArray();

            int primePartCount = 0;
            int primeSetCount = 0;
            int relicCount = 0;
            int failedCount = 0;

            foreach (var (id, urlName) in itemsIds)
            {
                if (items.Any(x => x.Id.Equals(id)))
                    continue;
                string itemJson;
                try
                {
                    if (ct.IsCancellationRequested)
                        throw new TaskCanceledException();
                    itemJson = await GetItemDetailsJsonFromApiAsync(urlName);
                    await Task.Delay(333, ct);
                }
                catch (WebException) { Debug.WriteLine($"WebException while receiving {urlName}"); continue; }
                jObject = JObject.Parse(itemJson);
                jItems = jObject["payload"]?["item"]?["items_in_set"]?.ToObject<JArray>();
                string[] setPartIds = jItems.Select(x => x["id"].ToObject<string>()).ToArray();
                if (jItems is null)
                    continue;

                foreach (JToken jItem in jItems)
                {
                    Item item = jItem.ToObject<Item>(itemJsonSerializer);

                    if (item is PrimePart primePart)
                    {
                        itemDropRelicTable.Add(primePart, GetRelicNamesFromPrimePart(jItem));
                        primePartCount++;
                    }
                    else if (item is PrimeSet primeSet)
                    {
                        primeSetPartsTable.Add(primeSet.Id, setPartIds.Where(x => !x.Equals(primeSet.Id)).ToArray());
                        primeSetCount++;
                    }
                    else if (item is Relic)
                        relicCount++;

                    if (item is not null)
                        items.Add(item);
                    else
                        failedCount++;

                    progress.Report(new ItemUpdateProgress
                    {
                        CurrentItemUrlName = urlName, 
                        ItemsFailed = failedCount, 
                        PrimePartCount = primePartCount, 
                        PrimeSetCount =  primeSetCount, 
                        RelicCount = relicCount,
                        ItemsLeft = itemsIds.Length - failedCount - primePartCount - primeSetCount - relicCount
                    });
                }

            }
            progress.Report(new ItemUpdateProgress { Done = true });

            FixRelicNames(items.OfType<Relic>());
            SetupRelicDropTable(items, itemDropRelicTable);
            SetupPrimeSetPartTable(items, primeSetPartsTable);

            return items;
        }

        private static void FixRelicNames(IEnumerable<Relic> relics)
        {
            foreach (Relic relic in relics)
            {
                Match match = Regex.Match(relic.Name, @"^(.+?)\sRelic$", RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;
                relic.Name = match.Groups[1].Value;
            }
        }

        private static string[] GetRelicNamesFromPrimePart(JToken primePartJObject)
        {
            JArray dropTable = primePartJObject["en"]?["drop"]?.ToObject<JArray>();

            if (dropTable is null || !dropTable.Any())
                return Array.Empty<string>();

            return dropTable
                .Select(relicJToken => relicJToken["name"]?.ToObject<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();
        }

        private static void SetupRelicDropTable(ItemCollection items, Dictionary<PrimePart, string[]> primePartDropTables)
        {
            Relic[] relics = items.OfType<Relic>().ToArray();

            foreach (var (primePart, primePartDropTable) in primePartDropTables)
            {
                foreach (string relicNameDrop in primePartDropTable)
                {
                    Match match = Regex.Match(relicNameDrop.Trim(), @"^((?:(?:Lith)|(?:Meso)|(?:Neo)|(?:Axi))\s\w\d+)\sRelic\s\(((?:Common)|(?:Uncommon)|(?:Rare))\)$", RegexOptions.IgnoreCase);
                    if (!match.Success)
                    {
                        Debug.WriteLine($"Failed to Regex relic {relicNameDrop}");
                        continue;
                    }

                    string relicName = match.Groups[1].Value;
                    if (relics.FirstOrDefault(x => x.Name.Equals(relicName)) is not Relic relic)
                        continue;

                    ItemRarity rarity;
                    switch (match.Groups[2].Value.ToLower())
                    {
                        case "common":
                            rarity = ItemRarity.Common;
                            break;
                        case "uncommon":
                            rarity = ItemRarity.Uncommon;
                            break;
                        case "rare":
                            rarity = ItemRarity.Rare;
                            break;
                        default:
                            Debug.WriteLine($"Failed to Regex rarity for relic {relicNameDrop}");
                            continue;
                    }

                    if (!relic.Drops.ContainsKey(primePart))
                        relic.Drops.Add(primePart, rarity);
                    if (!primePart.DropsFrom.Contains(relic))
                        primePart.DropsFrom.Add(relic);
                }
            }
        }

        public static void SetupPrimeSetPartTable(ItemCollection items, Dictionary<string, string[]> primeSetTables)
        {
            foreach (var (setId, partIds) in primeSetTables)
            {
                if (items.OfType<PrimeSet>().FirstOrDefault(x => x.Id.Equals(setId)) is not PrimeSet primeSet)
                    continue;
                PrimePart[] parts = items.OfType<PrimePart>().Where(x => partIds.Contains(x.Id)).ToArray();
                primeSet.Parts.AddRange(parts);
                foreach (PrimePart primePart in parts)
                    primePart.Set = primeSet;
            }
        }

        public async Task SetVaultedRelics(Relic[] relicsToUpdate)
        {
            if (relicsToUpdate is null) 
                throw new ArgumentNullException(nameof(relicsToUpdate));

            if (!relicsToUpdate.Any())
                return;

            string vaultedRelicsTableHtml = await client.DownloadStringTaskAsync("https://warframe.fandom.com/wiki/Module:Void/data");

            Match tableMatch = Regex.Match(vaultedRelicsTableHtml, $@"(local VoidData = .+?in ipairs)", RegexOptions.Singleline);
            if (!tableMatch.Success)
                throw new InvalidDataException();

            string vaultedRelicsTableLua = tableMatch.Groups[1].Value;

            MatchCollection matches = Regex.Matches(vaultedRelicsTableLua, @"{ Tier = &quot;(\w+)&quot;, Name = &quot;(\w\d+)&quot;,.+?IsVaulted = (0|1)", RegexOptions.Singleline);
            if (!matches.Any())
                throw new InvalidDataException();

            foreach (Match match in matches)
            {
                if (!match.Success)
                    throw new InvalidDataException();

                string relicName = $"{match.Groups[1].Value} {match.Groups[2].Value}";

                Relic relic = relicsToUpdate.FirstOrDefault(x => x.Name.Equals(relicName, StringComparison.InvariantCultureIgnoreCase));

                if (relic is null)
                    continue;

                switch (match.Groups[3].Value)
                {
                    case "0":
                        relic.IsVaulted = false;
                        break;
                    case "1":
                        relic.IsVaulted = true;
                        break;
                    default:
                        throw new InvalidDataException();
                }
            }
        }

        #endregion

        #region Orders

        public async Task<OrderCollection> GetOrdersForItemJsonFromApiAsync(Item item, UserCollection users, OnlineStatus minUserOnlineStatus = OnlineStatus.Ingame, bool onlyVisibleOrders = true)
        {
            const int maxRetries = 2;
            int tries = 0;
            JArray ordersJArray = null;
            while (ordersJArray == null && tries <= maxRetries)
            {
                try { ordersJArray = JObject.Parse(await client.DownloadStringTaskAsync(baseEndpoint + $"items/{item.UrlName}/orders"))["payload"]?["orders"]?.ToObject<JArray>(); }
                catch (WebException) { await Task.Delay(333); tries++; }
            }

            if (ordersJArray is null)
                return null;

            OrderCollection orders = new();
            JsonSerializer serializer = new() { Converters = { new ApiUserJsonConverter(), new ApiUserOnlineStatusJsonConverter(), new ApiOrderTypeJsonConverter() } };

            foreach (JToken jOrder in ordersJArray)
            {
                string id = jOrder["id"]?.ToObject<string>();
                string userId = jOrder["user"]?["id"]?.ToObject<string>();
                if (jOrder["visible"]?.ToObject<bool>() is not bool visible || onlyVisibleOrders && !visible)
                    continue;
                if (jOrder["user"]?["status"]?.ToObject<OnlineStatus>(serializer) is not OnlineStatus onlineStatus || onlineStatus == OnlineStatus.Undefined)
                    continue;
                if (onlineStatus < minUserOnlineStatus)
                    continue;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(userId))
                    continue;

                User user = users.FirstOrDefault(x => x.Id.Equals(userId));
                if (user is null) // Create the User and add to users
                {
                    user = jOrder["user"].ToObject<User>(serializer);
                    if (user is null)
                    {
                        Debug.WriteLine($"Failed to parse User with id: {userId}");
                        continue;
                    }
                    users.Add(user);
                }
                else // Update the User
                {
                    User tempUser = user = jOrder["user"].ToObject<User>(serializer);

                    if (tempUser is null)
                        continue;

                    user.Name = tempUser.Name;
                    user.LastSeen = tempUser.LastSeen;
                    user.Reputation = tempUser.Reputation;
                    user.OnlineStatus = tempUser.OnlineStatus;
                }

                if (jOrder["quantity"]?.ToObject<int>() is not int quantity ||
                    jOrder["platinum"]?.ToObject<int>() is not int price ||
                    jOrder["order_type"]?.ToObject<OrderType>(serializer) is not OrderType orderType || orderType == OrderType.Undefined ||
                    jOrder["creation_date"]?.ToObject<DateTime>() is not DateTime creationDate ||
                    jOrder["last_update"]?.ToObject<DateTime>() is not DateTime modifiedDate
                    )
                    continue;

                Order order = new(id)
                {
                    Item = item, 
                    User = user,
                    Quantity = quantity,
                    UnitPrice = price,
                    OrderType = orderType,
                    CreationDate = creationDate,
                    ModificationDate = modifiedDate
                };

                orders.Add(order);
            }

            return orders;
        }

        #endregion
    }
}