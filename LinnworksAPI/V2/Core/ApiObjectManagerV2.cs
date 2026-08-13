namespace LinnworksAPI.V2
{
    /// <summary>
    /// v2 entry point, separate from v1's ApiObjectManager since v2 needs an
    /// ApiContextV2 (session + locality), not v1's ApiContext (session + resolved
    /// server URL). Build the ApiContextV2 from the BaseSession.Locality returned by
    /// v1's Auth.AuthorizeByApplication - v2 has no separate auth flow of its own.
    /// Grows one property per controller as each is written, same as v1's.
    /// </summary>
    public class ApiObjectManagerV2
    {
        private readonly ApiContextV2 apiContext;
        private OrdersController orders;
        private WarehouseTransferController warehouseTransfer;

        public ApiObjectManagerV2(ApiContextV2 apiContext)
        {
            this.apiContext = apiContext;
        }

        public OrdersController Orders
        {
            get { return orders ?? (orders = new OrdersController(apiContext)); }
        }

        public WarehouseTransferController WarehouseTransfer
        {
            get { return warehouseTransfer ?? (warehouseTransfer = new WarehouseTransferController(apiContext)); }
        }
    }
}
