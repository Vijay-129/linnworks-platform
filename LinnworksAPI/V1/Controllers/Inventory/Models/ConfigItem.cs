using System;

namespace LinnworksAPI
{
    /// <summary>
    /// The generic parameter was originally named "String" (and, in the sibling
    /// classes, "Boolean") - a real bug from the legacy SDK, ported forward
    /// unchanged until a live API call (Inventory.GetChannels) crashed on it. Naming
    /// a generic parameter after a real type shadows that type inside the class body,
    /// so PropertyType - which the API always sends as a real string regardless of T
    /// (see ConfigItem_Boolean in inventory.json) - was silently resolving to
    /// whatever T was instantiated with instead. Only PropertyValue should vary by T.
    /// </summary>
    public class ConfigItem<T> : LinnObject
    {
        public Boolean Loaded { get; set; }

        public Int32 pkPropertyId { get; set; }

        public Boolean IsChanged { get; set; }

        public T PropertyValue { get; set; }

        public String PropertyType { get; set; }
    }
}
