using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LinnworksAPI.V2
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ShipmentStatus
    {
        DEFAULT,
        ABANDONED,
        CANCELLED,
        CHECKED_IN,
        CLOSED,
        DELETED,
        DELIVERED,
        IN_TRANSIT,
        MIXED,
        READY_TO_SHIP,
        RECEIVING,
        SHIPPED,
        WORKING,
    }
}
