using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ShipmentItemPrepInstructionModel
    {
        public Int32 shipmentItemId { get; set; }

        public SkuPrepBarcodeInstruction barcodeInstruction { get; set; }

        public SkuPrepGuidance prepGuidance { get; set; }

        public List<AmazonPrepInstructionItem> prepInstructionList { get; set; }

        public object feeAmountPerUnit { get; set; }

        public object totalFeeAmount { get; set; }
    }
}
