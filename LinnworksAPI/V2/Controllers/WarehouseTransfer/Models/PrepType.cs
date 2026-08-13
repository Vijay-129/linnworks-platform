using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LinnworksAPI.V2
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PrepType
    {
        None,
        ItemLabeling,
        ItemBubblewrap,
        ItemPolybagging,
        ItemTaping,
        ItemBlackShrinkwrap,
        ItemHangGarment,
        ItemBoxing,
        ItemSetcreat,
        ItemRmovhang,
        ItemSuffostk,
        ItemCapSealing,
        ItemDebundle,
        ItemSetstk,
        ItemSioc,
        ItemNoPrep,
        Adult,
        Baby,
        Textile,
        Hanger,
        Fragile,
        Liquid,
        Sharp,
        Small,
        Perforated,
        Granular,
        Set,
        FcProvided,
        Unknown,
    }
}
