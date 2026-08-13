using System;

namespace LinnworksAPI
{
    /// <summary>
    /// Same generic-parameter-shadowing bug as ConfigItem&lt;T&gt; (see its comment) -
    /// here the parameter was named "Boolean", so every field typed "Boolean" in the
    /// original (Loaded, IsChanged, PropertyValue) silently became T instead of a real
    /// bool for any instantiation where T wasn't actually Boolean (e.g.
    /// ConfigProperty&lt;String&gt;, used for ExtractInventoryVariationMappingPropertyName
    /// in AnyConfig.cs) - Loaded/IsChanged should always be real bool regardless of T.
    /// </summary>
    public class ConfigProperty<T> : LinnObject
    {
        public Boolean Loaded { get; set; }

        public Int32 pkPropertyId { get; set; }

        public Boolean IsChanged { get; set; }

        public T PropertyValue { get; set; }

        public String PropertyType { get; set; }
    }
}
