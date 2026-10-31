using SRECommerce.Cart.Domain;using SRECommerce.BuildingBlocks.Kernel;using Xunit;
namespace SRECommerce.Cart.Tests;
public sealed class CartRuleTests
{
 [Theory][InlineData(0)][InlineData(-1)][InlineData(1001)][InlineData(0.0001)]public void Quantities_reject_zero_negative_excessive_and_unsupported_precision(decimal value)=>Assert.Throws<DomainFault>(()=>CartRules.Quantity(value));
 [Fact]public void Guest_capabilities_require_canonical_256_bit_random_values(){var key=CartFixture.Key();Assert.Equal(32,CartRules.Capability(key).Length);CartRules.Match(CartRules.Capability(key),key);Assert.Throws<DomainFault>(()=>CartRules.Match(CartRules.Capability(key),CartFixture.Key()));Assert.Throws<DomainFault>(()=>CartRules.Capability(new string('a',42)));}
}
