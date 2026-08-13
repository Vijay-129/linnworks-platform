using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class PackingGroupModel
    {
        public String packingGroupId { get; set; }

        public List<ItemModel> items { get; set; }
    }
}
