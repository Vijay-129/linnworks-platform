using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class OrderFulfillmentStatus
    {
        public Guid OrderId { get; set; }

        public FulfillmentStatus FulfillmentStatus { get; set; }
    }
}
