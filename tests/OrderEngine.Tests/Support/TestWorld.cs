using OrderEngine.Composition;
using OrderEngine.Contract;
using OrderEngine.Vendor.CardPay;
using OrderEngine.Vendor.InstaPay;
using OrderEngine.Vendor.LegacyBank;
using OrderEngine.Vendor.Messaging;
using OrderEngine.Vendor.Wallet;

namespace OrderEngine.Tests.Support;

/// <summary>A fresh engine wired to fresh sandbox SDKs. Create one per test.</summary>
public sealed class TestWorld
{
    public FixedClock Clock { get; } = new(new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero));
    public InMemoryAuditSink Audit { get; } = new();
    public InMemoryCatalog Catalog { get; } = InMemoryCatalog.WithDefaultProducts();

    public CardPayGateway CardPay { get; } = new();
    public WalletClient Wallet { get; } = new();
    public LegacyBankApi LegacyBank { get; } = new();
    public InstaPayClient InstaPay { get; } = new();

    public SmtpMailer Mailer { get; } = new();
    public SmsGateway Sms { get; } = new();
    public WebhookClient Webhooks { get; } = new();

    public IOrderService Service { get; }

    public TestWorld()
    {
        Service = Bootstrap.Build(new ExternalSystems
        {
            Catalog = Catalog,
            Clock = Clock,
            Audit = Audit,
            CardPay = CardPay,
            Wallet = Wallet,
            LegacyBank = LegacyBank,
            InstaPay = InstaPay,
            Mailer = Mailer,
            Sms = Sms,
            Webhooks = Webhooks,
        });
    }

    public IEnumerable<string> AuditActionsFor(string orderId) =>
        Audit.Entries.Where(e => e.OrderId == orderId).Select(e => e.Action);
}

public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

public sealed class InMemoryAuditSink : IAuditSink
{
    private readonly List<AuditEntry> _entries = [];
    public IReadOnlyList<AuditEntry> Entries => _entries;
    public void Record(AuditEntry entry) => _entries.Add(entry);
}

public sealed class InMemoryCatalog : IProductCatalog
{
    private readonly Dictionary<string, Product> _products = new();

    public void Add(Product product) => _products[product.Sku] = product;

    public Product? Find(string sku) => _products.GetValueOrDefault(sku);

    public static InMemoryCatalog WithDefaultProducts()
    {
        var catalog = new InMemoryCatalog();
        catalog.Add(new Product("KB-01", "Mechanical Keyboard", "Accessories", 1500m, 1.2m));
        catalog.Add(new Product("MS-01", "Wireless Mouse", "Accessories", 450m, 0.2m));
        catalog.Add(new Product("CB-USB", "USB-C Cable", "Accessories", 120m, 0.05m));
        catalog.Add(new Product("MN-27", "27\" Monitor", "Displays", 7000m, 6.5m));
        catalog.Add(new Product("HD-01", "Headphones", "Audio", 2200m, 0.4m));
        catalog.Add(new Product("DK-01", "Standing Desk", "Furniture", 9500m, 28m));
        catalog.Add(new Product("PL-01", "Ceramic Plant Pot", "Home", 300m, 8m));
        return catalog;
    }
}
