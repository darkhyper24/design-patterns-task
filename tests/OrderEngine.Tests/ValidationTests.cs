using OrderEngine.Contract;
using OrderEngine.Tests.Support;
using static OrderEngine.Tests.Support.A;

namespace OrderEngine.Tests;

public class ValidationTests
{
    private readonly TestWorld _world = new();

    private void AssertRejected(PlaceOrderRequest request)
    {
        Assert.Throws<OrderValidationException>(() => _world.Service.PlaceOrder(request));
        Assert.Empty(_world.CardPay.Charges);
        Assert.Empty(_world.Mailer.Sent);
    }

    [Fact]
    public void Empty_cart_is_rejected() =>
        AssertRejected(Order() with { Lines = [] });

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Quantity_must_be_between_1_and_100(int quantity) =>
        AssertRejected(Order(lines: Line("MS-01", quantity)));

    [Fact]
    public void Unknown_product_is_rejected() =>
        AssertRejected(Order(lines: Line("NOPE-1")));

    [Fact]
    public void Unsupported_country_is_rejected() =>
        AssertRejected(Order(Customer(country: "US")));

    [Fact]
    public void Unknown_payment_method_is_rejected() =>
        AssertRejected(Order(payment: "paypal"));

    [Fact]
    public void Payment_method_is_case_insensitive()
    {
        var r = _world.Service.PlaceOrder(Order(payment: "CARD"));
        Assert.Equal(OrderStatus.Paid, r.Status);
    }

    [Fact]
    public void Wallet_is_only_available_in_Egypt() =>
        AssertRejected(Order(Customer(country: "SA"), payment: "wallet", token: "01012345678"));

    [Fact]
    public void Cash_on_delivery_is_only_available_in_Egypt() =>
        AssertRejected(Order(Customer(country: "AE"), payment: "cod"));

    [Fact]
    public void Cash_on_delivery_is_limited_to_10000() =>
        AssertRejected(Order(payment: "cod", lines: Line("MN-27", 2)));

    [Fact]
    public void Pickup_is_only_available_in_Egypt() =>
        AssertRejected(Order(Customer(country: "SA"), shipping: ShippingMethod.Pickup));

    [Fact]
    public void Unknown_coupon_is_rejected() =>
        AssertRejected(Order(coupon: "FREE-MONEY"));

    [Fact]
    public void Welcome10_is_for_regular_customers_only() =>
        AssertRejected(Order(Customer(CustomerTier.Gold), coupon: "WELCOME10"));
}
