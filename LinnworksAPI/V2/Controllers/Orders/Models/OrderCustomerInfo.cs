using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class OrderCustomerInfo
    {
        public String ChannelBuyerName { get; set; }

        public ShippingAddress Address { get; set; }

        public BillingAddress BillingAddress { get; set; }  // oneOf in spec - see generate_v2_models.py docstring

        public String TaxId { get; set; }
    }
}
