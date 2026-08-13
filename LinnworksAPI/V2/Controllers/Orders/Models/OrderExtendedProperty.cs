using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class OrderExtendedProperty
    {
        public Guid RowId { get; set; }

        public String Name { get; set; }

        public String Value { get; set; }

        public String Type { get; set; }

        public DateTime CreateDate { get; set; }

        public DateTime LastUpdate { get; set; }

        public String UpdatedBy { get; set; }
    }
}
