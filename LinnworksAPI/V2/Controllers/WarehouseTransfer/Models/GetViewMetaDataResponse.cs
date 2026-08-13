using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class GetViewMetaDataResponse
    {
        public List<Int32StringKeyValuePair> prepOwners { get; set; }

        public List<Int32StringKeyValuePair> labelOwners { get; set; }
    }
}
