using System;
using System.Collections.Generic;

namespace LinnworksAPI.V2
{
    /// <summary>
    /// v2 WarehouseTransfer. No v1 SDK equivalent exists to port from - generated directly
    /// against linnworks-api-python-main/PublicApiSpecs/2.0/warehousetransfer-v2.json via scripts/generate_v2_controller.py.
    /// This spec has no operationId on most operations, so method names below were
    /// mechanically derived from HTTP verb + path, not Linnworks-authored - treat them
    /// as provisional pending confirmation against real usage or official docs.
    /// </summary>
    public class WarehouseTransferController
    {
        private readonly ApiContextV2 apiContext;

        public WarehouseTransferController(ApiContextV2 apiContext)
        {
            this.apiContext = apiContext;
        }

        /// <summary>
        /// GetMetaData
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public GetViewMetaDataResponse GetFbaInboundMetadata()
        {
            return RestClient.Send<GetViewMetaDataResponse>(apiContext, "GET", "warehousetransfer/fba-inbound/metadata", null, null);
        }

        /// <summary>
        /// GetOperationById
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public OperationModel GetFbaInboundOperationsByOperationId(Guid operationId)
        {
            return RestClient.Send<OperationModel>(apiContext, "GET", $"warehousetransfer/fba-inbound/operations/{operationId}", null, null);
        }

