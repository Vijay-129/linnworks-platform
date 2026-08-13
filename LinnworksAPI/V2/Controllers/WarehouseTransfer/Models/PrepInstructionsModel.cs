using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class PrepInstructionsModel
    {
        public CurrencyModel fee { get; set; }

        public PrepOwner prepOwner { get; set; }

        public PrepType prepType { get; set; }
    }
}
