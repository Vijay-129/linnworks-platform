#nullable enable

/*
Scaffold Header
---------------
Macro Name      : Pick List Monitoring - Park Original Order (Idempotent)
Macro Type      : Scheduled macro
Namespace       : Rishvi.PickListMonitoring
Trigger/Schedule: Configure in Linnworks Automation. Recommended every 5-10 minutes.
Dependencies    : LinnworksAPI, LinnworksMacroHelpers
Runtime         : .NET 8.0
SDK/API         : Linnworks Macro SDK; Orders, OpenOrders and Inventory APIs
Filename        : RIS-RM-2371-2 Pick List Monitoring .cs
Version         : 2026.08.01-v3
*/

using LinnworksAPI;
using LinnworksMacroHelpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;

namespace Rishvi.PickListMonitoring
{
    /// <summary>
    /// Watches consolidated orders and parks the linked original only after the
    /// consolidated order's pick list has been printed.
    ///
    /// Safety controls:
    /// - Processes orders sequentially.
    /// - Re-reads both orders immediately before mutation.
    /// - Requires reciprocal OriginalOrderNumber / ConsolidatedOrderNumber links.
    /// - Refuses ambiguous duplicate correlation properties.
    /// - Uses a dedicated AddExtendedProperties marker instead of resubmitting the
    ///   original order's complete extended-property collection.
    /// - Verifies parking and marker persistence.
    /// - Adds notes only when an equivalent note does not already exist.
    /// - Uses a separate TC-029 validation-hold marker to prevent repeated hold notes.
    /// </summary>
    public sealed class PickListMonitoringScheduled : LinnworksMacroBase
    {
        private const string MacroLogName = "PickListMonitoring";
        private const string MacroVersion = "2026.08.01-v3-idempotent-notes";

        // Correlation properties written by the consolidation macro.
        private const string EpRole = "ConsolidationRole";
        private const string EpOriginalOrderNumber = "OriginalOrderNumber";
        private const string EpConsolidatedOrderNumber = "ConsolidatedOrderNumber";

        // Customs snapshot used by TC-029 validation.
        private const string EpCustomsQuantity = "ConsolidatedCustomsQuantity";
        private const string EpCustomsTotalValue = "ConsolidatedCustomsTotalValue";

        // Idempotency markers owned by this monitoring macro.
        private const string EpPickListParkedUtc = "PickListParkedUtc";
        private const string EpValidationHoldUtc = "ConsolidationValidationHoldUtc";

        private const int ParkedOrderTag = 7;
        private const int PageSize = 200;
        private const int ThrottleDelayMs = 500;

        private const int MaxRetries = 5;
        private const int RetryBaseDelayMs = 2000;

