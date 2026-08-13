using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class AnonymousOrderItemWithChildren
    {
        public Guid RowId { get; set; }

        public Guid StockItemId { get; set; }

        public Boolean IsService { get; set; }

        public Boolean IsUnlinked { get; set; }

        public Int32 Quantity { get; set; }

        public Guid? ParentItemId { get; set; }

        public Int32 StockItemIntId { get; set; }

        public String ItemNumber { get; set; }

        public String SKU { get; set; }

        public String ItemSource { get; set; }

        public String Title { get; set; }

        public Guid CategoryId { get; set; }

        public String CategoryName { get; set; }

        public Double PricePerUnit { get; set; }

        public Double UnitCost { get; set; }

        public Double DespatchStockUnitCost { get; set; }

        public Double Discount { get; set; }

        public Double TaxRate { get; set; }

        public Double Cost { get; set; }

        public Double CostIncTax { get; set; }

        public Double SalesTax { get; set; }

        public Boolean TaxCostInclusive { get; set; }

        public Double DiscountValue { get; set; }

        public Double Weight { get; set; }

        public String BarcodeNumber { get; set; }

        public String ChannelSKU { get; set; }

        public String ChannelTitle { get; set; }

        public Boolean BatchNumberScanRequired { get; set; }

        public Boolean SerialNumberScanRequired { get; set; }

        public List<OrderItemBinRack> BinRacks { get; set; }

        public List<OrderItem> CompositeSubItems { get; set; }
    }
}
