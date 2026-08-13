using System;

namespace LinnworksAPI
{
    /// <summary>
    /// Only referenced from PostalServices today. If the ShippingService controller later needs
    /// this same shape, promote it to Shared/Common instead of duplicating it.
    /// </summary>
    public class ShippingService : LinnObject
    {
        /// <summary>
        /// Postal service ID
        /// </summary>
        public Guid pkPostalServiceId { get; set; }

        /// <summary>
        /// Postal service name
        /// </summary>
        public String PostalServiceName { get; set; }

        /// <summary>
        /// Courier name (e.g. Royal Mail)
        /// </summary>
        public String vendor { get; set; }

        /// <summary>
        /// Shipping account ID
        /// </summary>
        public String accountid { get; set; }

        /// <summary>
        /// Courier friendly name (e.g. FedEx (US) for ShipEngine)
        /// </summary>
        public String vendorFriendlyName { get; set; }
    }
}
