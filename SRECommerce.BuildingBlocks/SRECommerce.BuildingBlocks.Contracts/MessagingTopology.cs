namespace SRECommerce.BuildingBlocks.Contracts;

public static class MessagingTopology
{
    public const string EventsExchange = "srecommerce.events";
    public static readonly string[] CatalogEvents = ["catalog.product.published.v1", "catalog.product.archived.v1"];
    public static readonly string[] InventoryEvents = ["inventory.low-stock.v1"];
    public static readonly string[] OrderEvents = OrderNoticesContract.Types;
    public static string[] Subscriptions(string service) => service switch
    {
        "Discovery" => CatalogEvents,
        "Notification" => [..OrderEvents, ..InventoryEvents],
        _ => []
    };
    public static string[] Publications(string service) => service switch
    {
        "Catalog" => CatalogEvents,
        "Inventory" => InventoryEvents,
        "Order" => OrderEvents,
        _ => []
    };
    public static string Prefix(string service) => "srecommerce." + ServiceManifest.Get(service).Name.ToLowerInvariant();
    public static string Queue(string service) => Prefix(service) + ".events";
    public static string RetryExchange(string service) => Prefix(service) + ".retry";
    public static string DeliveryExchange(string service) => Prefix(service) + ".delivery";
    public static string DeadExchange(string service) => Prefix(service) + ".dead";
}
