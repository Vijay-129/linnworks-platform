using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ShipmentAmazonModel
    {
        public String placementOptionId { get; set; }

        public String shipmentId { get; set; }

        public String name { get; set; }

        public List<ItemModel> shippingItems { get; set; }

        public String warehouseId { get; set; }

        public String warehouseAddress { get; set; }
    }
}
