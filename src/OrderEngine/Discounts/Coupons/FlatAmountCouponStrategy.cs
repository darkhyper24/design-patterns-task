namespace OrderEngine.Discounts.Coupons;

public class FlatAmountCouponStrategy : ICouponStrategy
{
    private readonly decimal _amount;
    private readonly decimal _minSubtotal;

    public FlatAmountCouponStrategy(decimal amount, decimal minSubtotal = 0)
    {
        _amount = amount;
        _minSubtotal = minSubtotal;
    }

    public decimal Discount(decimal subtotal) => subtotal >= _minSubtotal ? _amount : 0;

    public bool FreeStandardShipping => false;
}
