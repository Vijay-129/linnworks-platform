using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class OrderItemBinRack
    {
        public Guid Location { get; set; }

        public String BinRack { get; set; }

        public Int32? BatchId { get; set; }

        public Int32? OrderItemBatchId { get; set; }

        public Int32 Quantity { get; set; }
    }
}
