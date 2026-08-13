using System.Text;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;

namespace LinnworksAPI
{ 
    [JsonConverter(typeof(StringEnumConverter))]
	public enum DisplayType
	{
		FreeText,
		AutoComplete,
		Dropdown,
		None,
		// Everything below confirmed 2026-08-13 by pulling the raw JSON from a live
		// RulesEngine.GetActionTypes(RuleSetType.Orders) call and extracting every
		// distinct DisplayType value actually returned (11 total; the 4 above were all
		// the legacy SDK had). Not in vendor/PublicApiSpecs/1.0/rulesengine.json -
		// DisplayType isn't defined there at all. This enum can't be assumed complete
		// going forward - re-check the same way if RulesEngine deserialization fails again.
		NumberOfDays,
		Time,
		Currency,
		Paragraph,
		Percentage,
		Timezone,
		Toggle,
	}
}