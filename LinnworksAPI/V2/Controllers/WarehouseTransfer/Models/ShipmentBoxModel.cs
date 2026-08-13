using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ShipmentBoxModel
    {
        public Int32 shipmentBoxId { get; set; }

        public Int32? shipmentId { get; set; }

        public Int32? packingGroupId { get; set; }

        public String name { get; set; }

        public Double height { get; set; }

        public Double length { get; set; }

        public Double width { get; set; }

        public UnitOfMeasurement shipmentMeasurementUnit { get; set; }

        public Double weight { get; set; }

        public UnitOfWeight shipmentWeightUnit { get; set; }

        public Int32 quantity { get; set; }
    }
}
