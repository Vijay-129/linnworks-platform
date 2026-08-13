using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class AnonymousGetOrdersResponse
    {
        public Int32 TotalOrders { get; set; }

        public Guid? NextSearchToken { get; set; }

        public List<AnonymousOrder> OpenOrders { get; set; }

        public List<AnonymousProcessedOrder> ProcessedOrders { get; set; }
    }
}