        /// <summary>
        /// Scheduled entry point.
        /// </summary>
        /// <param name="viewName">
        /// Optional Linnworks open-order view. Leave empty to scan all open orders.
        /// </param>
        /// <param name="location">
        /// Optional fulfilment-location name. Leave empty to scan all locations.
        /// </param>
        public void Execute(string viewName = "", string location = "")
        {
            Logger.WriteInfo(
                $"[{MacroLogName}] Macro started. Version: {MacroVersion}.");

            var parkedCount = 0;
            var validationHoldCount = 0;
            var alreadyDoneCount = 0;
            var skippedCount = 0;
            var failedCount = 0;

            try
            {
                var locationId = ResolveLocationId(location);
                if (!string.IsNullOrWhiteSpace(location) && locationId == null)
                {
                    return;
                }

                var viewId = ResolveViewId(viewName);
                if (!string.IsNullOrWhiteSpace(viewName) && viewId <= 0)
                {
                    return;
                }

                var orderIds = FetchOpenOrderIds(viewId, locationId);
                var detailedOrders = LoadOrderDetails(orderIds);

                // This is only the first-pass candidate filter. Every field and link is
                // re-read authoritatively inside ProcessCandidate before any mutation.
                var candidates = detailedOrders
                    .Where(order =>
                        order.OrderId != Guid.Empty &&
                        order.GeneralInfo != null &&
                        order.GeneralInfo.PickListPrinted)
                    .OrderBy(order => order.NumOrderId)
                    .ToList();

                Logger.WriteInfo(
                    $"[{MacroLogName}] Open orders found: {orderIds.Count}; " +
                    $"orders with a printed pick list: {candidates.Count}.");

                // Prevent two consolidated candidates in the same run from mutating the
                // same original order.
                var processedOriginalOrderIds = new HashSet<Guid>();

                foreach (var candidate in candidates)
                {
                    var outcome = ProcessCandidate(
                        candidate,
                        processedOriginalOrderIds);

                    switch (outcome)
                    {
                        case ProcessingOutcome.Parked:
                            parkedCount++;
                            break;

                        case ProcessingOutcome.ValidationHeld:
                            validationHoldCount++;
                            break;

                        case ProcessingOutcome.AlreadyDone:
                            alreadyDoneCount++;
                            break;

                        case ProcessingOutcome.Skipped:
                            skippedCount++;
                            break;

                        case ProcessingOutcome.Failed:
                            failedCount++;
                            break;
                    }

                    Thread.Sleep(ThrottleDelayMs);
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                Logger.WriteError(
                    $"[{MacroLogName}] Unhandled macro error: {ex}");
            }
            finally
            {
                Logger.WriteInfo(
                    $"[{MacroLogName}] Run complete. " +
                    $"Parked: {parkedCount}; " +
                    $"Validation holds: {validationHoldCount}; " +
                    $"Already done: {alreadyDoneCount}; " +
                    $"Skipped: {skippedCount}; " +
                    $"Failed: {failedCount}.");

                Logger.WriteInfo($"[{MacroLogName}] Macro finished.");
            }
        }

        // ---------------------------------------------------------------------
        // Candidate resolution
        // ---------------------------------------------------------------------

        private ProcessingOutcome ProcessCandidate(
            OrderDetails candidate,
            HashSet<Guid> processedOriginalOrderIds)
        {
            try
            {
                var consolidated = ApiCall(
                    () => Api.Orders.GetOrderById(candidate.OrderId),
                    $"Reload consolidated candidate {candidate.NumOrderId}");

                if (consolidated == null ||
                    consolidated.OrderId == Guid.Empty ||
                    consolidated.GeneralInfo == null)
                {
                    Logger.WriteWarning(
                        $"[{MacroLogName}] Candidate {candidate.NumOrderId} could not " +
                        "be reloaded; skipping.");
                    return ProcessingOutcome.Skipped;
                }

                if (!consolidated.GeneralInfo.PickListPrinted)
                {
                    Logger.WriteInfo(
                        $"[{MacroLogName}] Order {consolidated.NumOrderId} no longer has " +
                        "a printed pick list; skipping.");
                    return ProcessingOutcome.Skipped;
                }

                var consolidatedProperties = LoadExtendedProperties(
                    consolidated.OrderId,
                    $"consolidated order {consolidated.NumOrderId}");

                if (!TryGetSingleTextValue(
                        consolidatedProperties,
                        EpRole,
                        out var consolidatedRole,
                        out var roleError) ||
                    !string.Equals(
                        consolidatedRole,
                        "Consolidated",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(roleError))
                    {
                        Logger.WriteWarning(
                            $"[{MacroLogName}] Order {consolidated.NumOrderId} skipped. " +
                            $"{roleError}");
                    }

                    return ProcessingOutcome.Skipped;
                }

                if (!TryGetSinglePositiveOrderNumber(
                        consolidatedProperties,
                        EpOriginalOrderNumber,
                        out var originalOrderNumber,
                        out var originalLinkError))
                {
                    Logger.WriteError(
                        $"[{MacroLogName}] Consolidated order " +
                        $"{consolidated.NumOrderId} has an invalid original-order link. " +
                        $"{originalLinkError}");
                    return ProcessingOutcome.Skipped;
                }

                var original = ApiCall(
                    () => Api.Orders.GetOrderDetailsByNumOrderId(originalOrderNumber),
                    $"Load original order {originalOrderNumber}");

                if (original == null || original.OrderId == Guid.Empty)
                {
                    Logger.WriteWarning(
                        $"[{MacroLogName}] Original order {originalOrderNumber} linked " +
                        $"from consolidated order {consolidated.NumOrderId} was not found.");
                    return ProcessingOutcome.Skipped;
                }

                var originalProperties = LoadExtendedProperties(
                    original.OrderId,
                    $"original order {original.NumOrderId}");

                if (!TryGetSingleTextValue(
                        originalProperties,
                        EpRole,
                        out var originalRole,
                        out var originalRoleError) ||
                    !string.Equals(
                        originalRole,
                        "Original",
                        StringComparison.OrdinalIgnoreCase))
                {
                    Logger.WriteError(
                        $"[{MacroLogName}] Order {original.NumOrderId} is not verified " +
                        $"as the original order. {originalRoleError}");
                    return ProcessingOutcome.Skipped;
                }

                if (!TryGetSinglePositiveOrderNumber(
                        originalProperties,
                        EpConsolidatedOrderNumber,
                        out var authoritativeConsolidatedOrderNumber,
                        out var consolidatedLinkError))
                {
                    Logger.WriteError(
                        $"[{MacroLogName}] Original order {original.NumOrderId} has an " +
                        $"invalid or ambiguous consolidated-order link. " +
                        $"{consolidatedLinkError} No order was changed.");
                    return ProcessingOutcome.Skipped;
                }

                if (authoritativeConsolidatedOrderNumber != consolidated.NumOrderId)
                {
                    Logger.WriteWarning(
                        $"[{MacroLogName}] Consolidated order {consolidated.NumOrderId} " +
                        $"is not the authoritative order linked from original " +
                        $"{original.NumOrderId}; the original points to " +
                        $"{authoritativeConsolidatedOrderNumber}. Skipping obsolete candidate.");
                    return ProcessingOutcome.Skipped;
                }

                if (!processedOriginalOrderIds.Add(original.OrderId))
                {
                    Logger.WriteWarning(
                        $"[{MacroLogName}] Original order {original.NumOrderId} was " +
                        "already considered during this run; skipping duplicate candidate.");
                    return ProcessingOutcome.Skipped;
                }

                return ProcessAuthoritativePair(
                    consolidated,
                    original);
            }
            catch (Exception ex)
            {
                Logger.WriteError(
                    $"[{MacroLogName}] Candidate {candidate.NumOrderId} failed: {ex}");
                return ProcessingOutcome.Failed;
            }
        }

        // ---------------------------------------------------------------------
        // Per-pair mutation
        // ---------------------------------------------------------------------

        private ProcessingOutcome ProcessAuthoritativePair(
            OrderDetails consolidatedSnapshot,
            OrderDetails originalSnapshot)
        {
            try
            {
                // Re-read immediately before mutation so the scheduler's earlier snapshot
                // cannot trigger work after a user or another rule changed either order.
                var consolidated = ApiCall(
                    () => Api.Orders.GetOrderById(consolidatedSnapshot.OrderId),
                    $"Final reload consolidated {consolidatedSnapshot.NumOrderId}");

                var original = ApiCall(
                    () => Api.Orders.GetOrderById(originalSnapshot.OrderId),
                    $"Final reload original {originalSnapshot.NumOrderId}");

                if (consolidated == null ||
                    original == null ||
                    consolidated.GeneralInfo == null ||
                    original.GeneralInfo == null)
                {
                    Logger.WriteWarning(
                        $"[{MacroLogName}] Final reload failed for consolidated " +
                        $"{consolidatedSnapshot.NumOrderId} or original " +
                        $"{originalSnapshot.NumOrderId}; skipping.");
                    return ProcessingOutcome.Skipped;
                }

                if (!consolidated.GeneralInfo.PickListPrinted)
                {
                    Logger.WriteInfo(
                        $"[{MacroLogName}] Consolidated order " +
                        $"{consolidated.NumOrderId} does not currently have a printed " +
                        "pick list; skipping.");
                    return ProcessingOutcome.Skipped;
                }

                var consolidatedProperties = LoadExtendedProperties(
                    consolidated.OrderId,
                    $"consolidated order {consolidated.NumOrderId}");

                var originalProperties = LoadExtendedProperties(
                    original.OrderId,
                    $"original order {original.NumOrderId}");

                if (!LinksAreStillAuthoritative(
                        consolidated,
                        consolidatedProperties,
                        original,
                        originalProperties,
                        out var linkError))
                {
                    Logger.WriteError(
                        $"[{MacroLogName}] Correlation verification failed immediately " +
                        $"before mutation. {linkError} No order was changed.");
                    return ProcessingOutcome.Skipped;
                }

                // TC-029 is deliberately evaluated before the success marker check.
                // This allows a later change to the original order to place both orders
                // on hold even if the original had previously been parked successfully.
                var validation = ValidateOriginalOrderUnchanged(
                    original,
                    consolidatedProperties);

                if (!validation.IsValid)
                {
                    return HandleValidationFailure(
                        original,
                        consolidated,
                        originalProperties,
                        validation.Detail);
                }

                var existingParkedMarkerValues = GetNonEmptyPropertyValues(
                    originalProperties,
                    EpPickListParkedUtc);

                if (existingParkedMarkerValues.Count > 1)
                {
                    Logger.WriteError(
                        $"[{MacroLogName}] Original order {original.NumOrderId} has " +
                        $"conflicting {EpPickListParkedUtc} values: " +
                        $"{string.Join(", ", existingParkedMarkerValues)}. " +
                        "No additional note or marker was written.");
                    return ProcessingOutcome.Failed;
                }

                var existingParkedMarker = existingParkedMarkerValues
                    .FirstOrDefault() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(existingParkedMarker))
                {
                    // Repair inconsistent state if a marker exists but the order was
                    // subsequently unparked.
                    if (!original.GeneralInfo.IsParked)
                    {
                        Logger.WriteWarning(
                            $"[{MacroLogName}] Original order {original.NumOrderId} has " +
                            $"{EpPickListParkedUtc} but is not parked. Re-applying parked status.");

                        ParkAndVerify(original);
                    }

                    // This is a repair-only, duplicate-safe operation. It does not add a
                    // note when any equivalent PickListMonitoring note already exists.
                    EnsureParkingNotes(original, consolidated);

                    Logger.WriteInfo(
                        $"[{MacroLogName}] Original order {original.NumOrderId} was " +
                        $"already completed at {existingParkedMarker}; skipping.");
                    return ProcessingOutcome.AlreadyDone;
                }

                Logger.WriteInfo(
                    $"[{MacroLogName}] Printed pick list detected on consolidated order " +
                    $"{consolidated.NumOrderId}. Parking original order " +
                    $"{original.NumOrderId}.");

                ParkAndVerify(original);

                var parkedUtc = DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture);

                // The marker is persisted and verified before notes are written. If another
                // overlapping execution created the marker first, this execution does not
                // write notes.
                var markerCreatedByThisExecution = TryCreateVerifiedMarker(
                    original.OrderId,
                    EpPickListParkedUtc,
                    parkedUtc,
                    $"original order {original.NumOrderId}");

                if (!markerCreatedByThisExecution)
                {
                    Logger.WriteInfo(
                        $"[{MacroLogName}] Another execution already created " +
                        $"{EpPickListParkedUtc} for original order " +
                        $"{original.NumOrderId}. Notes were not written by this execution.");
                    return ProcessingOutcome.AlreadyDone;
                }

                EnsureParkingNotes(original, consolidated);

                Logger.WriteInfo(
                    $"[{MacroLogName}] Success. Original order {original.NumOrderId} " +
                    $"is parked and linked to active consolidated order " +
                    $"{consolidated.NumOrderId}.");

                return ProcessingOutcome.Parked;
            }
            catch (Exception ex)
            {
                Logger.WriteError(
                    $"[{MacroLogName}] Failed processing consolidated order " +
                    $"{consolidatedSnapshot.NumOrderId} and original order " +
                    $"{originalSnapshot.NumOrderId}: {ex}");
                return ProcessingOutcome.Failed;
            }
        }

