using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class GetPackingGroupModel
    {
        public Int32 id { get; set; }

        public Int32 shippingPlanId { get; set; }

        public String amazonPackingGroupId { get; set; }
    }
}
