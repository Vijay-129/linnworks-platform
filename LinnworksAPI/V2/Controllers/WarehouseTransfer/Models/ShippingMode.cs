using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LinnworksAPI.V2
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ShippingMode
    {
        None,
        GroundSmallParcel,
        FreightLtl,
        FreightFtlPallet,
        FreightFtlNonPallet,
        OceanLcl,
        OceanFcl,
        AirSmallParcel,
        AirSmallParcelExpress,
    }
}
