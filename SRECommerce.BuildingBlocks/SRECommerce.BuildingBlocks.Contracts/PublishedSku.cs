namespace SRECommerce.BuildingBlocks.Contracts;
public sealed record PublishedSku(Guid ProductId,Guid VariantId,string Sku,string? Barcode,int Kind);