        private ProcessingOutcome HandleValidationFailure(
            OrderDetails original,
            OrderDetails consolidated,
            List<ExtendedProperty> originalProperties,
            string validationDetail)
        {
            var existingHoldMarkerValues = GetNonEmptyPropertyValues(
                originalProperties,
                EpValidationHoldUtc);

            if (existingHoldMarkerValues.Count > 1)
            {
                Logger.WriteError(
                    $"[{MacroLogName}] Original order {original.NumOrderId} has " +
                    $"conflicting {EpValidationHoldUtc} values: " +
                    $"{string.Join(", ", existingHoldMarkerValues)}. " +
                    "No additional hold note or marker was written.");
                return ProcessingOutcome.Failed;
            }

            var existingHoldMarker = existingHoldMarkerValues
                .FirstOrDefault() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(existingHoldMarker))
            {
                // Repair the parked state if either order was manually unparked.
                ParkAndVerify(original);
                ParkAndVerify(consolidated);

                EnsureValidationFailureNotes(
                    original,
                    consolidated,
                    validationDetail);

                Logger.WriteWarning(
                    $"[{MacroLogName}] TC-029 hold already exists for original order " +
                    $"{original.NumOrderId} at {existingHoldMarker}; no duplicate hold " +
                    "marker or equivalent note was added.");

                return ProcessingOutcome.AlreadyDone;
            }

