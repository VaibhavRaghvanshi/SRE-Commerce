using SRECommerce.BuildingBlocks.Kernel;
using SRECommerce.Theme.Domain;
using Xunit;

namespace SRECommerce.Theme.Tests;
public sealed class ThemeRuleTests
{
    [Theory][InlineData("spices")][InlineData("clothing")][InlineData("grocery")][InlineData("general-store")][InlineData("custom")][InlineData("neutral")]
    public void Bundled_business_defaults_have_valid_accessible_palettes(string type)=>ThemeRules.Validate(ThemeRules.Default(type));
    [Fact]public void Arbitrary_css_and_unknown_tokens_are_rejected()
    {var value=ThemeRules.Default("neutral");value.Light.Colors["primary"]="url(javascript:alert(1))";Assert.Throws<DomainFault>(()=>ThemeRules.Validate(value));}
    [Fact]public void Insufficient_text_contrast_is_rejected()
    {var value=ThemeRules.Default("neutral");value.Light.Colors["text"]="#ffffff";Assert.Throws<DomainFault>(()=>ThemeRules.Validate(value));}
    [Fact]public void Unsafe_font_and_unbounded_dimensions_are_rejected()
    {var value=ThemeRules.Default("neutral") with{Mobile=new("url(evil)",99,99,"unknown")};Assert.Throws<DomainFault>(()=>ThemeRules.Validate(value));}
    [Fact]public void Generated_variables_are_from_the_allowed_schema()
    {var values=ThemeRules.Variables(ThemeRules.Default("spices"),"light",true);Assert.Equal("16px",values["--font-size"]);Assert.Contains("--color-header-background",values.Keys);Assert.All(values.Values,value=>Assert.DoesNotContain("url(",value));}
}
