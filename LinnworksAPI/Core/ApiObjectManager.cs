using System;

namespace LinnworksAPI
{
    /// <summary>
    /// Entry point macros/plugins use to reach every controller. Grows one property
    /// at a time as each controller is promoted out of migration/STATUS.md - do not
    /// pre-declare properties for controllers that haven't been rewritten yet.
    /// </summary>
    public class ApiObjectManager
    {
        private readonly ApiContext apiContext;
        private LocationsController locations;
        private AuthController auth;
        private PostalServicesController postalServices;
        private OrdersController orders;
        private DashboardsController dashboards;
        private EmailController email;
        private GenericListingsController genericListings;
        private ImportExportController importExport;
        private InventoryController inventory;
        private ListingsController listings;
        private MacroController macro;
        private OpenOrdersController openOrders;
        private PickingController picking;
        private PostSaleController postSale;
        private PrintServiceController printService;
        private ProcessedOrdersController processedOrders;
        private PurchaseOrderController purchaseOrder;
        private ReturnsRefundsController returnsRefunds;
        private RulesEngineController rulesEngine;
        private SettingsController settings;
        private ShipStationController shipStation;
        private ShippingServiceController shippingService;
        private StockController stock;
        private WarehouseTransferController warehouseTransfer;
        private WmsController wms;
        private CustomerController customer;
        private OrderPrintStatusController orderPrintStatus;
        private OrderWorkflowController orderWorkflow;

        public ApiObjectManager(ApiContext apiContext)
        {
            this.apiContext = apiContext;
        }

        public Guid GetSessionId()
        {
            return apiContext.SessionId;
        }

        public LocationsController Locations
        {
            get { return locations ?? (locations = new LocationsController(apiContext)); }
        }

        public AuthController Auth
        {
            get { return auth ?? (auth = new AuthController(apiContext)); }
        }

        public PostalServicesController PostalServices
        {
            get { return postalServices ?? (postalServices = new PostalServicesController(apiContext)); }
        }

        public OrdersController Orders
        {
            get { return orders ?? (orders = new OrdersController(apiContext)); }
        }

        public DashboardsController Dashboards
        {
            get { return dashboards ?? (dashboards = new DashboardsController(apiContext)); }
        }

        public EmailController Email
        {
            get { return email ?? (email = new EmailController(apiContext)); }
        }

        public GenericListingsController GenericListings
        {
            get { return genericListings ?? (genericListings = new GenericListingsController(apiContext)); }
        }

        public ImportExportController ImportExport
        {
            get { return importExport ?? (importExport = new ImportExportController(apiContext)); }
        }

        public InventoryController Inventory
        {
            get { return inventory ?? (inventory = new InventoryController(apiContext)); }
        }

        public ListingsController Listings
        {
            get { return listings ?? (listings = new ListingsController(apiContext)); }
        }

        public MacroController Macro
        {
            get { return macro ?? (macro = new MacroController(apiContext)); }
        }

        public OpenOrdersController OpenOrders
        {
            get { return openOrders ?? (openOrders = new OpenOrdersController(apiContext)); }
        }

        public PickingController Picking
        {
            get { return picking ?? (picking = new PickingController(apiContext)); }
        }

        public PostSaleController PostSale
        {
            get { return postSale ?? (postSale = new PostSaleController(apiContext)); }
        }

        public PrintServiceController PrintService
        {
            get { return printService ?? (printService = new PrintServiceController(apiContext)); }
        }

        public ProcessedOrdersController ProcessedOrders
        {
            get { return processedOrders ?? (processedOrders = new ProcessedOrdersController(apiContext)); }
        }

        public PurchaseOrderController PurchaseOrder
        {
            get { return purchaseOrder ?? (purchaseOrder = new PurchaseOrderController(apiContext)); }
        }

        public ReturnsRefundsController ReturnsRefunds
        {
            get { return returnsRefunds ?? (returnsRefunds = new ReturnsRefundsController(apiContext)); }
        }

        public RulesEngineController RulesEngine
        {
            get { return rulesEngine ?? (rulesEngine = new RulesEngineController(apiContext)); }
        }

        public SettingsController Settings
        {
            get { return settings ?? (settings = new SettingsController(apiContext)); }
        }

        public ShipStationController ShipStation
        {
            get { return shipStation ?? (shipStation = new ShipStationController(apiContext)); }
        }

        public ShippingServiceController ShippingService
        {
            get { return shippingService ?? (shippingService = new ShippingServiceController(apiContext)); }
        }

        public StockController Stock
        {
            get { return stock ?? (stock = new StockController(apiContext)); }
        }

        public WarehouseTransferController WarehouseTransfer
        {
            get { return warehouseTransfer ?? (warehouseTransfer = new WarehouseTransferController(apiContext)); }
        }

        public WmsController Wms
        {
            get { return wms ?? (wms = new WmsController(apiContext)); }
        }

        public CustomerController Customer
        {
            get { return customer ?? (customer = new CustomerController(apiContext)); }
        }

        public OrderPrintStatusController OrderPrintStatus
        {
            get { return orderPrintStatus ?? (orderPrintStatus = new OrderPrintStatusController(apiContext)); }
        }

        public OrderWorkflowController OrderWorkflow
        {
            get { return orderWorkflow ?? (orderWorkflow = new OrderWorkflowController(apiContext)); }
        }
    }
}