            Logger.WriteWarning(
                $"[{MacroLogName}] TC-029 validation failed. Parking original order " +
                $"{original.NumOrderId} and consolidated order " +
                $"{consolidated.NumOrderId}. {validationDetail}");

            ParkAndVerify(original);
            ParkAndVerify(consolidated);

            var holdUtc = DateTime.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);

            var markerCreatedByThisExecution = TryCreateVerifiedMarker(
                original.OrderId,
                EpValidationHoldUtc,
                holdUtc,
                $"TC-029 hold for original order {original.NumOrderId}");

            if (markerCreatedByThisExecution)
            {
                EnsureValidationFailureNotes(
                    original,
                    consolidated,
                    validationDetail);
            }
            else
            {
                Logger.WriteInfo(
                    $"[{MacroLogName}] Another execution already created " +
                    $"{EpValidationHoldUtc} for original order {original.NumOrderId}. " +
                    "Notes were not written by this execution.");
            }

            return ProcessingOutcome.ValidationHeld;
        }

        // ---------------------------------------------------------------------
        // Validation
        // ---------------------------------------------------------------------

        private ValidationResult ValidateOriginalOrderUnchanged(
            OrderDetails original,
            List<ExtendedProperty> consolidatedProperties)
        {
            var expectedQuantityText = GetFirstNonEmptyPropertyValue(
                consolidatedProperties,
                EpCustomsQuantity);

            var expectedTotalValueText = GetFirstNonEmptyPropertyValue(
                consolidatedProperties,
                EpCustomsTotalValue);

            // Preserve the original macro's behavior: when the consolidation snapshot is
            // unavailable, do not create a false validation hold.
            if (string.IsNullOrWhiteSpace(expectedQuantityText) ||
                string.IsNullOrWhiteSpace(expectedTotalValueText))
            {
                Logger.WriteWarning(
                    $"[{MacroLogName}] TC-029 snapshot is incomplete for original order " +
                    $"{original.NumOrderId}; validation was not enforced.");

                return ValidationResult.Success(
                    "TC-029 snapshot properties were unavailable.");
            }

            if (!int.TryParse(
                    expectedQuantityText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var expectedQuantity))
            {
                return ValidationResult.Failure(
                    $"Invalid {EpCustomsQuantity} value " +
                    $"'{expectedQuantityText}'.");
            }

            if (!double.TryParse(
                    expectedTotalValueText,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var expectedTotalValue))
            {
                return ValidationResult.Failure(
                    $"Invalid {EpCustomsTotalValue} value " +
                    $"'{expectedTotalValueText}'.");
            }

            var positiveLines = original.Items?
                .Where(item => item != null && item.Quantity > 0)
                .ToList()
                ?? new List<OrderItem>();

            var currentQuantity = positiveLines.Sum(item => item.Quantity);

            double currentTotalValueRaw = 0d;

            foreach (var item in positiveLines)
            {
                var grossLineValue = item.PricePerUnit * item.Quantity;
                var discountPercent = Math.Max(
                    0d,
                    Math.Min(100d, Convert.ToDouble(item.Discount)));

                currentTotalValueRaw +=
                    grossLineValue * (1d - (discountPercent / 100d));
            }

            var currentTotalValue = Math.Round(
                currentTotalValueRaw,
                2,
                MidpointRounding.AwayFromZero);

            expectedTotalValue = Math.Round(
                expectedTotalValue,
                2,
                MidpointRounding.AwayFromZero);

            var quantityMatches = currentQuantity == expectedQuantity;
            var totalMatches = Math.Abs(
                currentTotalValue - expectedTotalValue) < 0.005d;

            if (quantityMatches && totalMatches)
            {
                return ValidationResult.Success(
                    $"Quantity {currentQuantity}; value " +
                    $"{currentTotalValue.ToString("0.00", CultureInfo.InvariantCulture)}.");
            }

            return ValidationResult.Failure(
                $"Expected quantity {expectedQuantity}, actual {currentQuantity}; " +
                $"expected value " +
                $"{expectedTotalValue.ToString("0.00", CultureInfo.InvariantCulture)}, " +
                $"actual " +
                $"{currentTotalValue.ToString("0.00", CultureInfo.InvariantCulture)}.");
        }

        // ---------------------------------------------------------------------
        // Parking and notes
        // ---------------------------------------------------------------------

        private void ParkAndVerify(OrderDetails order)
        {
            if (order.GeneralInfo?.IsParked != true)
            {
                ApiCall(
                    () =>
                    {
                        Api.Orders.ChangeOrderTag(
                            new List<Guid> { order.OrderId },
                            ParkedOrderTag);
                        return true;
                    },
                    $"Park order {order.NumOrderId}");
            }

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                var verified = ApiCall(
                    () => Api.Orders.GetOrderById(order.OrderId),
                    $"Verify parked state for order {order.NumOrderId}, attempt {attempt}");

                if (verified?.GeneralInfo?.IsParked == true)
                {
                    return;
                }

                if (attempt < 3)
                {
                    Thread.Sleep(250 * attempt);
                }
            }

            throw new InvalidOperationException(
                $"Linnworks did not report order {order.NumOrderId} as parked.");
        }

        private void EnsureParkingNotes(
            OrderDetails original,
            OrderDetails consolidated)
        {
            var consolidatedNumber = consolidated.NumOrderId.ToString(
                CultureInfo.InvariantCulture);

            var originalNumber = original.NumOrderId.ToString(
                CultureInfo.InvariantCulture);

            var originalNote =
                $"Order parked automatically. Pick list was printed on consolidated " +
                $"order {consolidated.NumOrderId}. Dispatch must occur from the " +
                "consolidated order only.";

            var consolidatedNote =
                $"Pick list printed. Original order {original.NumOrderId} has been " +
                "parked to prevent duplicate dispatch.";

            try
            {
                EnsureInternalNote(
                    original.OrderId,
                    originalNote,
                    note =>
                        IsCreatedByThisMacro(note) &&
                        ContainsIgnoreCase(note.Note, "Order parked automatically") &&
                        ContainsIgnoreCase(note.Note, consolidatedNumber) &&
                        ContainsIgnoreCase(note.Note, "Dispatch must occur"),
                    $"parking note on original order {original.NumOrderId}");
            }
            catch (Exception ex)
            {
                Logger.WriteWarning(
                    $"[{MacroLogName}] Original order {original.NumOrderId} is parked, " +
                    $"but its audit note could not be verified or written: {ex.Message}");
            }

            try
            {
                EnsureInternalNote(
                    consolidated.OrderId,
                    consolidatedNote,
                    note =>
                        IsCreatedByThisMacro(note) &&
                        ContainsIgnoreCase(note.Note, originalNumber) &&
                        ContainsIgnoreCase(note.Note, "parked") &&
                        ContainsIgnoreCase(note.Note, "duplicate dispatch"),
                    $"parking note on consolidated order {consolidated.NumOrderId}");
            }
            catch (Exception ex)
            {
                Logger.WriteWarning(
                    $"[{MacroLogName}] Original order {original.NumOrderId} is parked, " +
                    $"but the consolidated-order audit note could not be verified or " +
                    $"written: {ex.Message}");
            }
        }

        private void EnsureValidationFailureNotes(
            OrderDetails original,
            OrderDetails consolidated,
            string validationDetail)
        {
            var originalNumber = original.NumOrderId.ToString(
                CultureInfo.InvariantCulture);

            var noteText =
                $"TC-029 Validation Failed: Original order {original.NumOrderId} " +
                $"was modified after consolidation. Shipment held. {validationDetail}";

            try
            {
                EnsureInternalNote(
                    original.OrderId,
                    noteText,
                    note =>
                        IsCreatedByThisMacro(note) &&
                        ContainsIgnoreCase(note.Note, "TC-029 Validation Failed") &&
                        ContainsIgnoreCase(note.Note, originalNumber),
                    $"TC-029 note on original order {original.NumOrderId}");
            }
            catch (Exception ex)
            {
                Logger.WriteWarning(
                    $"[{MacroLogName}] TC-029 hold was applied, but the original-order " +
                    $"note could not be verified or written: {ex.Message}");
            }

            try
            {
                EnsureInternalNote(
                    consolidated.OrderId,
                    noteText,
                    note =>
                        IsCreatedByThisMacro(note) &&
                        ContainsIgnoreCase(note.Note, "TC-029 Validation Failed") &&
                        ContainsIgnoreCase(note.Note, originalNumber),
                    $"TC-029 note on consolidated order {consolidated.NumOrderId}");
            }
            catch (Exception ex)
            {
                Logger.WriteWarning(
                    $"[{MacroLogName}] TC-029 hold was applied, but the consolidated-" +
                    $"order note could not be verified or written: {ex.Message}");
            }
        }

        private void EnsureInternalNote(
            Guid orderId,
            string noteText,
            Func<OrderNote, bool> equivalentNotePredicate,
            string context)
        {
            var notes = ApiCall(
                    () => Api.Orders.GetOrderNotes(orderId),
                    $"Load notes for {context}")
                ?? new List<OrderNote>();

            var normalizedExpected = noteText.Trim();

            var alreadyExists = notes.Any(note =>
                note != null &&
                (string.Equals(
                     note.Note?.Trim() ?? string.Empty,
                     normalizedExpected,
                     StringComparison.Ordinal) ||
                 equivalentNotePredicate(note)));

            if (alreadyExists)
            {
                Logger.WriteInfo(
                    $"[{MacroLogName}] Equivalent note already exists for {context}; " +
                    "no note was added.");
                return;
            }

            notes.Add(new OrderNote
            {
                OrderNoteId = Guid.NewGuid(),
                OrderId = orderId,
                Note = normalizedExpected,
                Internal = true,
                NoteDate = DateTime.UtcNow,
                CreatedBy = MacroLogName
            });

            ApiCall(
                () =>
                {
                    Api.Orders.SetOrderNotes(orderId, notes);
                    return true;
                },
                $"Save note for {context}");

            // Read-after-write verification.
            var verifiedNotes = ApiCall(
                    () => Api.Orders.GetOrderNotes(orderId),
                    $"Verify note for {context}")
                ?? new List<OrderNote>();

            if (!verifiedNotes.Any(note =>
                    note != null &&
                    string.Equals(
                        note.Note?.Trim() ?? string.Empty,
                        normalizedExpected,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Linnworks did not return the expected note after saving {context}.");
            }

            Logger.WriteInfo(
                $"[{MacroLogName}] Note added for {context}.");
        }

        // ---------------------------------------------------------------------
        // Extended-property marker handling
        // ---------------------------------------------------------------------

        /// <summary>
        /// Adds a new, macro-owned marker and verifies it by re-reading the order.
        ///
        /// Returns true only when this execution created the marker. Returns false
        /// when another execution or a previous run already created it.
        ///
        /// This method intentionally does not call SetExtendedProperties and never
        /// resubmits channel/system-owned properties from the order.
        /// </summary>
        private bool TryCreateVerifiedMarker(
            Guid orderId,
            string propertyName,
            string propertyValue,
            string context)
        {
            var before = LoadExtendedProperties(orderId, context);

            var existingValues = GetNonEmptyPropertyValues(
                before,
                propertyName);

            if (existingValues.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Marker '{propertyName}' is already ambiguous for {context}. " +
                    $"Values: {string.Join(", ", existingValues)}.");
            }

            if (existingValues.Count == 1)
            {
                return false;
            }

            Exception? requestException = null;
            var responseErrors = Array.Empty<string>();

            try
            {
                var response = ApiCall(
                    () => Api.Orders.AddExtendedProperties(
                        new AddExtendedPropertiesRequest
                        {
                            OrderId = orderId,
                            ExtendedProperties = new[]
                            {
                                new BasicExtendedProperty
                                {
                                    Name = propertyName,
                                    Value = propertyValue,
                                    Type = "Order"
                                }
                            }
                        }),
                    $"Add {propertyName} for {context}");

                responseErrors = response?.Errors?
                    .Where(error => !string.IsNullOrWhiteSpace(error))
                    .ToArray()
                    ?? Array.Empty<string>();
            }
            catch (Exception ex)
            {
                requestException = ex;
            }

            // Always verify. The endpoint may persist the property even when a warning
            // or transport exception is surfaced to the caller.
            var after = LoadExtendedProperties(
                orderId,
                $"verify {propertyName} for {context}");

            var savedValues = GetNonEmptyPropertyValues(
                after,
                propertyName);

            if (savedValues.Count == 0)
            {
                var details = requestException != null
                    ? requestException.Message
                    : responseErrors.Length > 0
                        ? string.Join(" | ", responseErrors)
                        : "Linnworks returned no persisted marker.";

                throw new InvalidOperationException(
                    $"Could not persist required marker '{propertyName}' for {context}. " +
                    $"Details: {details}",
                    requestException);
            }

            if (savedValues.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Marker '{propertyName}' became ambiguous for {context}. " +
                    $"Values: {string.Join(", ", savedValues)}. " +
                    "No audit note was written by this execution.");
            }

            if (string.Equals(
                    savedValues[0],
                    propertyValue,
                    StringComparison.Ordinal))
            {
                Logger.WriteInfo(
                    $"[{MacroLogName}] Verified marker {propertyName} for {context}.");
                return true;
            }

            // A different non-empty value means another execution created the marker
            // between the initial read and this request.
            Logger.WriteInfo(
                $"[{MacroLogName}] Marker {propertyName} already exists for {context} " +
                "with a value created by another execution.");

            return false;
        }

        private List<ExtendedProperty> LoadExtendedProperties(
            Guid orderId,
            string context)
        {
            return ApiCall(
                    () => Api.Orders.GetExtendedProperties(orderId),
                    $"Load extended properties for {context}")
                ?.ToList()
                ?? new List<ExtendedProperty>();
        }

        private static string GetFirstNonEmptyPropertyValue(
            IEnumerable<ExtendedProperty> properties,
            string propertyName)
        {
            return properties
                .Where(property =>
                    property != null &&
                    string.Equals(
                        property.Name?.Trim(),
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                .Select(property => property.Value?.Trim() ?? string.Empty)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? string.Empty;
        }

        private static List<string> GetNonEmptyPropertyValues(
            IEnumerable<ExtendedProperty> properties,
            string propertyName)
        {
            return properties
                .Where(property =>
                    property != null &&
                    string.Equals(
                        property.Name?.Trim(),
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                .Select(property => property.Value?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static bool TryGetSingleTextValue(
            IEnumerable<ExtendedProperty> properties,
            string propertyName,
            out string value,
            out string error)
        {
            var values = properties
                .Where(property =>
                    property != null &&
                    string.Equals(
                        property.Name?.Trim(),
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                .Select(property => property.Value?.Trim() ?? string.Empty)
                .Where(propertyValue => !string.IsNullOrWhiteSpace(propertyValue))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (values.Count == 0)
            {
                value = string.Empty;
                error = $"Required extended property '{propertyName}' is missing or empty.";
                return false;
            }

            if (values.Count > 1)
            {
                value = string.Empty;
                error =
                    $"Extended property '{propertyName}' has conflicting values: " +
                    $"{string.Join(", ", values)}.";
                return false;
            }

            value = values[0];
            error = string.Empty;
            return true;
        }

        private static bool TryGetSinglePositiveOrderNumber(
            IEnumerable<ExtendedProperty> properties,
            string propertyName,
            out int orderNumber,
            out string error)
        {
            var rawValues = properties
                .Where(property =>
                    property != null &&
                    string.Equals(
                        property.Name?.Trim(),
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                .Select(property => property.Value?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (rawValues.Count == 0)
            {
                orderNumber = 0;
                error = $"Required extended property '{propertyName}' is missing or empty.";
                return false;
            }

            var parsedValues = new HashSet<int>();

            foreach (var rawValue in rawValues)
            {
                if (!int.TryParse(
                        rawValue,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var parsed) ||
                    parsed <= 0)
                {
                    orderNumber = 0;
                    error =
                        $"Extended property '{propertyName}' contains invalid value " +
                        $"'{rawValue}'.";
                    return false;
                }

                parsedValues.Add(parsed);
            }

            if (parsedValues.Count != 1)
            {
                orderNumber = 0;
                error =
                    $"Extended property '{propertyName}' is ambiguous. Values: " +
                    $"{string.Join(", ", parsedValues.OrderBy(v => v))}.";
                return false;
            }

            orderNumber = parsedValues.Single();
            error = string.Empty;
            return true;
        }

        // ---------------------------------------------------------------------
        // Correlation checks
        // ---------------------------------------------------------------------

        private static bool LinksAreStillAuthoritative(
            OrderDetails consolidated,
            List<ExtendedProperty> consolidatedProperties,
            OrderDetails original,
            List<ExtendedProperty> originalProperties,
            out string error)
        {
            if (!TryGetSingleTextValue(
                    consolidatedProperties,
                    EpRole,
                    out var consolidatedRole,
                    out error) ||
                !string.Equals(
                    consolidatedRole,
                    "Consolidated",
                    StringComparison.OrdinalIgnoreCase))
            {
                error =
                    $"Order {consolidated.NumOrderId} is not verified as Consolidated. " +
                    error;
                return false;
            }

            if (!TryGetSingleTextValue(
                    originalProperties,
                    EpRole,
                    out var originalRole,
                    out error) ||
                !string.Equals(
                    originalRole,
                    "Original",
                    StringComparison.OrdinalIgnoreCase))
            {
                error =
                    $"Order {original.NumOrderId} is not verified as Original. " +
                    error;
                return false;
            }

            if (!TryGetSinglePositiveOrderNumber(
                    consolidatedProperties,
                    EpOriginalOrderNumber,
                    out var originalNumberFromConsolidated,
                    out error))
            {
                return false;
            }

            if (!TryGetSinglePositiveOrderNumber(
                    originalProperties,
                    EpConsolidatedOrderNumber,
                    out var consolidatedNumberFromOriginal,
                    out error))
            {
                return false;
            }

            if (originalNumberFromConsolidated != original.NumOrderId)
            {
                error =
                    $"Consolidated order {consolidated.NumOrderId} points to original " +
                    $"{originalNumberFromConsolidated}, not {original.NumOrderId}.";
                return false;
            }

            if (consolidatedNumberFromOriginal != consolidated.NumOrderId)
            {
                error =
                    $"Original order {original.NumOrderId} points to consolidated " +
                    $"{consolidatedNumberFromOriginal}, not {consolidated.NumOrderId}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        // ---------------------------------------------------------------------
        // Order discovery
        // ---------------------------------------------------------------------

        private Guid? ResolveLocationId(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            var requestedName = location.Trim();

            var locations = ApiCall(
                () => Api.Inventory.GetStockLocations(),
                $"Resolve location '{requestedName}'");

            if (locations == null)
            {
                Logger.WriteError(
                    $"[{MacroLogName}] Linnworks returned no locations while resolving " +
                    $"'{requestedName}'. No orders were changed.");
                return null;
            }

            var matches = locations
                .Where(item =>
                    string.Equals(
                        item.LocationName?.Trim(),
                        requestedName,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count != 1)
            {
                Logger.WriteError(
                    $"[{MacroLogName}] Location '{requestedName}' resolved to " +
                    $"{matches.Count} records. No orders were changed.");
                return null;
            }

            Logger.WriteInfo(
                $"[{MacroLogName}] Location '{requestedName}' resolved to " +
                $"{matches[0].StockLocationId}.");

            return matches[0].StockLocationId;
        }

        private int ResolveViewId(string viewName)
        {
            if (string.IsNullOrWhiteSpace(viewName))
            {
                return 0;
            }

            var requestedName = viewName.Trim();

            var viewStats = ApiCall(
                    () => Api.OpenOrders.GetViewStats(new GetViewStatsRequest()),
                    $"Resolve view '{requestedName}'")
                ?? new List<OrderViewStats>();

            var matches = viewStats
                .Where(view =>
                    view.ViewExists &&
                    string.Equals(
                        view.ViewName?.Trim(),
                        requestedName,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count != 1)
            {
                Logger.WriteError(
                    $"[{MacroLogName}] View '{requestedName}' resolved to " +
                    $"{matches.Count} records. No orders were changed.");
                return 0;
            }

            Logger.WriteInfo(
                $"[{MacroLogName}] View '{requestedName}' resolved to ViewId " +
                $"{matches[0].ViewId}.");

            return matches[0].ViewId;
        }

        private HashSet<Guid> FetchOpenOrderIds(
            int viewId,
            Guid? locationId)
        {
            var orderIds = new HashSet<Guid>();
            var pageNumber = 1;

            if (viewId > 0)
            {
                while (true)
                {
                    var response = ApiCall(
                        () => Api.OpenOrders.GetOpenOrderIds(
                            new GetOpenOrdersRequest
                            {
                                OrderIds = null,
                                ViewId = viewId,
                                LocationId = locationId ?? Guid.Empty,
                                EntriesPerPage = PageSize,
                                PageNumber = pageNumber
                            }),
                        $"Load view {viewId}, page {pageNumber}");

                    var data = response?.Data?.ToList()
                        ?? new List<Guid>();

                    foreach (var orderId in data.Where(id => id != Guid.Empty))
                    {
                        orderIds.Add(orderId);
                    }

                    if (data.Count == 0 ||
                        (response != null && response.TotalPages > 0 && pageNumber >= response.TotalPages))
                    {
                        break;
                    }

                    pageNumber++;
                }
            }
            else
            {
                while (true)
                {
                    var response = ApiCall(
                        () => Api.Orders.GetOpenOrders(
                            PageSize,
                            pageNumber,
                            null,
                            null,
                            locationId,
                            null),
                        $"Load open orders, page {pageNumber}");

                    var data = response?.Data?.ToList()
                        ?? new List<OpenOrder>();

                    if (data.Count == 0)
                    {
                        break;
                    }

                    foreach (var order in data)
                    {
                        if (order != null && order.OrderId != Guid.Empty)
                        {
                            orderIds.Add(order.OrderId);
                        }
                    }

                    if (data.Count < PageSize)
                    {
                        break;
                    }

                    pageNumber++;
                }
            }

            return orderIds;
        }

        private List<OrderDetails> LoadOrderDetails(HashSet<Guid> orderIds)
        {
            var detailedOrders = new List<OrderDetails>();
            var idsList = orderIds.ToList();

            for (int i = 0; i < idsList.Count; i += PageSize)
            {
                var batchIds = idsList.Skip(i).Take(PageSize).ToList();
                var batchDetails = ApiCall(
                    () => Api.Orders.GetOrdersById(batchIds),
                    $"Load batch details ({batchIds.Count} orders)");

                if (batchDetails != null)
                {
                    detailedOrders.AddRange(batchDetails);
                }
            }

            return detailedOrders;
        }

        // ---------------------------------------------------------------------
        // Utility helpers
        // ---------------------------------------------------------------------

        private static bool ContainsIgnoreCase(string? source, string target)
        {
            if (source == null || target == null) return false;
            return source.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCreatedByThisMacro(OrderNote note)
        {
            if (note == null || string.IsNullOrWhiteSpace(note.CreatedBy)) return false;
            return string.Equals(note.CreatedBy.Trim(), MacroLogName, StringComparison.OrdinalIgnoreCase);
        }

        private T ApiCall<T>(Func<T> apiCall, string context)
        {
            int attempt = 0;
            int delayMs = RetryBaseDelayMs;

            while (true)
            {
                try
                {
                    return apiCall();
                }
                catch (WebException wex)
                    when (wex.Response is HttpWebResponse resp &&
                          (int)resp.StatusCode == 429)
                {
                    attempt++;
                    if (attempt > MaxRetries)
                    {
                        Logger.WriteError(
                            $"[{MacroLogName}] HTTP 429 persists after {MaxRetries} retries ({context}). Giving up.");
                        throw;
                    }

                    Logger.WriteWarning(
                        $"[{MacroLogName}] HTTP 429 rate limited during '{context}'. " +
                        $"Waiting {delayMs}ms before retry {attempt}/{MaxRetries}.");

                    Thread.Sleep(delayMs);
                    delayMs = Math.Min(delayMs * 2, 60000);
                }
            }
        }

        private enum ProcessingOutcome
        {
            Parked,
            ValidationHeld,
            AlreadyDone,
            Skipped,
            Failed
        }

        private readonly struct ValidationResult
        {
            public bool IsValid { get; }
            public string Detail { get; }

            private ValidationResult(bool isValid, string detail)
            {
                IsValid = isValid;
                Detail = detail;
            }

            public static ValidationResult Success(string detail) => new ValidationResult(true, detail);
            public static ValidationResult Failure(string detail) => new ValidationResult(false, detail);
        }
    }
}
