using System;

namespace LinnworksAPI
{
    /// <summary>
    /// Shared across multiple controllers (PostalServices, Inventory, Orders, ProcessedOrders) -
    /// do not move this back under a single controller's Models/ folder.
    /// </summary>
    public class Channel : LinnObject
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
        /// ChannelName/Source (e.g. EBAY)
        /// </summary>
        public String Source { get; set; }

        /// <summary>
        /// Subsource name (e.g. EBAY1)
        /// </summary>
        public String SubSource { get; set; }
    }
}
