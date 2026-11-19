using OrderEngine.Contract;
using OrderEngine.Tests.Support;
using static OrderEngine.Tests.Support.A;

namespace OrderEngine.Tests;

public class PricingTests
{
    private readonly TestWorld _world = new();

    private OrderReceipt Place(PlaceOrderRequest request) => _world.Service.PlaceOrder(request);

    private static void AssertReceipt(OrderReceipt r, decimal subtotal, decimal discount, decimal shipping, decimal tax, decimal fee, decimal total)
    {
        Assert.Equal(subtotal, r.Subtotal);
        Assert.Equal(discount, r.Discount);
        Assert.Equal(shipping, r.ShippingCost);
        Assert.Equal(tax, r.Tax);
        Assert.Equal(fee, r.PaymentFee);
        Assert.Equal(total, r.Total);
    }

    [Fact]
    public void Regular_customer_in_Egypt_pays_standard_shipping_vat_and_card_fee()
    {
        var r = Place(Order(lines: Line("MS-01")));

        // 450 + 50 shipping, 14% VAT on 500 = 70, card fee 2.9% of 570 + 3 = 19.53
        AssertReceipt(r, subtotal: 450m, discount: 0m, shipping: 50m, tax: 70m, fee: 19.53m, total: 589.53m);
    }

    [Fact]
    public void Gold_customer_gets_5_percent_and_free_standard_shipping_above_2000()
    {
        var r = Place(Order(Customer(CustomerTier.Gold), lines: Line("KB-01", 2)));

        AssertReceipt(r, subtotal: 3000m, discount: 150m, shipping: 0m, tax: 399m, fee: 97.22m, total: 3346.22m);
    }

    [Fact]
    public void Platinum_tier_and_bulk_discount_stack_and_express_is_never_free()
    {
        var r = Place(Order(Customer(CustomerTier.Platinum), payment: "wallet", token: "01012345678",
            shipping: ShippingMethod.Express, lines: Line("CB-USB", 12)));

        // tier 10% of 1440 = 144, bulk 5% of the 12-unit line = 72
        AssertReceipt(r, subtotal: 1440m, discount: 216m, shipping: 120m, tax: 188.16m, fee: 22.98m, total: 1555.14m);
    }

    [Fact]
    public void Bulk_discount_applies_only_to_lines_with_10_or_more_units()
    {
        var r = Place(Order(lines: [Line("CB-USB", 10), Line("MS-01", 9)]));

        // bulk: 5% of 1200 = 60; the 9-unit mouse line gets nothing
        Assert.Equal(1200m + 4050m, r.Subtotal);
        Assert.Equal(60m, r.Discount);
    }

    [Fact]
    public void Welcome10_gives_10_percent_to_regular_customers()
    {
        var r = Place(Order(payment: "bank", token: "EG-1234", coupon: "WELCOME10", lines: Line("MN-27")));

        AssertReceipt(r, subtotal: 7000m, discount: 700m, shipping: 0m, tax: 882m, fee: 15m, total: 7197m);
    }

    [Fact]
    public void Flat100_needs_a_subtotal_of_at_least_1000()
    {
        var small = Place(Order(coupon: "FLAT100", lines: Line("MS-01")));
        var big = Place(Order(payment: "cod", coupon: "FLAT100", lines: Line("KB-01")));

        Assert.Equal(0m, small.Discount);
        AssertReceipt(big, subtotal: 1500m, discount: 100m, shipping: 50m, tax: 203m, fee: 25m, total: 1678m);
    }

    [Fact]
    public void Freeship_removes_standard_shipping()
    {
        var r = Place(Order(payment: "cod", coupon: "FREESHIP", lines: Line("MS-01")));

        AssertReceipt(r, subtotal: 450m, discount: 0m, shipping: 0m, tax: 63m, fee: 25m, total: 538m);
    }

    [Fact]
    public void Freeship_does_not_apply_to_express()
    {
        var r = Place(Order(coupon: "FREESHIP", shipping: ShippingMethod.Express, lines: Line("MS-01")));

        AssertReceipt(r, subtotal: 450m, discount: 0m, shipping: 120m, tax: 79.8m, fee: 21.84m, total: 671.64m);
    }

    [Fact]
    public void Coupon_codes_are_case_insensitive_and_trimmed()
    {
        var r = Place(Order(coupon: "  freeship ", lines: Line("MS-01")));

        Assert.Equal(0m, r.ShippingCost);
    }

    [Fact]
    public void Standard_shipping_in_Egypt_charges_5_per_kg_above_5kg_rounded_up()
    {
        var r = Place(Order(lines: Line("PL-01")));

        // 8 kg -> 50 + 3 * 5 = 65
        AssertReceipt(r, subtotal: 300m, discount: 0m, shipping: 65m, tax: 51.1m, fee: 15.07m, total: 431.17m);
    }

    [Fact]
    public void Express_shipping_in_Egypt_charges_10_per_kg_above_5kg()
    {
        var r = Place(Order(payment: "bank", token: "EG-1234", shipping: ShippingMethod.Express, lines: Line("DK-01")));

        // 28 kg -> 120 + 23 * 10 = 350
        AssertReceipt(r, subtotal: 9500m, discount: 0m, shipping: 350m, tax: 1379m, fee: 15m, total: 11244m);
    }

    [Fact]
    public void Pickup_is_free()
    {
        var r = Place(Order(shipping: ShippingMethod.Pickup, lines: Line("PL-01")));

        Assert.Equal(0m, r.ShippingCost);
    }

    [Fact]
    public void Saudi_orders_pay_international_shipping_and_15_percent_vat()
    {
        var r = Place(Order(Customer(country: "SA"), lines: Line("KB-01")));

        // 1.2 kg -> 2 kg billable, 300 + 2 * 25 = 350
        AssertReceipt(r, subtotal: 1500m, discount: 0m, shipping: 350m, tax: 277.5m, fee: 64.70m, total: 2192.20m);
    }

    [Fact]
    public void International_shipping_is_never_free_even_above_2000()
    {
        var r = Place(Order(Customer(country: "SA"), lines: Line("MN-27")));

        // 6.5 kg -> 7 kg, 300 + 7 * 25 = 475
        Assert.Equal(475m, r.ShippingCost);
    }

    [Fact]
    public void Uae_express_costs_one_and_a_half_times_standard_with_5_percent_vat()
    {
        var r = Place(Order(Customer(country: "AE"), payment: "bank", token: "AE-77",
            shipping: ShippingMethod.Express, lines: Line("MS-01")));

        AssertReceipt(r, subtotal: 450m, discount: 0m, shipping: 487.5m, tax: 46.88m, fee: 15m, total: 999.38m);
    }
}
