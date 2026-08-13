using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ShipmentItemBatchResponse
    {
        public Int32 batchId { get; set; }

        public Int32 batchInventoryId { get; set; }

        public String batchNumber { get; set; }

        public String batchStatus { get; set; }

        public Int32 available { get; set; }

        public Int32 quantity { get; set; }

        public Int32 quantityToShip { get; set; }

        public Int32 shipmentItemId { get; set; }
    }
}
