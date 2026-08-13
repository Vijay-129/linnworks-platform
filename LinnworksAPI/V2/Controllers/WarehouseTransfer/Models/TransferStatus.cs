using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LinnworksAPI.V2
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TransferStatus
    {
        Draft,
        Request,
        Accepted,
        Packing,
        InTransit,
        CheckingIn,
        Delivered,
        Rejected,
    }
}
