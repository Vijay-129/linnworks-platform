using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class GetShippingPlanByIdResponse
    {
        public Int32 channelId { get; set; }

        public String defaultShipmentId { get; set; }

        public Guid fromLocation { get; set; }

        public Int32 id { get; set; }

        public Boolean? isPackingInfoKnown { get; set; }

        public String planId { get; set; }

        public Int32 shipmentItemsCount { get; set; }

        public List<ShipmentResponse> shipments { get; set; }

        public ShippingPlanStatus status { get; set; }

        public Guid toLocation { get; set; }

        public String placementOptionId { get; set; }
    }
}
