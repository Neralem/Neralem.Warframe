using Neralem.Security;
using Neralem.Warframe.Core.DataAcquisition.JsonConverter;
using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Neralem.Warframe.Core.DataAcquisition
{
    public class MarketApiProvider
    {
        private readonly HttpClient client;
        private const string baseEndpoint = "https://api.warframe.market/v1/";
        private string JWT { get; set; }
        public User CurrentUser { get; set; }

        public MarketApiProvider() => client = new HttpClient(new HttpClientHandler { UseCookies = false });

        private void FillRequestHeaders(HttpRequestMessage request)
        {
            if (!string.IsNullOrWhiteSpace(JWT))
            {
                request.Headers.Add("Authorization", "JWT " + JWT);
                request.Headers.Add("auth_type", "header");
            }
            request.Headers.Add("language", "en");
            request.Headers.Add("accept", "application/json");
            request.Headers.Add("platform", "pc");
        }

        #region Login

        public async Task<User> TryLoginAsync(string email, SecureString password)
        {
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri($"{baseEndpoint}auth/signin"),
                Method = HttpMethod.Post
            };
            var content =
                $"{{ \"email\":\"{email}\",\"password\":\"{Encoding.Default.GetString(password.ToByteArray()).Replace(@"\", @"\\")}\", \"auth_type\": \"header\"}}";
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");
            request.Headers.Add("Authorization", "JWT");
            request.Headers.Add("language", "en");
            request.Headers.Add("accept", "application/json");
            request.Headers.Add("platform", "pc");
            request.Headers.Add("auth_type", "header");
            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;
            string jwt = response.Headers.GetValues("Authorization").FirstOrDefault()?.Substring(4);
            if (string.IsNullOrWhiteSpace(jwt))
                return null;
            string responseBody = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseBody))
                return null;

            if (JToken.Parse(responseBody)["payload"]?["user"] is not JToken jUser)
                return null;

            if (jUser["id"]?.ToObject<string>() is not string _id ||
                jUser["reputation"]?.ToObject<int>() is not int _reputation ||
                jUser["ingame_name"]?.ToObject<string>() is not string _name)
                return null;

            JWT = jwt;
            CurrentUser = new User(_id, _name) {Reputation = _reputation, OnlineStatus = OnlineStatus.Online};

            return CurrentUser;
        }

        #endregion

        #region Items Update

        private async Task<string> GetItemsJsonAsync() => await client.GetStringAsync(baseEndpoint + "items");

        private async Task<string> GetItemDetailsJsonAsync(string itemUrlName, int maxRetries = 2)
        {
            int tries = 0;
            bool success = false;
            while (!success && tries <= maxRetries)
            {
                try
                {
                    string itemDetailJson = await client.GetStringAsync(baseEndpoint + "items/" + itemUrlName);
                    success = !string.IsNullOrWhiteSpace(itemDetailJson);
                    if (success)
                        return itemDetailJson;
                }
                catch (WebException)
                {
                    await Task.Delay(333);
                    tries++;
                }
            }

            throw new WebException();
        }

        public async Task<ItemCollection> GetItemCatalogFromApiAsync(IProgress<ItemUpdateProgress> progress,
            CancellationToken ct, ItemCollection existingItems = null, bool updateExisting = true)
        {
            ItemCollection items = new();
            JsonSerializer itemJsonSerializer = new() { Converters = { new ApiItemJsonConverter() } };
            Dictionary<string, string[]> primeSetPartsTable = new();
            Dictionary<PrimePart, string[]> itemDropRelicTable = new();

            string itemsJson = await GetItemsJsonAsync();
            JObject jObject = JObject.Parse(itemsJson);
            JArray jItems = jObject["payload"]?["items"]?.ToObject<JArray>();
            if (jItems is null || !jItems.Any())
                throw new InvalidDataException();

            string[] includedUrlNames = {
                "vaykor_hek",
                "vaykor_marelok",
                "vaykor_sydon",
                "telos_akbolto",
                "telos_boltace",
                "telos_boltor",
                "synoid_gammacor",
                "synoid_heliocor",
                "synoid_simulor",
                "secura_dual_cestra",
                "secura_lecta",
                "secura_penta",
                "rakta_ballistica",
                "rakta_cernos",
                "rakta_dark_dagger",
                "sancti_castanas",
                "sancti_magistar",
                "sancti_tigris",
            };

            (string id, string urlName)[] itemsIds = jItems
                .Select(x => (x["id"].ToObject<string>(), x["url_name"].ToObject<string>()))
                .Where(x=> !x.Item2.StartsWith("requiem_"))
                .ToArray();
                //.Where(x => x.Item2.EndsWith("_relic") && !x.Item2.StartsWith("requiem_") || x.Item2.Contains("_prime") || includedUrlNames.Contains(x.Item2))
            if (!updateExisting && existingItems != null)
                itemsIds = itemsIds
                    .Where(x => !existingItems.Select(item => item.Id).Contains(x.id))
                    .ToArray();

            int miscCount = 0;
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
                    itemJson = await GetItemDetailsJsonAsync(urlName);
                    await Task.Delay(333, ct);
                }
                catch (WebException)
                {
                    Debug.WriteLine($"WebException while receiving {urlName}");
                    continue;
                }

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
                    else
                        miscCount++;

                    if (item is not null)
                        items.Add(item);
                    else
                        failedCount++;

                    progress.Report(new ItemUpdateProgress
                    {
                        CurrentItemUrlName = urlName,
                        ItemsFailed = failedCount,
                        PrimePartCount = primePartCount,
                        PrimeSetCount = primeSetCount,
                        RelicCount = relicCount,
                        MiscCount = miscCount,
                        ItemsLeft = itemsIds.Length - failedCount - primePartCount - primeSetCount - relicCount - miscCount
                    });
                }

            }

            progress.Report(new ItemUpdateProgress {Done = true});

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

        private static void SetupRelicDropTable(ItemCollection items,
            Dictionary<PrimePart, string[]> primePartDropTables)
        {
            Relic[] relics = items.OfType<Relic>().ToArray();

            foreach (var (primePart, primePartDropTable) in primePartDropTables)
            {
                foreach (string relicNameDrop in primePartDropTable)
                {
                    Match match = Regex.Match(relicNameDrop.Trim(),
                        @"^((?:(?:Lith)|(?:Meso)|(?:Neo)|(?:Axi))\s\w\d+)\sRelic\s\(((?:Common)|(?:Uncommon)|(?:Rare))\)$",
                        RegexOptions.IgnoreCase);
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

            string vaultedRelicsTableHtml =
                await client.GetStringAsync("https://warframe.fandom.com/wiki/Module:Void/data");

            Match tableMatch = Regex.Match(vaultedRelicsTableHtml, $@"(local VoidData = .+?in ipairs)",
                RegexOptions.Singleline);
            if (!tableMatch.Success)
                throw new InvalidDataException();

            string vaultedRelicsTableLua = tableMatch.Groups[1].Value;

            MatchCollection matches = Regex.Matches(vaultedRelicsTableLua,
                @"{ Tier = &quot;(\w+)&quot;, Name = &quot;(\w\d+)&quot;,.+? = (0|1)", RegexOptions.Singleline);
            if (!matches.Any())
                throw new InvalidDataException();

            foreach (Match match in matches)
            {
                if (!match.Success)
                    throw new InvalidDataException();

                string relicName = $"{match.Groups[1].Value} {match.Groups[2].Value}";

                Relic relic = relicsToUpdate.FirstOrDefault(x =>
                    x.Name.Equals(relicName, StringComparison.InvariantCultureIgnoreCase));

                if (relic is null)
                    continue;

                if (match.Groups[0].Value.Contains("IsBaro = 1"))
                    relic.IsVaulted = false;
                else
                {
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
        }

        #endregion

        #region Orders

        private static Order GetOrderFromJObject(JObject jOrder, UserCollection users, ItemCollection items)
        {
            JsonSerializer serializer = new() { Converters = { new ApiUserJsonConverter(), new ApiUserOnlineStatusJsonConverter(), new ApiOrderTypeJsonConverter() } };

            string orderId = jOrder["id"]?.ToObject<string>();
            string itemId = jOrder["item"]?["id"]?.ToObject<string>() ?? items.Single().Id;
            if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(itemId) || items.FirstOrDefault(x => x.Id.Equals(itemId)) is not Item item)
                return null;

            User parsedUser = jOrder["user"] is not JToken jUser ? null : jUser.ToObject<User>(serializer);
            User existingUser = users.FirstOrDefault(x => x.Equals(parsedUser));

            if (parsedUser is null && existingUser is null)
                existingUser = users.Single();

            if (existingUser is null) // Create the User and add to users
                users.Add(parsedUser);
            else if (parsedUser is not null) // Update the User
            {
                existingUser.Name = parsedUser.Name;
                existingUser.LastSeen = parsedUser.LastSeen;
                existingUser.Reputation = parsedUser.Reputation;
                existingUser.OnlineStatus = parsedUser.OnlineStatus;
            }

            User user = existingUser ?? parsedUser;

            if (jOrder["quantity"]?.ToObject<int>() is not int quantity ||
                jOrder["platinum"]?.ToObject<int>() is not int price ||
                jOrder["visible"]?.ToObject<bool>() is not bool visible ||
                jOrder["order_type"]?.ToObject<OrderType>(serializer) is not OrderType orderType ||
                orderType == OrderType.Undefined ||
                jOrder["creation_date"]?.ToObject<DateTime>() is not DateTime creationDate ||
                jOrder["last_update"]?.ToObject<DateTime>() is not DateTime modifiedDate)
                return null;

            Order order = new(orderId)
            {
                Item = item,
                User = user,
                Visible = visible,
                Quantity = quantity,
                UnitPrice = price,
                OrderType = orderType,
                CreationDate = creationDate,
                ModificationDate = modifiedDate,
                Rank = jOrder["mod_rank"]?.ToObject<int>()??0
            };

            return order;
        }

        public async Task<OrderCollection> GetOrdersForItemAsync(Item item, UserCollection users)
        {
            const int maxRetries = 2;
            int tries = 0;
            JArray ordersJArray = null;
            while (ordersJArray == null && tries <= maxRetries)
            {
                try
                {
                    ordersJArray = JObject.Parse(await client.GetStringAsync(baseEndpoint + $"items/{item.UrlName}/orders"))["payload"]?["orders"]?.ToObject<JArray>();
                }
                catch (WebException)
                {
                    await Task.Delay(333);
                    tries++;
                }
            }

            if (ordersJArray is null)
                return null;

            OrderCollection orders = new();

            foreach (JToken jOrder in ordersJArray)
            {
                if (GetOrderFromJObject(jOrder as JObject, users, new ItemCollection(new[] {item})) is Order order)
                    orders.Add(order);
            }

            return orders;
        }

        public async Task<Order> CreateOrderAsync(Item item, int platinum, int quantity, OrderType orderType = OrderType.Sell, bool visible = true)
        {
            if (string.IsNullOrWhiteSpace(JWT)) throw new AccessViolationException("No JWT provided.");
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (platinum <= 0) throw new ArgumentOutOfRangeException(nameof(platinum));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (!Enum.IsDefined(typeof(OrderType), orderType))
                throw new InvalidEnumArgumentException(nameof(orderType), (int) orderType, typeof(OrderType));
            if (orderType == OrderType.Undefined) throw new InvalidDataException();
            
            dynamic payload = new
            {
                item_id = item.Id,
                order_type = orderType == OrderType.Sell ? "sell" : "buy",
                platinum,
                quantity,
                visible,
            };

            string body = JsonConvert.SerializeObject(payload);

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri($"{baseEndpoint}profile/orders"),
                Method = HttpMethod.Post,
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            FillRequestHeaders(request);

            var response = await client.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(responseBody))
                return null;

            Order newOrder = GetOrderFromJObject(JToken.Parse(responseBody)["payload"]?["order"] as JObject, new UserCollection(new[] {CurrentUser}), new ItemCollection(new[] {item}));

            if (newOrder is not null)
                CurrentUser.Orders.Add(newOrder);

            return newOrder;
        }

        public async Task<Order> UpdateOrderAsync(Order order, int platinum, int quantity, bool visible)
        {
            if (string.IsNullOrWhiteSpace(JWT)) throw new AccessViolationException("No JWT provided.");
            if (order == null) throw new ArgumentNullException(nameof(order));
            if (platinum <= 0) throw new ArgumentOutOfRangeException(nameof(platinum));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));

            dynamic payload = new
            {
                order_id = order.Id,
                platinum,
                quantity,
                visible
            };

            string body = JsonConvert.SerializeObject(payload);

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri($"{baseEndpoint}profile/orders/{order.Id}"),
                Method = HttpMethod.Put,
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            FillRequestHeaders(request);

            var response = await client.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(responseBody))
                return null;

            return GetOrderFromJObject(JToken.Parse(responseBody)["payload"]?["order"] as JObject, new UserCollection(new[] { CurrentUser }), new ItemCollection(new[] { order.Item }));
        }

        public async Task<OrderCollection> GetOwnOrdersAsync(ItemCollection items)
        {
            if (string.IsNullOrWhiteSpace(JWT)) throw new AccessViolationException("No JWT provided.");

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri($"{baseEndpoint}profile/{CurrentUser.Name}/orders"),
                Method = HttpMethod.Get
            };

            FillRequestHeaders(request);

            var response = await client.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            JArray sellJOrders = JObject.Parse(responseBody)["payload"]?["sell_orders"]?.ToObject<JArray>();
            JArray buyJOrders = JObject.Parse(responseBody)["payload"]?["buy_orders"]?.ToObject<JArray>();

            if (sellJOrders is null || buyJOrders is null)
                return null;

            OrderCollection orders = new();

            foreach (JToken jOrder in sellJOrders.Concat(buyJOrders))
                if (GetOrderFromJObject(jOrder as JObject, new UserCollection(new[] { CurrentUser }), items) is Order order)
                    orders.Add(order);

            return orders;
        }
        public async Task<bool> DeleteOwnOrderAsync(Order myOrder)
        {
            if (string.IsNullOrWhiteSpace(JWT)) throw new AccessViolationException("No JWT provided.");

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri($"{baseEndpoint}profile/orders/{myOrder.Id}"),
                Method = HttpMethod.Delete
            };

            FillRequestHeaders(request);

            var response = await client.SendAsync(request);
            
            return response.IsSuccessStatusCode;
        }


        #endregion
    }
}