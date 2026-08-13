using System;
using System.Collections.Generic;

namespace LinnworksAPI
{
    /// <summary>
    /// Exposes only those elements required by the Linnworks front end
    /// </summary>
    public class PostalService_WithChannelAndShippingLinks : LinnObject
    {
        /// <summary>
        /// Postal service ID
        /// </summary>
        public Guid id { get; set; }

        /// <summary>
        /// Whether there is channel linking with a shipping service
        /// </summary>
        public Boolean hasMappedShippingService { get; set; }

        /// <summary>
        /// Channel information
        /// </summary>
        public IEnumerable<Channel> Channels { get; set; }

        /// <summary>
        /// Shipping service information
        /// </summary>
        public IEnumerable<ShippingService> ShippingServices { get; set; }

        public String PostalServiceName { get; set; }

        public String PostalServiceTag { get; set; }

        public String ServiceCountry { get; set; }

        public String PostalServiceCode { get; set; }

        public String Vendor { get; set; }

        public String PrintModule { get; set; }

        public String PrintModuleTitle { get; set; }

        public Guid pkPostalServiceId { get; set; }

        public Boolean TrackingNumberRequired { get; set; }

        public Boolean WeightRequired { get; set; }

        public Boolean IgnorePackagingGroup { get; set; }

        public Int32 fkShippingAPIConfigId { get; set; }

        public Guid? IntegratedServiceId { get; set; }
    }
}
