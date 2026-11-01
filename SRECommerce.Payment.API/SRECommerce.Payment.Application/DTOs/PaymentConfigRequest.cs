using SRECommerce.Payment.Domain;
namespace SRECommerce.Payment.Application;
public sealed record PaymentConfigRequest(bool Active, bool AllowCard, bool AllowUPI, bool AllowNetBanking, bool AllowCOD, SandboxOutcome Outcome, string? Version = null);
