#nullable enable

// Scaffold Header
// Macro Name       : ContainerEtaFolderSync
// Macro Type       : Scheduled
// Namespace        : Rishvi.ContainerEtaFolderSyncMacro
// Trigger/Schedule : Every 30-60 minutes; do not overlap executions
// Dependencies     : LinnworksAPI, LinnworksMacroHelpers
// Runtime Target   : .NET 8.0
// SDK/API           : Linnworks Macro SDK / Linnworks API
// Filename          : ContainerEtaFolderSync_FIXED.cs
//
// Purpose
// -------
// Synchronises managed pre-sale order folders with the current quoted delivery
// date of the matching open purchase order.
//
// Important rate-limit correction
// -------------------------------
// The previous implementation called GetExtendedProperties and GetOrderById
// repeatedly for every open order. It also polled each order several times
// during folder replacement. That produced a large API burst and HTTP 429.
//
// This version:
//   1. Reads open orders in pages.
//   2. Identifies the allocated PO from the existing managed folder.
//   3. Reads open PO headers in pages.
//   4. Performs unlock, unpark, folder assignment, folder removal, re-park,
//      and re-lock operations in batches.
//   5. Does not call GetExtendedProperties during ETA folder synchronisation.
//   6. Applies global request pacing and HTTP 429 exponential retry.
//
// The allocation extended properties created by the allocator are left intact.
// The folder is treated as the operational ETA display for this scheduled macro.

using LinnworksAPI;
using LinnworksMacroHelpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;

namespace Rishvi.ContainerEtaFolderSyncMacro{

public sealed class ContainerEtaFolderSync : LinnworksMacroBase
{
    private const int OpenOrderPageSize = 200;
    private const int PurchaseOrderPageSize = 200;
    private const int MutationBatchSize = 100;

    // 550 ms keeps this macro below 110 requests/minute even before batching.
    // The batching in this implementation normally results in far fewer calls.
    private const int MinimumApiSpacingMilliseconds = 550;

    private static readonly object RateLimitLock = new();
    private static DateTime _lastApiCallUtc = DateTime.MinValue;

    public void Execute(
        string preSalesLocationName,
        string folderPrefix = ContainerEtaFolderSyncConstants.FolderPrefixDefault)
    {
        var startedUtc = DateTime.UtcNow;

        try
        {
            Logger.WriteInfo(
                $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                "ContainerEtaFolderSync started.");

            var normalizedPrefix = NormalizeFolderPrefix(folderPrefix);
            var location = ResolveLocationByName(preSalesLocationName);

            var poByNumber = LoadOpenPurchaseOrderEtas();
            if (poByNumber.Count == 0)
            {
                Logger.WriteInfo(
                    $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                    "No usable open purchase-order headers were returned. Nothing to synchronise.");
                return;
            }

            var openOrders = LoadOpenOrders(location.StockLocationId);
            if (openOrders.Count == 0)
            {
                Logger.WriteInfo(
                    $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                    $"No open orders found in location '{location.LocationName}'.");
                return;
            }

            var plan = BuildFolderChangePlan(openOrders, poByNumber, normalizedPrefix);

            Logger.WriteInfo(
                $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                $"Open orders scanned: {openOrders.Count}; folder changes required: {plan.Count}.");

            if (plan.Count == 0)
            {
                Logger.WriteInfo(
                    $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                    "All recognised container folders are already aligned.");
                return;
            }

            EnsureFolders(plan.Select(x => x.NewFolder));
            ApplyFolderChanges(plan, location.StockLocationId, normalizedPrefix);

            Logger.WriteInfo(
                $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                $"Container ETA folder synchronisation completed for {plan.Count} order(s).");
        }
        catch (Exception ex)
        {
            Logger.WriteError(
                $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                $"ContainerEtaFolderSync failed: {ex}");
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startedUtc;
            Logger.WriteInfo(
                $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                $"ContainerEtaFolderSync finished in {elapsed.TotalSeconds:N1} second(s).");
        }
    }

    private StockLocation ResolveLocationByName(string locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName))
            throw new ArgumentException("Pre-sales location name is required.", nameof(locationName));

        var locations = ExecuteApi(
            "Inventory.GetStockLocations",
            () => Api.Inventory.GetStockLocations() ?? new List<StockLocation>());

