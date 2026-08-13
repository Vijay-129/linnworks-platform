using System;
using System.Collections.Generic;

namespace LinnworksAPI
{
    public class GetEbayListingOperationsRequest : LinnObject
    {
        public Guid LocationId { get; set; }

        public Int32 PageNumber { get; set; }

        public Int32 EntriesPerPage { get; set; }

        public List<Int32> ChannelIds { get; set; }
    }
}
