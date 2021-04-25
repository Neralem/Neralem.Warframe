using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json.Linq;

namespace Neralem.Warframe.Core.DataAcquisition.JsonConverter
{
    internal class ApiItemDeserializationHelper
    {
        public static bool ParseDefaultValues(Item item, JObject jObject)
        {
            string name = jObject["en"]?["item_name"]?.ToObject<string>();
            string urlName = jObject["url_name"]?.ToObject<string>();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(urlName))
                return false;

            item.Name = name;
            item.UrlName = urlName;
            item.MasteryLevel = jObject["mastery_level"]?.ToObject<int>() ?? 0;

            return true;
        }
    }
}