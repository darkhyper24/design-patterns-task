namespace OrderEngine;

// Added in 2021 "so we can change rates without redeploying". Nobody ever changed them at runtime.
public class GlobalSettings
{
    private static GlobalSettings? _instance;

    public static GlobalSettings Instance
    {
        get
        {
            if (_instance == null)
                _instance = new GlobalSettings();
            return _instance;
        }
    }

    private GlobalSettings() { }

    public string Currency { get; set; } = "EGP";
    public string ShopName { get; set; } = "FutureShop";

    public decimal EgyptVat { get; set; } = 0.14m;
    public decimal SaudiVat { get; set; } = 0.15m;
    public decimal UaeVat { get; set; } = 0.05m;

    public decimal CardFeePercent { get; set; } = 0.029m;
    public decimal CardFeeFixed { get; set; } = 3m;
    public decimal WalletFeePercent { get; set; } = 0.015m;
    public decimal BankFeeFixed { get; set; } = 15m;
    public decimal CodFeeFixed { get; set; } = 25m;

    public decimal MaxDiscountPercent { get; set; } = 0.30m;
    public decimal CodMaxOrderTotal { get; set; } = 10000m;
}

public static class OrderIdGenerator
{
    private static int _counter;

    public static string Next() => $"ORD-{Interlocked.Increment(ref _counter):D5}";
}
