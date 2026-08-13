using System;

namespace LinnworksAPI
{
    public class CreateTemplatesInBulkChannelParameters : LinnObject
    {
        public Int32 ChannelId { get; set; }

        public Guid ConfiguratorId { get; set; }
    }
}
