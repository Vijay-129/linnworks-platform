using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class StockItemSearchModel
    {
        public String barcodeNumber { get; set; }

        public String itemNumber { get; set; }

        public String itemTitle { get; set; }

        public String sellerSKU { get; set; }

        public String asin { get; set; }
    }
}
