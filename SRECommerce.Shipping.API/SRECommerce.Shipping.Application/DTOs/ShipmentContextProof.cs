namespace SRECommerce.Shipping.Application;
public sealed record ShipmentContextProof(FulfillmentProof Fulfillment,ShippingOrderProof Order,Guid? ShippingQuoteId);
