using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    /// <summary>
    /// Transfer model
    /// </summary>
    public class WarehouseTransferModel
    {
        public DateTime createDate { get; set; }

        public Guid fromLocationId { get; set; }

        public String referenceNumber { get; set; }

        public TransferStatus status { get; set; }

        public Guid toLocationId { get; set; }

        public Int32 transferId { get; set; }

        public TransferType transferType { get; set; }

        public DateTime updateDate { get; set; }
    }
}
