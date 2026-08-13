using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class StockItemBatchResponse
    {
        public Int32? fkBinRackId { get; set; }

        public Int32 batchId { get; set; }

        public Int32 batchInventoryId { get; set; }

        public BinRackResponse binRackType { get; set; }

        public String binRack { get; set; }

        public DateTime expiresOn { get; set; }

        public String number { get; set; }

        public Int32 prioritySequence { get; set; }

        public Int32 available { get; set; }

        public Int32 quantity { get; set; }

        public DateTime sellBy { get; set; }

        public String status { get; set; }

        public Int32 stockItemIntId { get; set; }

        public Guid stockItemId { get; set; }

        public Guid stockLocationId { get; set; }

        public Double stockValue { get; set; }
    }
}
