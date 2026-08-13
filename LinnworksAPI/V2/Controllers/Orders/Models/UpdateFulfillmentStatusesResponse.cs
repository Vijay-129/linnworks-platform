using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    /// <summary>
    /// A batched API response for OrderFulfillmentStatuses
    /// </summary>
    public class UpdateFulfillmentStatusesResponse
    {
        public List<APIResultResponse_OrderFulfillmentStatus> Results { get; set; }

        public Int32 TotalResults { get; set; }

        public APIResultStatus ResultStatus { get; set; }
    }
}
