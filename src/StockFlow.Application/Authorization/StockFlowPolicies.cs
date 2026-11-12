namespace StockFlow.Application.Authorization
{
    public static class StockFlowPolicies
    {
        public const string CatalogRead = "Catalog.Read";
        public const string InventoryRead = "Inventory.Read";
        public const string InventoryReceive = "Inventory.Receive";
        public const string SuppliersRead = "Suppliers.Read";
        public const string SuppliersManage = "Suppliers.Manage";
        public const string BackgroundJobsManage = "BackgroundJobs.Manage";
        public const string PurchaseOrdersRead = "PurchaseOrders.Read";
        public const string PurchaseOrdersManage = "PurchaseOrders.Manage";
        public const string PurchaseOrdersApprove = "PurchaseOrders.Approve";
    }
}
