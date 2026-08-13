using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ItemModel
    {
        public String asin { get; set; }

        public String fnsku { get; set; }

        public String labelOwner { get; set; }

        public String manufacturingLotCode { get; set; }

        public String msku { get; set; }

        public Int32? quantity { get; set; }

        public String thumbnailSource { get; set; }

        public String title { get; set; }

        public Guid stockItemId { get; set; }

        public List<PrepInstructionsModel> prepInstructions { get; set; }
    }
}
