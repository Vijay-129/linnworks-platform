using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    public class OrderNote
    {
        public Guid OrderNoteId { get; set; }

        public DateTime NoteDate { get; set; }

        public Boolean Internal { get; set; }

        public String Note { get; set; }

        public String CreatedBy { get; set; }

        public Int32? NoteTypeId { get; set; }
    }
}
