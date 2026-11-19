using OrderEngine.Contract;

namespace OrderEngine.Tests.Support;

public static class A
{
    public static Customer Customer(
        CustomerTier tier = CustomerTier.Regular,
        string country = "EG",
        NotificationChannel[]? channels = null,
        string email = "mona@example.com",
        string phone = "01012345678",
        string? webhookUrl = null) =>
        new("C-1", "Mona", email, phone, country, tier, channels ?? [NotificationChannel.Email], webhookUrl);

    public static PlaceOrderRequest Order(
        Customer? customer = null,
        string payment = "card",
        string token = "tok_visa",
        ShippingMethod shipping = ShippingMethod.Standard,
        string? coupon = null,
        params CartLine[] lines) =>
        new(customer ?? Customer(), lines.Length == 0 ? [new CartLine("MS-01", 1)] : lines, payment, token, shipping, coupon);

    public static CartLine Line(string sku, int quantity = 1) => new(sku, quantity);
}
