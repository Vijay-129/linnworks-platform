using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class OrderTotalsInfo
    {
        public Double Subtotal { get; set; }

        public Double PostageCost { get; set; }

        public Double PostageCostExTax { get; set; }

        public Double Tax { get; set; }

        public Double TotalCharge { get; set; }

        public String PaymentMethod { get; set; }

        public Guid PaymentMethodId { get; set; }

        public Double TotalDiscount { get; set; }

        public String Currency { get; set; }

        public Double CountryTaxRate { get; set; }

        public Double ConversionRate { get; set; }
    }
}
