using OrderEngine.Contract;
using OrderEngine.Discounts;
using OrderEngine.Discounts.Coupons;
using OrderEngine.Service;
using OrderEngine.Shipping;
using OrderEngine.Validation;

namespace OrderEngine.Composition;

/// <summary>
/// The composition root: the one place where the object graph is built.
/// Keep this signature. Change the body as much as you like.
/// </summary>
///

// this will be the final file to be updated.
public static class Bootstrap
{
    public static IOrderService Build(ExternalSystems externals)
    {
        var coupons = new CouponFactory();
        var discounts = new DiscountCalculator(GlobalSettings.Instance.MaxDiscountPercent);

        return new OrderService(externals, new OrederServiceValidation(externals, coupons),
            new ShippingCostCalculator(), coupons, discounts);
    }
}
