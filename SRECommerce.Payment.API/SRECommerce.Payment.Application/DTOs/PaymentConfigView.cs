using SRECommerce.Payment.Domain;
namespace SRECommerce.Payment.Application;
public sealed record PaymentConfigView(string Provider, bool Active, bool AllowCard, bool AllowUPI, bool AllowNetBanking, bool AllowCOD, SandboxOutcome Outcome, string Version, bool Sandbox = true);
