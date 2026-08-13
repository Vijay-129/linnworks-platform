using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class OrderShippingInfo
    {
        public String Vendor { get; set; }

        public Guid PostalServiceId { get; set; }

        public String PostalServiceName { get; set; }

        public Double TotalWeight { get; set; }

        public Double ItemWeight { get; set; }

        public Guid PackageCategoryId { get; set; }

        public String PackageCategory { get; set; }

        public Guid? PackageTypeId { get; set; }

        public String PackageType { get; set; }

        public String TrackingNumber { get; set; }

        public Boolean ManualAdjust { get; set; }
    }
}
