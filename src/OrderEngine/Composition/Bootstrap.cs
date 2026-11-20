using OrderEngine.Contract;
using OrderEngine.Discounts;
using OrderEngine.Discounts.Coupons;
using OrderEngine.Payment;
using OrderEngine.Payment.Methods;
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
        var settings = GlobalSettings.Instance;
        var coupons = new CouponFactory();
        var discounts = new DiscountCalculator(settings.MaxDiscountPercent);

        var payments = new PaymentMethodFactory(new Dictionary<string, IPaymentMethod>
        {
            ["card"] = new CardPayment(externals.CardPay, externals.Audit, externals.Clock,
                settings.Currency, settings.CardFeePercent, settings.CardFeeFixed),
            ["wallet"] = new WalletPayment(externals.Wallet, externals.Audit, externals.Clock, settings.WalletFeePercent),
            ["bank"] = new BankTransferPayment(externals.LegacyBank, externals.Audit, externals.Clock, settings.BankFeeFixed),
            ["cod"] = new CashOnDeliveryPayment(settings.CodFeeFixed),
        });

        return new OrderService(externals, new OrederServiceValidation(externals, coupons),
            new ShippingCostCalculator(), coupons, discounts, payments);
    }
}
