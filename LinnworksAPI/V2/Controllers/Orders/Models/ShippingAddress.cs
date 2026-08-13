using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ShippingAddress
    {
        public String Address1 { get; set; }

        public String Address2 { get; set; }

        public String Address3 { get; set; }

        public String Town { get; set; }

        public String Region { get; set; }

        public String PostCode { get; set; }

        public String Country { get; set; }

        public Guid CountryId { get; set; }

        public String FullName { get; set; }

        public String Company { get; set; }

        public String PhoneNumber { get; set; }

        public String EmailAddress { get; set; }

        public String Continent { get; set; }
    }
}
