using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class BinRackResponse
    {
        public Int32 binRackId { get; set; }

        public String binRack { get; set; }

        public Int32 binRackTypeId { get; set; }

        public String binRackTypeName { get; set; }

        public Int32 routingSequence { get; set; }
    }
}
