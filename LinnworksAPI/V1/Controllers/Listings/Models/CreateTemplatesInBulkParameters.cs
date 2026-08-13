using System;
using System.Collections.Generic;

namespace LinnworksAPI
{
    public class CreateTemplatesInBulkParameters : LinnObject
    {
        public Guid LocationId { get; set; }

        public List<CreateTemplatesInBulkChannelParameters> ChannelsConfigurators { get; set; }
    }
}
