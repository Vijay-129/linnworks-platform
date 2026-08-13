using LinnworksAPI;
using LinnworksMacroHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;

namespace LinnworksMacro._2349
{
    public class Shopify_PaymentMethod_Mapping_MacroGraphQL : LinnworksMacroBase
    {
        private const string ProcessedIdentifierTag = "SHOPIFY_PAYMENT_METHOD_UPDATED";
        private const string ProcessedIdentifierName = "Shopify Payment Method Updated";

        public void Execute(
            Guid[] OrderIds,
            string Source,
            string XMLNodeValue,
            string PaymentMethodMappings)
        {
            try
            {
                Logger.WriteInfo("Shopify payment method mapping macro started.");

                if (OrderIds == null || OrderIds.Length == 0)
                {
                    Logger.WriteInfo("No OrderIds supplied. Macro exiting.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(Source))
                {
                    Logger.WriteError("Source parameter is required. Macro exiting.");
                    return;
                }

                var paymentMethodMapping = ParsePaymentMethodMappings(PaymentMethodMappings);

                if (paymentMethodMapping == null || paymentMethodMapping.Count == 0)
                {
                    Logger.WriteError("PaymentMethodMappings parameter is empty or invalid. Macro exiting.");
                    return;
                }

                EnsureProcessedIdentifierExists();

                string sourceToCheck = Source.Trim();

                string paymentGatewayNodeName = string.IsNullOrWhiteSpace(XMLNodeValue)
                    ? "PaymentGatewayNames"
                    : XMLNodeValue.Trim();

                foreach (var orderId in OrderIds.Distinct())
                {
                    ProcessOrder(orderId, sourceToCheck, paymentGatewayNodeName, paymentMethodMapping);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteError($"Unhandled macro error: {ex.Message}");
            }
            finally
            {
                Logger.WriteInfo("Shopify payment method mapping macro finished.");
            }
        }

        public void ProcessOrder(
            Guid orderId,
            string sourceToCheck,
            string paymentGatewayNodeName,
            Dictionary<string, string> paymentMethodMapping)
        {
            try
            {
                var order = Api.Orders.GetOrderById(orderId);

                if (order == null)
                {
                    Logger.WriteError($"Order not found. OrderId: {orderId}");
                    return;
                }

                if (order.GeneralInfo == null)
                {
                    Logger.WriteInfo($"Order {order.NumOrderId} skipped - GeneralInfo missing.");
                    return;
                }

                if (OrderAlreadyHasProcessedIdentifier(order))
                {
                    Logger.WriteInfo($"Order {order.NumOrderId} skipped - identifier '{ProcessedIdentifierTag}' already exists on this order.");
                    return;
                }

                string orderSource = (order.GeneralInfo.Source ?? "").Trim();

                if (!string.Equals(orderSource, sourceToCheck, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.WriteInfo($"Order {order.NumOrderId} skipped - Source '{orderSource}' does not match '{sourceToCheck}'.");
                    return;
                }

                var orderXmlList = Api.Orders.GetOrderXml(orderId);

                if (orderXmlList == null || !orderXmlList.Any())
                {
                    Logger.WriteInfo($"Order {order.NumOrderId} has no XML records.");
                    return;
                }

                string xmlContent = orderXmlList.First().XML;

                if (string.IsNullOrWhiteSpace(xmlContent))
                {
                    Logger.WriteInfo($"Order {order.NumOrderId} XML content empty.");
                    return;
                }

                XDocument xDoc;

                try
                {
                    xDoc = XDocument.Parse(xmlContent);
                }
                catch (Exception ex)
                {
                    Logger.WriteError($"Order {order.NumOrderId} XML parsing failed. Error: {ex.Message}");
                    return;
                }

                string gatewayName = ExtractMappedGatewayName(xDoc, paymentGatewayNodeName, paymentMethodMapping);

                if (string.IsNullOrWhiteSpace(gatewayName))
                {
                    Logger.WriteInfo($"Order {order.NumOrderId} has no value in XML node '{paymentGatewayNodeName}'.");
                    return;
                }

                if (!paymentMethodMapping.TryGetValue(gatewayName.Trim(), out string targetPaymentMethodName))
                {
                    Logger.WriteInfo($"Order {order.NumOrderId} payment gateway '{gatewayName}' has no mapping. Payment method unchanged.");
                    return;
                }

                if (order.TotalsInfo == null)
                {
                    Logger.WriteInfo($"Order {order.NumOrderId} skipped - TotalsInfo missing.");
                    return;
                }

                string oldPaymentMethod = (order.TotalsInfo.PaymentMethod ?? "").Trim();

                if (string.Equals(oldPaymentMethod, targetPaymentMethodName, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.WriteInfo($"Order {order.NumOrderId} already has payment method '{targetPaymentMethodName}'. Skipping.");
                    return;
                }

                var paymentMethods = Api.Orders.GetPaymentMethods();

                if (paymentMethods == null || !paymentMethods.Any())
                {
                    Logger.WriteError($"No Linnworks payment methods found. Cannot update Order {order.NumOrderId}.");
                    return;
                }

                var targetPaymentMethod = paymentMethods.FirstOrDefault(pm =>
                    pm != null &&
                    !string.IsNullOrWhiteSpace(pm.Name) &&
                    string.Equals(pm.Name.Trim(), targetPaymentMethodName, StringComparison.OrdinalIgnoreCase));

                if (targetPaymentMethod == null)
                {
                    Logger.WriteError($"Mapped Linnworks payment method '{targetPaymentMethodName}' does not exist. Create it in Linnworks first. Order {order.NumOrderId} unchanged.");
                    return;
                }

                bool wasParked = order.GeneralInfo.IsParked;
                bool wasUnparkedForUpdate = false;

                var oldpostagetotal = order.TotalsInfo.PostageCost;
                var oldpostageextax = order.TotalsInfo.PostageCostExTax;
                var oldPostageCostExTax = order.ShippingInfo.PostageCostExTax;
                var oldPostageCost = order.ShippingInfo.PostageCost;
                var tax = order.TotalsInfo.Tax;

                try
                {
                    if (wasParked)
                    {
                        order.GeneralInfo.IsParked = false;
                        Api.Orders.SetOrderGeneralInfo(orderId, order.GeneralInfo, false);
                        wasUnparkedForUpdate = true;
                        Logger.WriteInfo($"Order {order.NumOrderId} unparked for payment method update.");
                    }

                    Logger.WriteInfo($"Order {order.NumOrderId} - Pre-Update ShippingTotal: PostageCostExTax = {order.ShippingInfo.PostageCostExTax}, PostageCost : {order.ShippingInfo.PostageCost}, TotalCost:PostageTaxExTax = {order.TotalsInfo.PostageCostExTax}, PostageCost = {order.TotalsInfo.PostageCost}, PaymentMethod = {order.TotalsInfo.PaymentMethod}, tax = {order.TotalsInfo.Tax}, CountryTaxRate = {order.TotalsInfo.CountryTaxRate}");

                    try
                    {
                        var req = new OrderTotalsInfo
                        {
                            PaymentMethod = targetPaymentMethod.Name,
                            PaymentMethodId = targetPaymentMethod.PaymentMethodId,
                            PostageCostExTax = oldPostageCostExTax,
                            PostageCost = oldPostageCost,
                            Subtotal = order.TotalsInfo.Subtotal,
                            Tax = order.TotalsInfo.Tax,
                            TotalCharge = order.TotalsInfo.TotalCharge,
                            ProfitMargin = order.TotalsInfo.ProfitMargin,
                            TotalDiscount = order.TotalsInfo.TotalDiscount,
                            Currency = order.TotalsInfo.Currency,
                            CountryTaxRate = order.TotalsInfo.CountryTaxRate,
                            ConversionRate = order.TotalsInfo.ConversionRate
                        };

                        Api.Orders.SetOrderTotalsInfo(orderId, req);
                    }
                    catch (WebException webEx)
                    {
                        using (var stream = webEx.Response?.GetResponseStream())
                        using (var reader = new StreamReader(stream))
                        {
                            var responseText = reader.ReadToEnd();
                            Logger.WriteInfo("HTTP Error Body: " + responseText);
                        }
                    }

                    var updatedOrder = Api.Orders.GetOrderById(orderId);
                    Logger.WriteInfo($"Order {updatedOrder.NumOrderId} - Post-Update TotalsInfo: PostageCostExTax = {updatedOrder.ShippingInfo.PostageCostExTax}, PostageCost = {updatedOrder.ShippingInfo.PostageCost}, PaymentMethod = {updatedOrder.TotalsInfo.PaymentMethod}, Tax = {updatedOrder.TotalsInfo.Tax}");

                    Logger.WriteInfo($"Order {order.NumOrderId} payment method updated. Gateway: '{gatewayName}', Old: '{oldPaymentMethod}', New: '{targetPaymentMethod.Name}'.");

                    AddPaymentMethodChangeNote(orderId, order.NumOrderId, oldPaymentMethod, targetPaymentMethod.Name);
                    AssignProcessedIdentifier(orderId, order.NumOrderId);
                }
                finally
                {
                    if (wasParked && wasUnparkedForUpdate)
                    {
                        try
                        {
                            order.GeneralInfo.IsParked = true;
                            Api.Orders.SetOrderGeneralInfo(orderId, order.GeneralInfo, false);
                            Logger.WriteInfo($"Order {order.NumOrderId} parked again after update.");
                        }
                        catch (Exception parkEx)
                        {
                            Logger.WriteError($"Failed to park Order {order.NumOrderId} again after payment method update. Error: {parkEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteError($"Error processing Order {orderId}: {ex.Message}");
            }
        }

        private Dictionary<string, string> ParsePaymentMethodMappings(string paymentMethodMappings)
        {
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(paymentMethodMappings))
            {
                return mappings;
            }

            var mappingPairs = paymentMethodMappings.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var pair in mappingPairs)
            {
                var cleanedPair = pair.Trim();

                if (string.IsNullOrWhiteSpace(cleanedPair))
                {
                    continue;
                }

                var separatorIndex = cleanedPair.IndexOf('=');

                if (separatorIndex <= 0 || separatorIndex == cleanedPair.Length - 1)
                {
                    Logger.WriteInfo($"Invalid payment mapping skipped: '{cleanedPair}'. Expected format: XMLValue=Linnworks Payment Method.");
                    continue;
                }

                string xmlGatewayValue = cleanedPair.Substring(0, separatorIndex).Trim();
                string linnworksPaymentMethod = cleanedPair.Substring(separatorIndex + 1).Trim();

                if (string.IsNullOrWhiteSpace(xmlGatewayValue) || string.IsNullOrWhiteSpace(linnworksPaymentMethod))
                {
                    Logger.WriteInfo($"Invalid payment mapping skipped: '{cleanedPair}'. Gateway or payment method is blank.");
                    continue;
                }

                mappings[xmlGatewayValue] = linnworksPaymentMethod;

                Logger.WriteInfo($"Payment mapping loaded: XML value '{xmlGatewayValue}' => Linnworks payment method '{linnworksPaymentMethod}'.");
            }

            return mappings;
        }

        private void EnsureProcessedIdentifierExists()
        {
            try
            {
                var identifiers = Api.OpenOrders.GetIdentifiers();

                bool identifierExists = identifiers != null &&
                    identifiers.Any(i =>
                        i != null &&
                        (
                            string.Equals(i.Tag, ProcessedIdentifierTag, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(i.Name, ProcessedIdentifierName, StringComparison.OrdinalIgnoreCase)
                        ));

                if (identifierExists)
                {
                    Logger.WriteInfo($"Identifier '{ProcessedIdentifierTag}' already exists. Creation skipped.");
                    return;
                }

                var saveIdentifierRequest = new SaveIdentifiersRequest
                {
                    Identifier = new Identifier
                    {
                        Tag = ProcessedIdentifierTag,
                        Name = ProcessedIdentifierName,
                        ImageId = Guid.Empty,
                        ImageUrl = null,
                        IsCustom = true
                    }
                };

                var result = Api.OpenOrders.SaveIdentifier(saveIdentifierRequest);

                Logger.WriteInfo($"Identifier '{ProcessedIdentifierTag}' did not exist and has been created. IdentifierId: {result.IdentifierId}");
            }
            catch (Exception ex)
            {
                Logger.WriteError($"Failed to check/create identifier '{ProcessedIdentifierTag}'. Error: {ex.Message}");
                throw;
            }
        }

        private bool OrderAlreadyHasProcessedIdentifier(OrderDetails order)
        {
            try
            {
                var identifiers = order.GeneralInfo?.Identifiers;

                if (identifiers == null || identifiers.Count == 0)
                {
                    return false;
                }

                return identifiers.Any(i =>
                    i != null &&
                    (
                        string.Equals(i.Tag, ProcessedIdentifierTag, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(i.Name, ProcessedIdentifierName, StringComparison.OrdinalIgnoreCase)
                    ));
            }
            catch (Exception ex)
            {
                Logger.WriteError($"Failed to check existing identifiers on Order {order.NumOrderId}. Error: {ex.Message}");
                return false;
            }
        }

        private void AssignProcessedIdentifier(Guid orderId, int numOrderId)
        {
            try
            {
                Api.OpenOrders.AssignOrderIdentifier(new ChangeOrderIdentifierRequest
                {
                    OrderIds = new[] { orderId },
                    Tag = ProcessedIdentifierTag
                });

                Logger.WriteInfo($"Order {numOrderId} tagged with identifier '{ProcessedIdentifierTag}'.");
            }
            catch (Exception identifierEx)
            {
                Logger.WriteError($"Failed to add identifier to Order {numOrderId}: {identifierEx.Message}");
            }
        }

        private void AddPaymentMethodChangeNote(
            Guid orderId,
            int numOrderId,
            string oldPaymentMethod,
            string newPaymentMethod)
        {
            try
            {
                string oldValueForNote = string.IsNullOrWhiteSpace(oldPaymentMethod) ? "[blank]" : oldPaymentMethod.Trim();
                string newValueForNote = string.IsNullOrWhiteSpace(newPaymentMethod) ? "[blank]" : newPaymentMethod.Trim();

                string noteText = $"Payment method changed by macro. Old value: {oldValueForNote}. New method value: {newValueForNote}.";

                var existingNotes = Api.Orders.GetOrderNotes(orderId) ?? new List<OrderNote>();

                existingNotes.Add(new OrderNote
                {
                    OrderNoteId = Guid.NewGuid(),
                    OrderId = orderId,
                    Note = noteText,
                    Internal = true,
                    NoteDate = DateTime.UtcNow,
                    CreatedBy = "Tools4Trade"
                });

                Api.Orders.SetOrderNotes(orderId, existingNotes);

                Logger.WriteInfo($"Payment method change note added to Order {numOrderId}.");
            }
            catch (Exception noteEx)
            {
                Logger.WriteError($"Failed to add payment method change note to Order {numOrderId}: {noteEx.Message}");
            }
        }

        private string ExtractMappedGatewayName(
            XDocument xDoc,
            string paymentGatewayNodeName,
            Dictionary<string, string> paymentMethodMapping)
        {
            if (xDoc == null)
            {
                return null;
            }

            /*
             * Legacy Shopify REST XML:
             * <PaymentGatewayNames>
             *     <string>shopify_payments</string>
             * </PaymentGatewayNames>
             *
             * Shopify GraphQL XML:
             * <paymentGatewayNames>
             *     <string>shopify_payments</string>
             * </paymentGatewayNames>
             */

            var gatewayNodeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                paymentGatewayNodeName,
                "paymentGatewayNames"
            };

            var gatewayValues = xDoc
                .Descendants()
                .Where(element => gatewayNodeNames.Contains(element.Name.LocalName))
                .SelectMany(node =>
                {
                    var leafValues = node
                        .Descendants()
                        .Where(element => !element.HasElements)
                        .Select(element => (element.Value ?? "").Trim())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToList();

                    if (leafValues.Any())
                    {
                        return leafValues;
                    }

                    string directValue = (node.Value ?? "").Trim();

                    return string.IsNullOrWhiteSpace(directValue)
                        ? new List<string>()
                        : new List<string> { directValue };
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!gatewayValues.Any())
            {
                return null;
            }

            foreach (var gatewayValue in gatewayValues)
            {
                if (paymentMethodMapping.ContainsKey(gatewayValue))
                {
                    return gatewayValue;
                }
            }

            return gatewayValues.FirstOrDefault();
        }
    }
}
