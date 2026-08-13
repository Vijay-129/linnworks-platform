using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ShipmentItemModel
    {
        public Int32 stockItemId { get; set; }

        public Int32 id { get; set; }

        public Int32 quantityToShip { get; set; }

        public Int32 receivedQty { get; set; }

        public Int32 shipmentId { get; set; }

        public Int32 shippedQty { get; set; }

        public String sellerSku { get; set; }

        public String sku { get; set; }

        public String title { get; set; }

        public SkuPrepBarcodeInstruction barcodeInstructionId { get; set; }

        public SkuPrepGuidance prepGuidanceId { get; set; }

        public Double unitCost { get; set; }
    }
}
