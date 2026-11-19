using OrderEngine.Contract;

namespace OrderEngine.Discounts.Coupons;

public class CouponFactory
{
    private readonly Dictionary<string, ICouponStrategy> _coupons = new()
    {
        ["WELCOME10"] = new PercentageCouponStrategy("WELCOME10", 0.10m, newCustomersOnly: true),
        ["FLAT100"] = new FlatAmountCouponStrategy(100m, minSubtotal: 1000m),
        ["FREESHIP"] = new FreeShippingCouponStrategy(),
    };

 
    public ICouponStrategy? Create(string? rawCode)
    {
        var code = rawCode?.Trim().ToUpper();
        if (string.IsNullOrEmpty(code)) return null;

        if (!_coupons.TryGetValue(code, out var coupon))
            throw new OrderValidationException("Unknown coupon " + rawCode);

        return coupon;
    }
}
