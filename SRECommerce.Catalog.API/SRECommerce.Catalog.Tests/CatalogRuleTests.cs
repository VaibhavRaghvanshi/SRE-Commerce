using System.Text.Json;
using SRECommerce.BuildingBlocks.Kernel;
using SRECommerce.Catalog.Domain;
using Xunit;
namespace SRECommerce.Catalog.Tests;
public sealed class CatalogRuleTests
{
    [Theory][InlineData("abc-1","ABC-1")][InlineData(" SKU_123 ","SKU_123")]
    public void Sku_is_canonical(string value,string expected)=>Assert.Equal(expected,CatalogRules.Sku(value));
    [Theory][InlineData("../sku")][InlineData("SKU A")][InlineData("<script>")]
    public void Unsafe_skus_are_rejected(string value)=>Assert.Throws<DomainFault>(()=>CatalogRules.Sku(value));
    [Theory][InlineData("javascript:alert(1)")][InlineData("//external.test/image")][InlineData("/uploads/../secret")][InlineData("http://external.test/image.jpg")]
    public void Unsafe_media_is_rejected(string value)=>Assert.Throws<DomainFault>(()=>CatalogRules.MediaUrl(value));
    [Fact]
    public void Required_typed_attributes_are_validated_at_publish()
    {
        var definition=new AttributeDefinition{Code="weight",DataType=AttributeDataType.Number};var bindings=new[]{(definition,true)};
        CatalogRules.Attributes([],bindings,false);Assert.Throws<DomainFault>(()=>CatalogRules.Attributes([],bindings,true));
        Assert.Throws<DomainFault>(()=>CatalogRules.Attributes(new(){["weight"]=JsonSerializer.SerializeToElement("heavy")},bindings,true));
        CatalogRules.Attributes(new(){["weight"]=JsonSerializer.SerializeToElement(100.5m)},bindings,true);
    }
    [Fact]
    public void Unknown_and_unsupported_choice_attributes_are_rejected()
    {
        var definition=new AttributeDefinition{Code="size",DataType=AttributeDataType.Choice,Options="[\"S\",\"M\"]"};var bindings=new[]{(definition,false)};
        Assert.Throws<DomainFault>(()=>CatalogRules.Attributes(new(){["unexpected"]=JsonSerializer.SerializeToElement("S")},bindings,false));
        Assert.Throws<DomainFault>(()=>CatalogRules.Attributes(new(){["size"]=JsonSerializer.SerializeToElement("XL")},bindings,false));
    }
}
