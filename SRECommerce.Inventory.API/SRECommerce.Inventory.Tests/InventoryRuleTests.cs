using SRECommerce.BuildingBlocks.Kernel;
using SRECommerce.Inventory.Domain;
using Xunit;
namespace SRECommerce.Inventory.Tests;
public sealed class InventoryRuleTests
{
    [Theory][InlineData("0")][InlineData("-1")][InlineData("0.0001")][InlineData("1000000001")]
    public void Invalid_positive_quantities_are_rejected(string value)=>Assert.Throws<DomainFault>(()=>InventoryRules.Quantity(decimal.Parse(value,System.Globalization.CultureInfo.InvariantCulture)));
    [Fact]public void Reserved_or_damaged_units_cannot_be_fabricated()
    {Assert.Throws<DomainFault>(()=>InventoryRules.Balance(5,6,0));Assert.Throws<DomainFault>(()=>InventoryRules.Balance(5,0,-1));InventoryRules.Balance(5,5,2);}
    [Theory][InlineData("../LOT")][InlineData("LOT A")][InlineData("<script>")]
    public void Unsafe_lot_codes_are_rejected(string value)=>Assert.Throws<DomainFault>(()=>InventoryRules.Code(value));
    [Fact]public void Holds_are_bounded_and_codes_are_canonical()
    {var now=DateTimeOffset.UtcNow;Assert.Throws<DomainFault>(()=>InventoryRules.Reservation(now,now.AddHours(2)));Assert.Throws<DomainFault>(()=>InventoryRules.Reservation(now,now));Assert.Equal("LOT-1",InventoryRules.Code(" lot-1 "));}
}
