using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ShipmentBoxItemExtendedModel
    {
        public String id { get; set; }

        public Int32 shippingPlanId { get; set; }

        public Int32? shipmentId { get; set; }

        public Int32? packingGroupId { get; set; }

        public Int32? stockItemIntId { get; set; }

        public Guid? stockItemId { get; set; }

        public String sellerSku { get; set; }

        public String sku { get; set; }

        public Double weight { get; set; }

        public Double length { get; set; }

        public Double height { get; set; }

        public Double width { get; set; }

        public Int32 quantityToShip { get; set; }

        public String thumbnailSource { get; set; }

        public ShipmentBoxRecordType type { get; set; }

        public List<String> dataPath { get; set; }

        public Int32? shipmentBoxId { get; set; }

        public Int32? shipmentBoxItemId { get; set; }

        public String shipmentBoxName { get; set; }

        public String title { get; set; }
    }
}
