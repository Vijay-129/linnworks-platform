using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class AnonymousProcessedOrder
    {
        public Guid OrderId { get; set; }

        public Int32 NumOrderId { get; set; }

        public Boolean Processed { get; set; }

        public DateTime? ProcessedOn { get; set; }

        public DateTime? PaidOn { get; set; }

        public DateTime LastUpdated { get; set; }

        public Guid FulfilmentLocationId { get; set; }

        public OrderShippingInfo ShippingInfo { get; set; }

        public OrderTotalsInfo TotalsInfo { get; set; }

        public List<OrderExtendedProperty> ExtendedProperties { get; set; }

        public List<String> Folders { get; set; }

        public String TaxId { get; set; }

        public ProcessedOrderGeneralInfo GeneralInfo { get; set; }

        public List<AnonymousOrderItemWithChildren> Items { get; set; }
    }
}
