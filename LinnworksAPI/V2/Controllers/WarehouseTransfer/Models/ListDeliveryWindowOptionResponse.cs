using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ListDeliveryWindowOptionResponse
    {
        public AvailabilityType availabilityType { get; set; }

        public String deliveryWindowOptionId { get; set; }

        public DateTime endDate { get; set; }

        public DateTime startDate { get; set; }

        public DateTime validUntil { get; set; }
    }
}
