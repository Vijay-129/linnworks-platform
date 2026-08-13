using System;
using System.Collections.Generic;

namespace LinnworksAPI
{
    public class DeleteWarehouseToteRequest : LinnObject
    {
        /// <summary>
        /// List of warehouse tote ids to delete
        /// </summary>
        public List<Int32> ToteIds { get; set; }

        /// <summary>
        /// Location id
        /// </summary>
        public Guid LocationId { get; set; }
    }
}
