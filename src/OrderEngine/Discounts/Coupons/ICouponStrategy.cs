using OrderEngine.Contract;

namespace OrderEngine.Discounts.Coupons;

public interface ICouponStrategy
{

    void CheckEligibility(Customer customer) { }

    decimal Discount(decimal subtotal);

    bool FreeStandardShipping { get; }
}
