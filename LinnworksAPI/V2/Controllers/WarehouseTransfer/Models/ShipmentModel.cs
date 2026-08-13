using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ShipmentModel
    {
        public Int32 id { get; set; }

        public String name { get; set; }

        public Int32 shippingPlanId { get; set; }

        public String amazonShipmentId { get; set; }

        public String warehouseAddress { get; set; }

        public ShipmentStatus status { get; set; }

        public DateTime updateDate { get; set; }

        public DateTime createDate { get; set; }

        public List<ShipmentItemModel> items { get; set; }

        public String contactName { get; set; }

        public String contactEmail { get; set; }

        public String contactPhoneNumber { get; set; }

        public DateTime? readyToShipWindow { get; set; }
    }
}
