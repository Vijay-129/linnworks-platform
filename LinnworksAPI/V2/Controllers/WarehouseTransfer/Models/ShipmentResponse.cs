using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ShipmentResponse
    {
        public DateTime createDate { get; set; }

        public Int32 id { get; set; }

        public String name { get; set; }

        public String amazonShipmentId { get; set; }

        public List<ShipmentItemResponse> shippingItems { get; set; }

        public Int32 shippingPlanId { get; set; }

        public String status { get; set; }

        public DateTime updateDate { get; set; }

        public String warehouseAddress { get; set; }
    }
}
