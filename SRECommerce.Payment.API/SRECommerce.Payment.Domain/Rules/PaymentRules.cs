using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Payment.Domain;
public static class PaymentRules
{
    public static void Id(Guid value) { if (value == Guid.Empty) throw new DomainFault("InvalidPaymentId"); }
    public static void Amount(decimal value) { if (value <= 0 || value > 1000000000000m || decimal.Round(value, 4) != value) throw new DomainFault("InvalidPaymentAmount"); }
    public static void Currency(string value) { if (value.Length != 3 || value.Any(c => c is < 'A' or > 'Z')) throw new DomainFault("InvalidPaymentCurrency"); }
    public static string Reason(string value) { if (string.IsNullOrWhiteSpace(value) || value.Length > 300 || value.Any(char.IsControl)) throw new DomainFault("InvalidPaymentReason"); return value.Trim(); }
    public static void Enabled(PaymentProviderConfiguration config, string method)
    {
        var enabled = method switch { "Card" => config.AllowCard, "UPI" => config.AllowUPI, "NetBanking" => config.AllowNetBanking, "COD" => config.AllowCOD, _ => false };
        if (!config.Active || !enabled) throw new DomainFault("PaymentMethodUnavailable", 403);
    }
    public static void Refund(PaymentTransaction payment, decimal amount)
    {
        Amount(amount);
        if (decimal.Round(amount, payment.DecimalPlaces) != amount) throw new DomainFault("RefundCurrencyPrecisionExceeded");
        if (payment.Status is not (PaymentStatus.Captured or PaymentStatus.PartiallyRefunded) || payment.Refunded + amount > payment.Amount) throw new DomainFault("RefundExceedsCapturedBalance", 409);
    }
}