        /// <summary>
        /// Get
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<GetPackingGroupModel> GetFbaInboundShippingPlansByShippingPlanIdPackingGroups(Int32 shippingPlanId)
        {
            return RestClient.Send<List<GetPackingGroupModel>>(apiContext, "GET", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/packing-groups", null, null);
        }

        /// <summary>
        /// Get
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<PackingOptionModel> GetFbaInboundShippingPlansByShippingPlanIdPackingOptions(Int32 shippingPlanId)
        {
            return RestClient.Send<List<PackingOptionModel>>(apiContext, "GET", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/packing-options", null, null);
        }

        /// <summary>
        /// Confirm
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public OperationModel CreateFbaInboundShippingPlansByShippingPlanIdPackingOptionsByOptionIdConfirm(Int32 shippingPlanId, String optionId)
        {
            return RestClient.Send<OperationModel>(apiContext, "POST", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/packing-options/{optionId}/confirm", null, null);
        }

        /// <summary>
        /// Generate
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public OperationModel CreateFbaInboundShippingPlansByShippingPlanIdPackingOptionsGenerate(Int32 shippingPlanId)
        {
            return RestClient.Send<OperationModel>(apiContext, "POST", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/packing-options/generate", null, null);
        }

        /// <summary>
        /// Get
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<PlacementOptionModel> GetFbaInboundShippingPlansByShippingPlanIdPlacementOptions(Int32 shippingPlanId)
        {
            return RestClient.Send<List<PlacementOptionModel>>(apiContext, "GET", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/placement-options", null, null);
        }

        /// <summary>
        /// Confirm
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public OperationModel CreateFbaInboundShippingPlansByShippingPlanIdPlacementOptionsByOptionIdConfirm(Int32 shippingPlanId, String optionId)
        {
            return RestClient.Send<OperationModel>(apiContext, "POST", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/placement-options/{optionId}/confirm", null, null);
        }

        /// <summary>
        /// Generate
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public OperationModel CreateFbaInboundShippingPlansByShippingPlanIdPlacementOptionsGenerate(Int32 shippingPlanId)
        {
            return RestClient.Send<OperationModel>(apiContext, "POST", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/placement-options/generate", null, null);
        }

        /// <summary>
        /// AddShipmentBoxes
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<ShipmentBoxModel> CreateFbaInboundShippingPlansByShippingPlanIdBoxes(Int32 shippingPlanId)
        {
            return RestClient.Send<List<ShipmentBoxModel>>(apiContext, "POST", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/boxes", null, null);
        }

        /// <summary>
        /// UpdateShipmentBoxes
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<ShipmentBoxModel> UpdateFbaInboundShippingPlansByShippingPlanIdBoxes(Int32 shippingPlanId)
        {
            return RestClient.Send<List<ShipmentBoxModel>>(apiContext, "PUT", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/boxes", null, null);
        }

        /// <summary>
        /// GetShipmentBoxes
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<ShipmentBoxModel> GetFbaInboundShippingPlansByShippingPlanIdBoxes(Int32 shippingPlanId)
        {
            return RestClient.Send<List<ShipmentBoxModel>>(apiContext, "GET", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/boxes", null, null);
        }

        /// <summary>
        /// DeleteShipmentBoxes
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public void DeleteFbaInboundShippingPlansByShippingPlanIdBoxesByShipmentBoxId(Int32 shippingPlanId, Int32 shipmentBoxId)
        {
            RestClient.Send(apiContext, "DELETE", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/boxes/{shipmentBoxId}", null, null);
        }

        /// <summary>
        /// AddShipmentBoxItems
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<ShipmentBoxItemModel> CreateFbaInboundShippingPlansByShippingPlanIdBoxItems(Int32 shippingPlanId)
        {
            return RestClient.Send<List<ShipmentBoxItemModel>>(apiContext, "POST", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/box-items", null, null);
        }

        /// <summary>
        /// UpdateShipmentBoxItems
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<ShipmentBoxItemModel> UpdateFbaInboundShippingPlansByShippingPlanIdBoxItems(Int32 shippingPlanId)
        {
            return RestClient.Send<List<ShipmentBoxItemModel>>(apiContext, "PUT", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/box-items", null, null);
        }

        /// <summary>
        /// DeleteShipmentBoxItems
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public void DeleteFbaInboundShippingPlansByShippingPlanIdBoxItems(Int32 shippingPlanId, List<Int32> shipmentBoxItemIds)
        {
            var query = new Dictionary<string, string>
            {
                ["shipmentBoxItemIds"] = shipmentBoxItemIds?.ToString(),
            };
            RestClient.Send(apiContext, "DELETE", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/box-items", query, null);
        }

        /// <summary>
        /// GetShipmentBoxItems
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public GetShipmentBoxItemsResponse GetFbaInboundShippingPlansByShippingPlanIdBoxItemsPackingGroupsByPackingGroupId(Int32 shippingPlanId, Int32 packingGroupId)
        {
            return RestClient.Send<GetShipmentBoxItemsResponse>(apiContext, "GET", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/box-items/packing-groups/{packingGroupId}", null, null);
        }

        /// <summary>
        /// ListDeliveryWindowOptions
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<ListDeliveryWindowOptionResponse> GetFbaInboundShippingPlansByShippingPlanIdShipmentsByShipmentIdDeliveryWindowOptions(Int32 shippingPlanId, Int32 shipmentId)
        {
            return RestClient.Send<List<ListDeliveryWindowOptionResponse>>(apiContext, "GET", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/{shipmentId}/delivery-window-options", null, null);
        }

        /// <summary>
        /// GenerateDeliveryWindowOptions
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public OperationModel CreateFbaInboundShippingPlansByShippingPlanIdShipmentsByShipmentIdDeliveryWindowOptionsGenerate(Int32 shippingPlanId, Int32 shipmentId)
        {
            return RestClient.Send<OperationModel>(apiContext, "POST", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/{shipmentId}/delivery-window-options/generate", null, null);
        }

        /// <summary>
        /// ConfirmDeliveryWindowOptions
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public OperationModel CreateFbaInboundShippingPlansByShippingPlanIdShipmentsByShipmentIdDeliveryWindowOptionsByDeliveryWindowOptionIdConfirm(Int32 shippingPlanId, Int32 shipmentId, String deliveryWindowOptionId)
        {
            return RestClient.Send<OperationModel>(apiContext, "POST", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/{shipmentId}/delivery-window-options/{deliveryWindowOptionId}/confirm", null, null);
        }

        /// <summary>
        /// CreateShipmentItemBatch
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<ShipmentItemResponse> CreateFbaInboundShippingPlansByShippingPlanIdShipmentsItems(Int32 shippingPlanId)
        {
            return RestClient.Send<List<ShipmentItemResponse>>(apiContext, "POST", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/items", null, null);
        }

        /// <summary>
        /// DeleteShipmentItem
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public void DeleteFbaInboundShippingPlansByShippingPlanIdShipmentsItems(String shippingPlanId)
        {
            RestClient.Send(apiContext, "DELETE", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/items", null, null);
        }

        /// <summary>
        /// GetShipmentItems
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<ShipmentItemResponse> GetFbaInboundShippingPlansByShippingPlanIdShipmentsItems(Int32 shippingPlanId, List<Int32> shipmentItemId = null)
        {
            var query = new Dictionary<string, string>
            {
                ["shipmentItemId"] = shipmentItemId?.ToString(),
            };
            return RestClient.Send<List<ShipmentItemResponse>>(apiContext, "GET", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/items", query, null);
        }

        /// <summary>
        /// UpdateShipmentItem
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public void UpdateFbaInboundShippingPlansByShippingPlanIdShipmentsItemsByShipmentItemId(Int32 shippingPlanId, Int32 shipmentItemId)
        {
            RestClient.Send(apiContext, "PUT", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/items/{shipmentItemId}", null, null);
        }

        /// <summary>
        /// UpdateQuantity
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public ShipmentItemResponse PatchFbaInboundShippingPlansByShippingPlanIdShipmentsItemsByShipmentItemIdQuantity(Int32 shippingPlanId, Int32 shipmentItemId)
        {
            return RestClient.Send<ShipmentItemResponse>(apiContext, "PATCH", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/items/{shipmentItemId}/quantity", null, null);
        }

        /// <summary>
        /// UpdateShipmentItemPrepInstruction
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<ShipmentItemPrepInstructionModel> UpdateFbaInboundShippingPlansByShippingPlanIdShipmentsItemsPrepInstructions(String shippingPlanId)
        {
            return RestClient.Send<List<ShipmentItemPrepInstructionModel>>(apiContext, "PUT", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/items/prep-instructions", null, null);
        }

        /// <summary>
        /// UpdateShippingItemWhoLabelPrep
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public void UpdateFbaInboundShippingPlansByShippingPlanIdShipmentsItemsPrepInstructionsPrepOwner(String shippingPlanId)
        {
            RestClient.Send(apiContext, "PUT", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/items/prep-instructions/prep-owner", null, null);
        }

        /// <summary>
        /// UpdateShippingItemLabelOwner
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public ShipmentItemResponse UpdateFbaInboundShippingPlansByShippingPlanIdShipmentsItemsPrepInstructionsLabelOwner(Int32 shippingPlanId)
        {
            return RestClient.Send<ShipmentItemResponse>(apiContext, "PUT", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/items/prep-instructions/label-owner", null, null);
        }

        /// <summary>
        /// AddFbaItemBatch
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public Boolean CreateFbaInboundShippingPlansByShippingPlanIdShipmentsItemsBatches(Int32 shippingPlanId)
        {
            return RestClient.Send<Boolean>(apiContext, "POST", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/items/batches", null, null);
        }

        /// <summary>
        /// GetShipmentById
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public ShipmentModel GetFbaInboundShippingPlansByShippingPlanIdShipmentsByShipmentId(Int32 shipmentId, String shippingPlanId)
        {
            return RestClient.Send<ShipmentModel>(apiContext, "GET", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/{shipmentId}", null, null);
        }

        /// <summary>
        /// GetLabelByShipmentId
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public GetLabelByShipmentIdResponse GetFbaInboundShippingPlansByShippingPlanIdShipmentsByShipmentIdLabels(Int32 shipmentId, String shippingPlanId, String pageType = null, String labelType = null)
        {
            var query = new Dictionary<string, string>
            {
                ["pageType"] = pageType?.ToString(),
                ["labelType"] = labelType?.ToString(),
            };
            return RestClient.Send<GetLabelByShipmentIdResponse>(apiContext, "GET", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/{shipmentId}/labels", query, null);
        }

        /// <summary>
        /// GetBillOfLading
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public GetBillOfLadingByShipmentIdResponse GetFbaInboundShippingPlansByShippingPlanIdShipmentsByShipmentIdBillOfLading(Int32 shipmentId, String shippingPlanId)
        {
            return RestClient.Send<GetBillOfLadingByShipmentIdResponse>(apiContext, "GET", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments/{shipmentId}/bill-of-lading", null, null);
        }

        /// <summary>
        /// GetShipmentsByShippingPlanId
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<ShipmentResponse> GetFbaInboundShippingPlansByShippingPlanIdShipments(Int32 shippingPlanId)
        {
            return RestClient.Send<List<ShipmentResponse>>(apiContext, "GET", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/shipments", null, null);
        }

        /// <summary>
        /// CreateShippingPlan
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public GetShippingPlanCardsResponse CreateFbaInboundShippingPlans()
        {
            return RestClient.Send<GetShippingPlanCardsResponse>(apiContext, "POST", "warehousetransfer/fba-inbound/shipping-plans", null, null);
        }

        /// <summary>
        /// DeleteShippingPlan
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public void DeleteFbaInboundShippingPlansByShippingPlanId(Int32 shippingPlanId)
        {
            RestClient.Send(apiContext, "DELETE", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}", null, null);
        }

        /// <summary>
        /// GetFbaShippingPlanById
        /// </summary>
        public GetShippingPlanByIdResponse GetFbaShippingPlanById(Int32 shippingPlanId)
        {
            return RestClient.Send<GetShippingPlanByIdResponse>(apiContext, "GET", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}", null, null);
        }

        /// <summary>
        /// UpdateShippingPlan
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public GetShippingPlanCardsResponse UpdateFbaInboundShippingPlansByShippingPlanId(Int32 shippingPlanId)
        {
            return RestClient.Send<GetShippingPlanCardsResponse>(apiContext, "PUT", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}", null, null);
        }

        /// <summary>
        /// SubmitShippingPlan
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public GetShippingPlanCardsResponse UpdateFbaInboundShippingPlansByShippingPlanIdSubmit(Int32 shippingPlanId)
        {
            return RestClient.Send<GetShippingPlanCardsResponse>(apiContext, "PUT", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/submit", null, null);
        }

        /// <summary>
        /// SetPackingInformation
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public OperationModel CreateFbaInboundShippingPlansByShippingPlanIdPackingInformation(Int32 shippingPlanId)
        {
            return RestClient.Send<OperationModel>(apiContext, "POST", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/packing-information", null, null);
        }

        /// <summary>
        /// GetStockItemBatchesByShippingPlanId
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<StockItemBatchResponse> GetFbaInboundShippingPlansByShippingPlanIdStockItemsBatches(Int32 shippingPlanId, List<Int32> stockItemIds = null)
        {
            var query = new Dictionary<string, string>
            {
                ["stockItemIds"] = stockItemIds?.ToString(),
            };
            return RestClient.Send<List<StockItemBatchResponse>>(apiContext, "GET", $"warehousetransfer/fba-inbound/shipping-plans/{shippingPlanId}/stock-items/batches", query, null);
        }

        /// <summary>
        /// GetShippingPlanCards
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public List<GetShippingPlanCardsResponse> GetFbaInboundTransferCardsShippingPlans()
        {
            return RestClient.Send<List<GetShippingPlanCardsResponse>>(apiContext, "GET", "warehousetransfer/fba-inbound/transfer-cards/shipping-plans", null, null);
        }

        /// <summary>
        /// GetShippingPlanCardById
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public GetShippingPlanCardsResponse GetFbaInboundTransferCardsShippingPlansByShippingPlanId(Int32 shippingPlanId)
        {
            return RestClient.Send<GetShippingPlanCardsResponse>(apiContext, "GET", $"warehousetransfer/fba-inbound/transfer-cards/shipping-plans/{shippingPlanId}", null, null);
        }

        /// <summary>
        /// GetShipmentCards
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public GetShipmentCardsResponse GetFbaInboundTransferCardsShipments()
        {
            return RestClient.Send<GetShipmentCardsResponse>(apiContext, "GET", "warehousetransfer/fba-inbound/transfer-cards/shipments", null, null);
        }

        /// <summary>
        /// RequestShipmentCardsUpdate
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public void CreateFbaInboundTransferCardsRefresh()
        {
            RestClient.Send(apiContext, "POST", "warehousetransfer/fba-inbound/transfer-cards/refresh", null, null);
        }

        /// <summary>
        /// GetTransferById
        /// (method name derived from path - no operationId in spec)
        /// </summary>
        public WarehouseTransferModel GetTransfersById(Int32 id)
        {
            return RestClient.Send<WarehouseTransferModel>(apiContext, "GET", $"warehousetransfer/transfers/{id}", null, null);
        }
    }
}
