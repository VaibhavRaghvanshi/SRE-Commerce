using SRECommerce.BuildingBlocks.Kernel;
using SRECommerce.Storefront.Domain;
using Xunit;

namespace SRECommerce.Storefront.Tests;
public sealed class LayoutRuleTests
{
    [Theory][InlineData("neutral")][InlineData("spices")][InlineData("clothing")][InlineData("grocery")][InlineData("general-store")][InlineData("custom")]
    public void Bundled_layouts_are_valid_and_business_fields_differ(string type)
    {var layout=LayoutRules.Default(type);LayoutRules.Validate(layout);Assert.Equal(type,layout.Template);if(type=="spices")Assert.Contains("origin",layout.Product.BusinessFields);if(type=="clothing")Assert.Contains("size",layout.Product.BusinessFields);}
    [Theory][InlineData("javascript:alert(1)")][InlineData("//evil.test")][InlineData("data:text/html,evil")][InlineData("/products/../admin")][InlineData("https://user:pass@example.test")][InlineData("/api/private")]
    public void Unsafe_navigation_urls_are_rejected(string url)=>Assert.Throws<DomainFault>(()=>LayoutRules.Url(url));
    [Fact]
    public void Arbitrary_html_unknown_sections_duplicate_order_and_identifiers_are_rejected()
    {
        var value=LayoutRules.Default("neutral");
        Assert.Throws<DomainFault>(()=>LayoutRules.Validate(value with{Sections=[value.Sections[0] with{Text="<script>alert(1)</script>"}]}));
        Assert.Throws<DomainFault>(()=>LayoutRules.Validate(value with{Sections=[value.Sections[0] with{Kind="script"}]}));
        Assert.Throws<DomainFault>(()=>LayoutRules.Validate(value with{Sections=[value.Sections[0],value.Sections[0]]}));
        Assert.Throws<DomainFault>(()=>LayoutRules.Validate(value with{Sections=[value.Sections[0],value.Sections[1] with{Desktop=new(true,1,4)}]}));
    }
    [Fact]
    public void Different_mobile_order_and_visibility_are_supported()
    {var value=LayoutRules.Default("clothing");value.Sections[0]=value.Sections[0] with{Mobile=new(false,3,1)};value.Sections[1]=value.Sections[1] with{Mobile=new(true,1,2)};LayoutRules.Validate(value);}
    [Theory][InlineData("../api")][InlineData("admin")][InlineData("my page")][InlineData("checkout")]
    public void Reserved_or_unsafe_slugs_are_rejected(string value)=>Assert.Throws<DomainFault>(()=>LayoutRules.Slug(value));
}
