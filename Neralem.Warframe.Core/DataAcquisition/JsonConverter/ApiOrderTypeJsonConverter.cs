using System;
using Neralem.Warframe.Core.DOMs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neralem.Warframe.Core.DataAcquisition.JsonConverter
{
    public class ApiOrderTypeJsonConverter : JsonConverter<OrderType>
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, OrderType value, JsonSerializer serializer) => throw new NotImplementedException();

        public override OrderType ReadJson(JsonReader reader, Type objectType, OrderType existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string str = reader.Value as string;
            if (string.IsNullOrWhiteSpace(str))
                return OrderType.Undefined;

            return str.ToLower() switch
            {
                "sell" => OrderType.Sell,
                "buy" => OrderType.Buy,
                _ => OrderType.Undefined
            };
        }
    }
}