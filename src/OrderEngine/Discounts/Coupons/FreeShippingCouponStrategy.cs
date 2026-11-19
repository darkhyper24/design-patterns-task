namespace OrderEngine.Discounts.Coupons;


public class FreeShippingCouponStrategy : ICouponStrategy
{
    public decimal Discount(decimal subtotal) => 0;

    public bool FreeStandardShipping => true;
}
