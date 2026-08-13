using System;

namespace LinnworksAPI
{
    public class GetWarehouseTotesRequest : LinnObject
    {
        /// <summary>
        /// Location Id of the totes
        /// </summary>
        public Guid LocationId { get; set; }

        /// <summary>
        /// (Optional) Barcode of the tote. If provided the response will contain one record that
        /// matches exactly to the ToteBarcode, or an empty response if nothing is found. If not
        /// provided (or empty/null) and TotId is not specified, all totes for the warehouse are returned.
        /// </summary>
        public String ToteBarcode { get; set; }

        /// <summary>
        /// (Optional) Id of the tote. If specified, ToteBarcode is ignored. If null and ToteBarcode
        /// is not specified, all totes in the warehouse are returned.
        /// </summary>
        public Int32? TotId { get; set; }
    }
}
