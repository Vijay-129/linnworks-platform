using System;

namespace LinnworksAPI
{
    /// <summary>
    /// Same generic-parameter-shadowing bug as ConfigItem&lt;T&gt; (see its comment) -
    /// the second parameter was named "String", shadowing System.String for
    /// PropertyType, and the first was named "SelectStringValueOption", shadowing the
    /// real SelectStringValueOption class. Neither shadowed name happened to be
    /// referenced inside the original class body, so this particular class never
    /// crashed at runtime - renamed anyway so it can't become a footgun if a field
    /// referencing either name is ever added.
    /// </summary>
    public class ConfigPropertySelectionList<TOption, TValue> : LinnObject
    {
        public Boolean Loaded { get; set; }

        public Int32 pkPropertyId { get; set; }

        public Boolean IsChanged { get; set; }

        public TValue PropertyValue { get; set; }

        public String PropertyType { get; set; }
    }
}
