using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    /// <summary>
    /// OrderItemBasic
    /// </summary>
    public class BasicOrderItem
    {
        public Guid RowId { get; set; }

        public Guid StockItemId { get; set; }

        public Boolean IsService { get; set; }

        public Boolean IsUnlinked { get; set; }

        public Int32 Quantity { get; set; }
    }
}
