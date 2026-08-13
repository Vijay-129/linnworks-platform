using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class APIResultResponse_OrderFulfillmentStatus
    {
        public OrderFulfillmentStatus Result { get; set; }

        public APIResultStatus ResultStatus { get; set; }

        public String Message { get; set; }
    }
}
