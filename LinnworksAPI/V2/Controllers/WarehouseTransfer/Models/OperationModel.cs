using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class OperationModel
    {
        public Guid id { get; set; }

        public Int32 entityId { get; set; }

        public OperationStatus status { get; set; }

        public OperationType type { get; set; }

        public DateTime createdDate { get; set; }

        public List<OperationProblemModel> problems { get; set; }
    }
}
