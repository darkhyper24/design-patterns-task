using OrderEngine.Vendor.CardPay;
using OrderEngine.Vendor.InstaPay;
using OrderEngine.Vendor.LegacyBank;
using OrderEngine.Vendor.Messaging;
using OrderEngine.Vendor.Wallet;

namespace OrderEngine.Contract;

/// <summary>Everything outside the engine's control. Tests pass in sandbox instances and inspect them afterwards.</summary>
public sealed class ExternalSystems
{
    public required IProductCatalog Catalog { get; init; }
    public required IClock Clock { get; init; }
    public required IAuditSink Audit { get; init; }

    public required CardPayGateway CardPay { get; init; }
    public required WalletClient Wallet { get; init; }
    public required LegacyBankApi LegacyBank { get; init; }
    public required InstaPayClient InstaPay { get; init; }

    public required SmtpMailer Mailer { get; init; }
    public required SmsGateway Sms { get; init; }
    public required WebhookClient Webhooks { get; init; }
}
