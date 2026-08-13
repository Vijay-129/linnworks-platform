using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class OperationProblemModel
    {
        public SeverityProblem severity { get; set; }

        public String message { get; set; }

        public String details { get; set; }

        public String code { get; set; }
    }
}
