namespace SRECommerce.Payment.Application;
public interface IPaymentSignatures
{
    string Sign(Guid tenant, string timestamp, string document);
    void Verify(Guid tenant, string timestamp, string document, string signature, DateTimeOffset now);
}
