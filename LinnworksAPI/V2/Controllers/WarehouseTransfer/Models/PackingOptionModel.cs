using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class PackingOptionModel
    {
        public String packingOptionId { get; set; }

        public DateTime? expirationDate { get; set; }

        public OptionStatus status { get; set; }

        public List<PackingGroupModel> packingGroups { get; set; }

        public List<IncentiveModel> fees { get; set; }

        public List<IncentiveModel> discounts { get; set; }

        public List<ShippingConfigurationModel> shippingConfigurations { get; set; }
    }
}
