using System;
using System.Collections.Generic;
using System.Linq;

namespace LinnworksAPI.V2
{
    /// <summary>
    /// v2 Orders. No v1 SDK equivalent exists to port from - written directly against
    /// references/api/v2/Orders.md (linnworks-api-python-main/PublicApiSpecs/2.0/orders-v2.json).
    /// GetOrders can return either GetOrdersResponse or AnonymousGetOrdersResponse
    /// (oneOf in the spec, depending on account permissions) - this always requests the
    /// named-customer shape; if the account only has anonymous access the CustomerInfo/Notes
    /// fields on the result will simply be absent from the raw JSON and left at their
    /// default values.
    /// </summary>
    public class OrdersController
    {
        private readonly ApiContextV2 apiContext;

        public OrdersController(ApiContextV2 apiContext)
        {
            this.apiContext = apiContext;
        }

        /// <summary>
        /// Get orders.
        /// </summary>
        public GetOrdersResponse GetOrders(List<Guid> id = null, DateTime? fromDate = null, int? entriesPerPage = null, bool? includeProcessed = null, bool? onlyPaid = null, Guid? locationId = null, Guid? searchToken = null)
        {
            var query = new Dictionary<string, string>
            {
                ["id"] = id != null && id.Count > 0 ? string.Join(",", id) : null,
                ["fromDate"] = fromDate?.ToString("O"),
                ["entriesPerPage"] = entriesPerPage?.ToString(),
                ["includeProcessed"] = includeProcessed?.ToString(),
                ["onlyPaid"] = onlyPaid?.ToString(),
                ["locationId"] = locationId?.ToString(),
                ["searchToken"] = searchToken?.ToString(),
            };
            return RestClient.Send<GetOrdersResponse>(apiContext, "GET", "orders", query);
        }

        /// <summary>
        /// Get fulfillment status for requested order ids.
        /// </summary>
        public List<OrderFulfillmentStatus> GetFulfillmentStatuses(List<Guid> id = null, Guid? locationId = null, FulfillmentStatus? fulfillmentStatus = null)
        {
            var query = new Dictionary<string, string>
            {
                ["id"] = id != null && id.Count > 0 ? string.Join(",", id) : null,
                ["locationId"] = locationId?.ToString(),
                ["fulfillmentStatus"] = fulfillmentStatus?.ToString(),
            };
            return RestClient.Send<List<OrderFulfillmentStatus>>(apiContext, "GET", "orders/fulfillment-status", query);
        }

        /// <summary>
        /// Update fulfillment status for requested order ids.
        /// </summary>
        public UpdateFulfillmentStatusesResponse UpdateFulfillmentStatuses(List<OrderFulfillmentStatus> statuses)
        {
            return RestClient.Send<UpdateFulfillmentStatusesResponse>(apiContext, "POST", "orders/fulfillment-status", body: statuses);
        }

        /// <summary>
        /// Get fulfillment status for a single order id.
        /// </summary>
        public OrderFulfillmentStatus GetFulfillmentStatus(Guid orderId)
        {
            return RestClient.Send<OrderFulfillmentStatus>(apiContext, "GET", $"orders/{orderId}/fulfillment-status");
        }

        /// <summary>
        /// Update fulfillment status for a single order id.
        /// </summary>
        public OrderFulfillmentStatus UpdateFulfillmentStatus(Guid orderId, FulfillmentStatusRequest request)
        {
            return RestClient.Send<OrderFulfillmentStatus>(apiContext, "PUT", $"orders/{orderId}/fulfillment-status", body: request);
        }
    }
}
