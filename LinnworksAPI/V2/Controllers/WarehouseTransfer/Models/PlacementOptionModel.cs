using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class PlacementOptionModel
    {
        public String placementOptionId { get; set; }

        public DateTime? expirationDate { get; set; }

        public OptionStatus status { get; set; }

        public List<IncentiveModel> fees { get; set; }

        public List<IncentiveModel> discounts { get; set; }

        public List<ShipmentAmazonModel> shipments { get; set; }
    }
}
