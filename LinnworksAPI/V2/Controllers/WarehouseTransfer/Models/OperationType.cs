using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LinnworksAPI.V2
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum OperationType
    {
        None,
        CreatingShippingPlan,
        GeneratingPackingOptions,
        ConfirmingPackingOptions,
        GeneratingPlacementOptions,
        ConfirmingPlacementOptions,
        GeneratingTransportOptions,
        ConfirmingTransportOptions,
        GeneratingDeliveryWindows,
        ConfirmingDeliveryWindows,
        SettingPackingInfo,
    }
}
