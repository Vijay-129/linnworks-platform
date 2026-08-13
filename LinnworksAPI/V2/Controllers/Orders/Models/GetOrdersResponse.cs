using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class GetOrdersResponse
    {
        public Int32 TotalOrders { get; set; }

        public Guid? NextSearchToken { get; set; }

        public List<Order> OpenOrders { get; set; }

        public List<ProcessedOrder> ProcessedOrders { get; set; }
    }
}
