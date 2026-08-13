using System;
using System.Collections.Generic;

namespace LinnworksAPI
{
    public class DeleteWarehouseToteResponse : LinnObject
    {
        /// <summary>
        /// Deleted list of tote ids
        /// </summary>
        public List<Int32> DeletedToteIds { get; set; }
    }
}
