using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class BaseGetOrdersResponse
    {
        public Int32 TotalOrders { get; set; }

        public Guid? NextSearchToken { get; set; }
    }
}
