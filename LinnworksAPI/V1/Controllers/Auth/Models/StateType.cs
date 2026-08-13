using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LinnworksAPI
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum StateType
    {
        AVAILABLE,
        LOCKED_BASIC,
        LOCKED_FULL,
        MAINTENANCE,
    }
}