        var location = locations.FirstOrDefault(x =>
            string.Equals(
                x.LocationName?.Trim(),
                locationName.Trim(),
                StringComparison.OrdinalIgnoreCase));

        if (location == null)
        {
            throw new InvalidOperationException(
                $"Linnworks location '{locationName}' was not found.");
        }

        return location;
    }

    private List<OpenOrder> LoadOpenOrders(Guid locationId)
    {
        var result = new List<OpenOrder>();
        var pageNumber = 1;

        while (true)
        {
            var currentPage = pageNumber;

            var response = ExecuteApi(
                $"Orders.GetOpenOrders(page={currentPage})",
                () => Api.Orders.GetOpenOrders(
                    OpenOrderPageSize,
                    currentPage,
                    null,
                    null,
                    locationId,
                    null));

            var page = response?.Data?.ToList() ?? new List<OpenOrder>();
            if (page.Count == 0)
                break;

            result.AddRange(page);

            Logger.WriteInfo(
                $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                $"Loaded open-order page {currentPage}: {page.Count} order(s).");

            if (page.Count < OpenOrderPageSize)
                break;

            pageNumber++;
        }

        return result
            .GroupBy(x => x.OrderId)
            .Select(x => x.First())
            .ToList();
    }

    private Dictionary<string, PurchaseOrderEtaSnapshot> LoadOpenPurchaseOrderEtas()
    {
        var snapshots = new List<PurchaseOrderEtaSnapshot>();
        var pageNumber = 1;

        while (true)
        {
            var currentPage = pageNumber;
            var request = new Search_PurchaseOrder2Request
            {
                Status = PurchaseOrderStatus.OPEN,
                PageNumber = currentPage,
                EntriesPerPage = PurchaseOrderPageSize
            };

            var response = ExecuteApi(
                $"PurchaseOrder.Search_PurchaseOrders2(page={currentPage})",
                () => Api.PurchaseOrder.Search_PurchaseOrders2(request));

            var headers = response?.Result;
            if (headers == null || headers.Count == 0)
                break;

            foreach (var header in headers)
            {
                var poNumber = FirstNonEmpty(
                    TryReadString(header, "ExternalInvoiceNumber"),
                    TryReadString(header, "PurchaseOrderReferenceNumber"),
                    TryReadString(header, "SupplierReferenceNumber"));

                var eta = header.QuotedDeliveryDate;

                // Search_PurchaseOrders2 returns purchase-order headers. The
                // reflection fallback below protects against SDK versions that
                // omit one of the display-number fields from the search model.
                if (string.IsNullOrWhiteSpace(poNumber))
                {
                    try
                    {
                        var fullPo = ExecuteApi(
                            $"PurchaseOrder.Get_PurchaseOrder({header.pkPurchaseID})",
                            () => Api.PurchaseOrder.Get_PurchaseOrder(header.pkPurchaseID));

                        var fullHeader = fullPo?.PurchaseOrderHeader;
                        poNumber = FirstNonEmpty(
                            TryReadString(fullHeader, "ExternalInvoiceNumber"),
                            TryReadString(fullHeader, "PurchaseOrderReferenceNumber"),
                            TryReadString(fullHeader, "SupplierReferenceNumber"));

                        if (eta == DateTime.MinValue && fullHeader != null)
                            eta = fullHeader.QuotedDeliveryDate;
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteError(
                            $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                            $"Could not load display number for PO {header.pkPurchaseID}: {ex.Message}");
                    }
                }

                if (string.IsNullOrWhiteSpace(poNumber))
                {
                    Logger.WriteInfo(
                        $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                        $"Skipping PO {header.pkPurchaseID}: no external PO number/reference was found.");
                    continue;
                }

                if (eta == DateTime.MinValue)
                {
                    Logger.WriteInfo(
                        $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                        $"Skipping PO '{poNumber}': quoted delivery date is empty.");
                    continue;
                }

                snapshots.Add(new PurchaseOrderEtaSnapshot
                {
                    PurchaseOrderId = header.pkPurchaseID,
                    PurchaseOrderNumber = poNumber.Trim(),
                    QuotedDeliveryDate = eta
                });
            }

            Logger.WriteInfo(
                $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                $"Loaded open purchase-order page {currentPage}: {headers.Count} header(s).");

            if (headers.Count < PurchaseOrderPageSize)
                break;

            pageNumber++;
        }

        var result = new Dictionary<string, PurchaseOrderEtaSnapshot>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var group in snapshots.GroupBy(
                     x => x.PurchaseOrderNumber,
                     StringComparer.OrdinalIgnoreCase))
        {
            var distinctPurchaseIds = group
                .Select(x => x.PurchaseOrderId)
                .Distinct()
                .ToList();

            if (distinctPurchaseIds.Count > 1)
            {
                Logger.WriteError(
                    $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                    $"PO number '{group.Key}' is not unique across open purchase orders. " +
                    "Orders for this PO number will be skipped to avoid using the wrong ETA.");
                continue;
            }

            result[group.Key] = group
                .OrderByDescending(x => x.QuotedDeliveryDate)
                .First();
        }

        return result;
    }

    private List<FolderChangePlan> BuildFolderChangePlan(
        IEnumerable<OpenOrder> openOrders,
        IReadOnlyDictionary<string, PurchaseOrderEtaSnapshot> poByNumber,
        string folderPrefix)
    {
        var plan = new List<FolderChangePlan>();

        foreach (var order in openOrders)
        {
            var allFolders = order.FolderName?.ToList() ?? new List<string>();
            var managedFolders = allFolders
                .Where(folder => IsManagedFolder(folder, folderPrefix))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (managedFolders.Count == 0)
                continue;

            var parsedFolders = new List<ParsedContainerFolder>();

            foreach (var folder in managedFolders)
            {
                if (TryParseContainerFolder(folder, folderPrefix, out var parsed))
                    parsedFolders.Add(parsed);
            }

            if (parsedFolders.Count == 0)
            {
                // NO PO AVAILABLE and Exception folders intentionally reach here.
                continue;
            }

            var poNumbers = parsedFolders
                .Select(x => x.PurchaseOrderNumber)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (poNumbers.Count != 1)
            {
                Logger.WriteError(
                    $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                    $"Order {order.NumOrderId} ({order.OrderId}) has managed folders " +
                    $"for multiple PO numbers [{string.Join(", ", poNumbers)}]. Skipped.");
                continue;
            }

            var poNumber = poNumbers[0];
            if (!poByNumber.TryGetValue(poNumber, out var po))
            {
                Logger.WriteInfo(
                    $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                    $"Order {order.NumOrderId}: open PO '{poNumber}' was not found. Folder left unchanged.");
                continue;
            }

            var targetFolder = BuildContainerFolderName(
                po.PurchaseOrderNumber,
                po.QuotedDeliveryDate,
                folderPrefix);

            var obsoleteFolders = managedFolders
                .Where(folder => !string.Equals(
                    folder,
                    targetFolder,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            var alreadyHasTarget = managedFolders.Any(folder =>
                string.Equals(
                    folder,
                    targetFolder,
                    StringComparison.OrdinalIgnoreCase));

            if (alreadyHasTarget && obsoleteFolders.Count == 0)
                continue;

            var oldEta = parsedFolders
                .Where(x => x.Eta != DateTime.MinValue)
                .Select(x => x.Eta)
                .FirstOrDefault();

            plan.Add(new FolderChangePlan
            {
                OrderId = order.OrderId,
                OrderNumber = order.NumOrderId,
                PurchaseOrderNumber = po.PurchaseOrderNumber,
                OldEta = oldEta,
                NewEta = po.QuotedDeliveryDate,
                NewFolder = targetFolder,
                ObsoleteFolders = obsoleteFolders,
                WasParked = order.GeneralInfo?.IsParked == true,
                WasLocked = order.GeneralInfo?.HoldOrCancel == true
            });
        }

        return plan;
    }

    private void EnsureFolders(IEnumerable<string> requiredFolderNames)
    {
        var required = requiredFolderNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (required.Count == 0)
            return;

        var availableFolders = ExecuteApi(
            "Orders.GetAvailableFolders",
            () => Api.Orders.GetAvailableFolders() ?? new List<OrderFolder>());

        var changed = false;

        foreach (var folderName in required)
        {
            if (availableFolders.Any(x =>
                    string.Equals(
                        x.FolderName,
                        folderName,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            availableFolders.Add(new OrderFolder
            {
                pkFolderId = Guid.NewGuid(),
                FolderName = folderName
            });

            changed = true;

            Logger.WriteInfo(
                $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                $"Registering folder '{folderName}'.");
        }

        if (!changed)
            return;

        ExecuteApi(
            "Orders.SetAvailableFolders",
            () => Api.Orders.SetAvailableFolders(availableFolders));
    }

    private void ApplyFolderChanges(
        IReadOnlyCollection<FolderChangePlan> plan,
        Guid locationId,
        string folderPrefix)
    {
        var originallyLocked = plan
            .Where(x => x.WasLocked)
            .Select(x => x.OrderId)
            .Distinct()
            .ToList();

        var originallyParked = plan
            .Where(x => x.WasParked)
            .Select(x => x.OrderId)
            .Distinct()
            .ToList();

        try
        {
            // Existing Linnworks behaviour prevents folder mutation on locked or
            // parked orders. Prepare all affected orders in batches.
            ExecuteInBatches(
                originallyLocked,
                "Orders.LockOrder(false)",
                batch => Api.Orders.LockOrder(batch, false));

            ExecuteInBatches(
                originallyParked,
                "Orders.ChangeOrderTag(null)",
                batch => Api.Orders.ChangeOrderTag(batch, null));

            if (originallyLocked.Count > 0 || originallyParked.Count > 0)
                Thread.Sleep(1500);

            // Assign target folders first. This keeps every order visible in a
            // valid managed folder even if a later unassign operation fails.
            foreach (var targetGroup in plan.GroupBy(
                         x => x.NewFolder,
                         StringComparer.OrdinalIgnoreCase))
            {
                var targetFolder = targetGroup.Key;
                var orderIds = targetGroup
                    .Select(x => x.OrderId)
                    .Distinct()
                    .ToList();

                ExecuteInBatches(
                    orderIds,
                    $"Orders.AssignToFolder('{targetFolder}')",
                    batch =>
                    {
                        var assigned = Api.Orders.AssignToFolder(batch, targetFolder)
                                       ?? new List<Guid>();

                        ValidateReturnedIds(
                            "AssignToFolder",
                            batch,
                            assigned);
                    });
            }

            // Remove every obsolete managed folder after the target folder has
            // been assigned successfully.
            var removalGroups = plan
                .SelectMany(change => change.ObsoleteFolders.Select(folder => new
                {
                    change.OrderId,
                    Folder = folder
                }))
                .GroupBy(
                    x => x.Folder,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var removalGroup in removalGroups)
            {
                var obsoleteFolder = removalGroup.Key;
                var orderIds = removalGroup
                    .Select(x => x.OrderId)
                    .Distinct()
                    .ToList();

                ExecuteInBatches(
                    orderIds,
                    $"Orders.UnassignToFolder('{obsoleteFolder}')",
                    batch =>
                    {
                        var unassigned = Api.Orders.UnassignToFolder(batch, obsoleteFolder)
                                         ?? new List<Guid>();

                        ValidateReturnedIds(
                            "UnassignToFolder",
                            batch,
                            unassigned);
                    });
            }

            Thread.Sleep(1000);
            VerifyFolderChanges(plan, locationId, folderPrefix);
            UpdateOrderExtendedPropertiesAndNotes(plan);
            LogSuccessfulPlan(plan);
        }
        finally
        {
            // Always restore original operational state, including when a folder
            // call fails after some batches have already completed.
            try
            {
                ExecuteInBatches(
                    originallyParked,
                    "Orders.ChangeOrderTag(7)",
                    batch => Api.Orders.ChangeOrderTag(batch, 7));
            }
            catch (Exception ex)
            {
                Logger.WriteError(
                    $"{ContainerEtaFolderSyncConstants.LoggingPrefix} IMPORTANT: " +
                    $"One or more originally parked orders could not be re-parked: {ex}");
            }

            try
            {
                ExecuteInBatches(
                    originallyLocked,
                    "Orders.LockOrder(true)",
                    batch => Api.Orders.LockOrder(batch, true));
            }
            catch (Exception ex)
            {
                Logger.WriteError(
                    $"{ContainerEtaFolderSyncConstants.LoggingPrefix} IMPORTANT: " +
                    $"One or more originally locked orders could not be re-locked: {ex}");
            }
        }
    }

    private void VerifyFolderChanges(
        IReadOnlyCollection<FolderChangePlan> plan,
        Guid locationId,
        string folderPrefix)
    {
        var refreshed = LoadOpenOrders(locationId)
            .ToDictionary(x => x.OrderId, x => x);

        var failed = new List<FolderChangePlan>();

        foreach (var change in plan)
        {
            if (!refreshed.TryGetValue(change.OrderId, out var order))
            {
                failed.Add(change);
                continue;
            }

            var folders = order.FolderName?.ToList() ?? new List<string>();

            var hasTarget = folders.Any(folder =>
                string.Equals(
                    folder,
                    change.NewFolder,
                    StringComparison.OrdinalIgnoreCase));

            var hasObsoleteManagedFolder = folders.Any(folder =>
                IsManagedFolder(folder, folderPrefix) &&
                !string.Equals(
                    folder,
                    change.NewFolder,
                    StringComparison.OrdinalIgnoreCase));

            if (!hasTarget || hasObsoleteManagedFolder)
                failed.Add(change);
        }

        if (failed.Count == 0)
        {
            Logger.WriteInfo(
                $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                $"Verified {plan.Count} folder replacement(s).");
            return;
        }

        foreach (var change in failed.Take(25))
        {
            Logger.WriteError(
                $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                $"Folder verification failed for order {change.OrderNumber} " +
                $"({change.OrderId}); expected '{change.NewFolder}'.");
        }

        if (failed.Count > 25)
        {
            Logger.WriteError(
                $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                $"{failed.Count - 25} additional verification failure(s) were not individually logged.");
        }

        throw new InvalidOperationException(
            $"{failed.Count} order(s) did not reach the expected managed folder state.");
    }

    private void LogSuccessfulPlan(IEnumerable<FolderChangePlan> plan)
    {
        foreach (var change in plan)
        {
            var etaChange = change.OldEta == DateTime.MinValue
                ? $"ETA set to {change.NewEta:dd MMM yyyy}"
                : $"ETA {change.OldEta:dd MMM yyyy} -> {change.NewEta:dd MMM yyyy}";

            Logger.WriteInfo(
                $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                $"Order {change.OrderNumber}, PO {change.PurchaseOrderNumber}: " +
                $"{etaChange}; folder '{change.NewFolder}'.");
        }
    }

    private void ExecuteInBatches(
        IReadOnlyCollection<Guid> orderIds,
        string operationName,
        Action<List<Guid>> operation)
    {
        if (orderIds.Count == 0)
            return;

        var batchNumber = 0;

        foreach (var batch in Chunk(orderIds, MutationBatchSize))
        {
            batchNumber++;
            var currentBatch = batch;

            ExecuteApi(
                $"{operationName}, batch {batchNumber}",
                () => operation(currentBatch));

            Logger.WriteInfo(
                $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                $"{operationName}: batch {batchNumber}, {currentBatch.Count} order(s).");
        }
    }

    private T ExecuteApi<T>(string operationName, Func<T> operation)
    {
        const int maximumAttempts = 5;
        var retryDelaysSeconds = new[] { 5, 15, 30, 60 };

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            PaceApiCall();

            try
            {
                return operation();
            }
            catch (Exception ex) when (
                IsHttp429(ex) &&
                attempt < maximumAttempts)
            {
                var delaySeconds = retryDelaysSeconds[attempt - 1];

                Logger.WriteInfo(
                    $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                    $"{operationName} returned HTTP 429. " +
                    $"Retry {attempt}/{maximumAttempts - 1} in {delaySeconds} second(s).");

                Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
            }
        }

        throw new InvalidOperationException(
            $"{operationName} failed without returning a result.");
    }

    private void ExecuteApi(string operationName, Action operation)
    {
        ExecuteApi<object?>(
            operationName,
            () =>
            {
                operation();
                return null;
            });
    }

    private static void PaceApiCall()
    {
        lock (RateLimitLock)
        {
            var elapsedMilliseconds =
                (DateTime.UtcNow - _lastApiCallUtc).TotalMilliseconds;

            if (elapsedMilliseconds < MinimumApiSpacingMilliseconds)
            {
                Thread.Sleep(
                    MinimumApiSpacingMilliseconds -
                    (int)elapsedMilliseconds);
            }

            _lastApiCallUtc = DateTime.UtcNow;
        }
    }

    private static bool IsHttp429(Exception exception)
    {
        Exception? current = exception;

        while (current != null)
        {
            if (current is WebException webException &&
                webException.Response is HttpWebResponse httpResponse &&
                (int)httpResponse.StatusCode == 429)
            {
                return true;
            }

            if (current.Message.Contains(
                    "(429)",
                    StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains(
                    "Too Many Requests",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private static void ValidateReturnedIds(
        string operationName,
        IReadOnlyCollection<Guid> requestedIds,
        IReadOnlyCollection<Guid> returnedIds)
    {
        // Some Linnworks SDK versions return an empty list on successful folder
        // mutation. Only validate when the endpoint returned explicit IDs.
        if (returnedIds.Count == 0)
            return;

        var missing = requestedIds
            .Where(id => !returnedIds.Contains(id))
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"{operationName} did not return {missing.Count} requested order ID(s).");
        }
    }

    private static string NormalizeFolderPrefix(string? folderPrefix)
    {
        var value = string.IsNullOrWhiteSpace(folderPrefix)
            ? ContainerEtaFolderSyncConstants.FolderPrefixDefault
            : folderPrefix.Trim();

        // Prevent the historical malformed name "Pre-Sale Hold |PO...".
        return value.TrimEnd() + " ";
    }

    private static string BuildContainerFolderName(
        string poNumber,
        DateTime eta,
        string folderPrefix) =>
        $"{folderPrefix}{poNumber.Trim()} | ETA {eta:dd MMM yyyy}";

    private static bool IsManagedFolder(
        string? folderName,
        string folderPrefix)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return false;

        var normalizedFolder = RemoveWhitespace(folderName);
        var normalizedPrefix = RemoveWhitespace(folderPrefix);

        return normalizedFolder.StartsWith(
            normalizedPrefix,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseContainerFolder(
        string folderName,
        string folderPrefix,
        out ParsedContainerFolder parsed)
    {
        parsed = new ParsedContainerFolder();

        if (!IsManagedFolder(folderName, folderPrefix))
            return false;

        var parts = folderName
            .Split('|')
            .Select(x => x.Trim())
            .ToList();

        var etaPartIndex = parts.FindIndex(x =>
            x.StartsWith("ETA ", StringComparison.OrdinalIgnoreCase));

        if (etaPartIndex <= 0)
            return false;

        var poNumber = parts[etaPartIndex - 1].Trim();

        if (string.IsNullOrWhiteSpace(poNumber) ||
            string.Equals(
                poNumber,
                "NO PO AVAILABLE",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                poNumber,
                "EXCEPTION",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var etaText = parts[etaPartIndex]
            .Substring(4)
            .Trim();

        DateTime.TryParseExact(
            etaText,
            "dd MMM yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var eta);

        parsed = new ParsedContainerFolder
        {
            FolderName = folderName,
            PurchaseOrderNumber = poNumber,
            Eta = eta
        };

        return true;
    }

    private static string RemoveWhitespace(string value) =>
        new(value
            .Where(character => !char.IsWhiteSpace(character))
            .ToArray());

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
        ?.Trim() ?? string.Empty;

    private static string? TryReadString(
        object? source,
        string propertyName)
    {
        if (source == null)
            return null;

        var property = source
            .GetType()
            .GetProperty(propertyName);

        return property
            ?.GetValue(source)
            ?.ToString();
    }

    private static IEnumerable<List<T>> Chunk<T>(
        IEnumerable<T> source,
        int size)
    {
        var bucket = new List<T>(size);

        foreach (var item in source)
        {
            bucket.Add(item);

            if (bucket.Count != size)
                continue;

            yield return bucket;
            bucket = new List<T>(size);
        }

        if (bucket.Count > 0)
            yield return bucket;
    }

    private sealed class PurchaseOrderEtaSnapshot
    {
        public Guid PurchaseOrderId { get; init; }
        public string PurchaseOrderNumber { get; init; } = string.Empty;
        public DateTime QuotedDeliveryDate { get; init; }
    }

    private sealed class ParsedContainerFolder
    {
        public string FolderName { get; init; } = string.Empty;
        public string PurchaseOrderNumber { get; init; } = string.Empty;
        public DateTime Eta { get; init; }
    }

    private sealed class FolderChangePlan
    {
        public Guid OrderId { get; init; }
        public int OrderNumber { get; init; }
        public string PurchaseOrderNumber { get; init; } = string.Empty;
        public DateTime OldEta { get; init; }
        public DateTime NewEta { get; init; }
        public string NewFolder { get; init; } = string.Empty;
        public List<string> ObsoleteFolders { get; init; } = new();
        public bool WasParked { get; init; }
        public bool WasLocked { get; init; }
    }

    private void UpdateOrderExtendedPropertiesAndNotes(IReadOnlyCollection<FolderChangePlan> plan)
    {
        foreach (var change in plan)
        {
            try
            {
                var orderId = change.OrderId;
                var newEtaStr = change.NewEta != DateTime.MinValue ? change.NewEta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
                var oldEtaStr = change.OldEta != DateTime.MinValue ? change.OldEta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";

                // 1. Update Extended Properties on the Order
                var eps = ExecuteApi(
                    $"Orders.GetExtendedProperties({orderId})",
                    () => Api.Orders.GetExtendedProperties(orderId) ?? new List<ExtendedProperty>());

                eps = eps.Where(ep =>
                    !string.Equals(ep.Name, ContainerEtaFolderSyncConstants.EpEta, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ep.Name, ContainerEtaFolderSyncConstants.EpOldEta, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ep.Name, ContainerEtaFolderSyncConstants.EpLastEtaUpdatedUtc, StringComparison.OrdinalIgnoreCase)).ToList();

                eps.Add(new ExtendedProperty { RowId = Guid.NewGuid(), Name = ContainerEtaFolderSyncConstants.EpEta, Value = newEtaStr, Type = "Order" });
                if (!string.IsNullOrWhiteSpace(oldEtaStr))
                {
                    eps.Add(new ExtendedProperty { RowId = Guid.NewGuid(), Name = ContainerEtaFolderSyncConstants.EpOldEta, Value = oldEtaStr, Type = "Order" });
                }
                eps.Add(new ExtendedProperty { RowId = Guid.NewGuid(), Name = ContainerEtaFolderSyncConstants.EpLastEtaUpdatedUtc, Value = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), Type = "Order" });

                ExecuteApi(
                    $"Orders.SetExtendedProperties({orderId})",
                    () => Api.Orders.SetExtendedProperties(orderId, eps.ToArray()));

                // 2. Write Internal Note on the Order
                var oldEtaDisplay = change.OldEta != DateTime.MinValue ? change.OldEta.ToString("dd MMM yyyy") : "N/A";
                var newEtaDisplay = change.NewEta != DateTime.MinValue ? change.NewEta.ToString("dd MMM yyyy") : "N/A";
                var noteText = $"Container ETA updated from {oldEtaDisplay} to {newEtaDisplay}. Folder updated to: {change.NewFolder}";

                var existingNotes = ExecuteApi(
                    $"Orders.GetOrderNotes({orderId})",
                    () => Api.Orders.GetOrderNotes(orderId) ?? new List<OrderNote>());

                if (!existingNotes.Any(n => string.Equals(n.Note?.Trim(), noteText.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    existingNotes.Add(new OrderNote
                    {
                        OrderNoteId = Guid.NewGuid(),
                        OrderId = orderId,
                        Note = noteText,
                        Internal = true,
                        NoteDate = DateTime.UtcNow,
                        CreatedBy = "ContainerEtaFolderSync"
                    });

                    ExecuteApi(
                        $"Orders.SetOrderNotes({orderId})",
                        () => Api.Orders.SetOrderNotes(orderId, existingNotes));
                }

                Logger.WriteInfo(
                    $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                    $"Updated extended properties and note for order {change.OrderNumber}: Old ETA='{oldEtaStr}', New ETA='{newEtaStr}'.");
            }
            catch (Exception ex)
            {
                Logger.WriteError(
                    $"{ContainerEtaFolderSyncConstants.LoggingPrefix} " +
                    $"Failed updating extended properties/notes for order {change.OrderNumber}: {ex.Message}");
            }
        }
    }
}

internal static class ContainerEtaFolderSyncConstants
{
    public const string LoggingPrefix =
        "[Rishvi.ContainerEtaFolderSyncMacro]";

    public const string FolderPrefixDefault =
        "Pre-Sale Hold | ";

    public const string EpEta = "CPS.ETA";
    public const string EpOldEta = "CPS.OldETA";
    public const string EpLastEtaUpdatedUtc = "CPS.LastEtaUpdatedUtc";
}
}
