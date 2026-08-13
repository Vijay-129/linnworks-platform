using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ShipmentBoxItemModel
    {
        public Int32 shipmentBoxItemId { get; set; }

        public Int32? shipmentId { get; set; }

        public Int32? packingGroupId { get; set; }

        public Int32 stockItemId { get; set; }

        public Int32 quantity { get; set; }

        public Int32 shipmentBoxId { get; set; }
    }
}
