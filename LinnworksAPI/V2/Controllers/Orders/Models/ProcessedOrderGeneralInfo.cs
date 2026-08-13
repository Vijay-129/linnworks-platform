using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class ProcessedOrderGeneralInfo
    {
        public Int32 Status { get; set; }

        public String StatusDescription { get; set; }

        public Boolean LabelPrinted { get; set; }

        public String LabelError { get; set; }

        public Boolean InvoicePrinted { get; set; }

        public String InvoicePrintError { get; set; }

        public Boolean PickListPrinted { get; set; }

        public String PickListPrintError { get; set; }

        public Int32 NotesCount { get; set; }

        public Int32 Marker { get; set; }

        public List<BasicIdentifier> Identifiers { get; set; }

        public String ReferenceNum { get; set; }

        public String SecondaryReference { get; set; }

        public String ExternalReferenceNum { get; set; }

        public DateTime ReceivedDate { get; set; }

        public String Source { get; set; }

        public String SubSource { get; set; }

        public DateTime DespatchByDate { get; set; }

        public ScheduledDelivery ScheduledDelivery { get; set; }

        public Boolean HasScheduledDelivery { get; set; }

        public Int32 NumItems { get; set; }

        public StockAllocationType StockAllocationType { get; set; }

        public Boolean IsCancelled { get; set; }
    }
}
