using OrderEngine.Contract;

namespace OrderEngine.Discounts.Coupons;

public class PercentageCouponStrategy : ICouponStrategy
{
    private readonly string _code;
    private readonly decimal _rate;
    private readonly bool _newCustomersOnly;

    public PercentageCouponStrategy(string code, decimal rate, bool newCustomersOnly = false)
    {
        _code = code;
        _rate = rate;
        _newCustomersOnly = newCustomersOnly;
    }

    public void CheckEligibility(Customer customer)
    {
        if (_newCustomersOnly && customer.Tier != CustomerTier.Regular)
            throw new OrderValidationException(_code + " is for new customers only");
    }

    public decimal Discount(decimal subtotal) =>
        Math.Round(subtotal * _rate, 2, MidpointRounding.AwayFromZero);

    public bool FreeStandardShipping => false;
}
