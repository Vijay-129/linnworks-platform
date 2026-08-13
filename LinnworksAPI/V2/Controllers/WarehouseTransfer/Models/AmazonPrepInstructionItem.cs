using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class AmazonPrepInstructionItem
    {
        public SkuPrepInstruction prepInstruction { get; set; }

        public String currencyCode { get; set; }

        public Double? currencyValue { get; set; }

        public PrepOwner prepOwner { get; set; }
    }
}
