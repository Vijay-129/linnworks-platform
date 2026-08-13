using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class GetShipmentCardsResponse
    {
        public List<ShipmentCardModel> cards { get; set; }

        public DateTime lastUpdateDate { get; set; }

        public List<AmazonConfigErrorResponse> amazonConfigErrors { get; set; }
    }
}
