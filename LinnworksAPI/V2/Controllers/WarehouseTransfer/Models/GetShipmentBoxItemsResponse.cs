using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class GetShipmentBoxItemsResponse
    {
        public List<ShipmentBoxItemExtendedModel> boxItems { get; set; }

        public UnitOfWeight weightUnit { get; set; }

        public UnitOfMeasurement dimensionUnit { get; set; }
    }
}
