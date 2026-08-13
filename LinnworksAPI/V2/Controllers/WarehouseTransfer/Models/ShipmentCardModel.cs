using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ShipmentCardModel
    {
        public Int32 channelId { get; set; }

        public DateTime createDate { get; set; }

        public DateTime updateDate { get; set; }

        public Guid fromLocation { get; set; }

        public Int32 id { get; set; }

        public String amazonShipmentId { get; set; }

        public Int32 shippingPlanId { get; set; }

        public Int32 packingType { get; set; }

        public String planId { get; set; }

        public Int32 shipmentItemsCount { get; set; }

        public Int32 shipmentReceived { get; set; }

        public Int32 shipmentShipped { get; set; }

        public ShipmentStatus status { get; set; }

        public Guid toLocation { get; set; }

        public TransferCard type { get; set; }

        public List<StockItemSearchModel> items { get; set; }

        public List<ShipmentSearchModel> shipments { get; set; }
    }
}
