using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LinnworksAPI.V2
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SkuPrepInstruction
    {
        Polybagging,
        BubbleWrapping,
        Taping,
        BlackShrinkWrapping,
        Labeling,
        HangGarment,
        SuffocationStickering,
        Boxing,
        SetCreation,
        RemoveFromHanger,
        CapSealing,
        Debundle,
        SetStickering,
        BlankStickering,
        NoPrep,
    }
}
