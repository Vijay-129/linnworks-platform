using System.Text;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;

namespace LinnworksAPI
{
    [JsonConverter(typeof(StringEnumConverter))]
	public enum ActionType
	{
		AssignShippingService,
		AssignToFolder,
		AssignToLocation,
		SplitOrderByWeight,
		SplitOrderByValue,
		SplitOrderSingle,
		AssignOrderExtendedProperty,
		ChangeOrderLockStatus,
		ChangeOrderParkStatus,
		AssignTagToOrder,
		ExecuteMacro,
		AssignIdentifierToOrder,
		BlockOrderFromMerging,
		SendToFulfillmentNetwork,
		// Everything below confirmed 2026-08-13 by pulling the raw JSON from a live
		// RulesEngine.GetActionTypes(RuleSetType.Orders) call and extracting every
		// distinct action type value actually returned (18 total; the 14 above were
		// all the legacy SDK had). Not in vendor/PublicApiSpecs/1.0/rulesengine.json
		// or the legacy SDK - Linnworks has added action types to production since
		// the spec was last exported. This enum can't be assumed complete going
		// forward - re-check the same way if RulesEngine deserialization fails again.
		SetDispatchDate,
		AddItemToOrder,
		AddNoteToOrder,
		AddServiceToOrder,
	}
}