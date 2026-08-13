using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LinnworksAPI.V2
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TransferCard
    {
        SHIPPING_PLAN,
        SHIPMENT,
    }
}
