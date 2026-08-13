using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ShipmentItemResponse
    {
        public Int32 available { get; set; }

        public List<ShipmentItemBatchResponse> batches { get; set; }

        public Int32 batchType { get; set; }

        public Int32 fbaAvailable { get; set; }

        public Int32 fbaStockLevel { get; set; }

        public Int32 fbaTotalStock { get; set; }

        public Int32 id { get; set; }

        public Int32 quantityToShip { get; set; }

        public Int32 receivedQty { get; set; }

        public String asin { get; set; }

        public String sellerSku { get; set; }

        public Int32 shipmentId { get; set; }

        public Int32 shippedQty { get; set; }

        public String sku { get; set; }

        public Int32 stockItemId { get; set; }

        public String stockItemIdGuid { get; set; }

        public String thumbnailSource { get; set; }

        public String title { get; set; }

        public LabelOwner labelOwner { get; set; }

        public List<AmazonPrepInstructionItem> prepInstructions { get; set; }
    }
}
