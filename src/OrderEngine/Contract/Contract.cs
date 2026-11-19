namespace OrderEngine.Contract;

public enum CustomerTier { Regular, Gold, Platinum }

public enum NotificationChannel { Email, Sms, Webhook }

public enum ShippingMethod { Standard, Express, Pickup }

public enum OrderStatus { Pending, Paid, Shipped, Delivered, Cancelled, Refunded }

public sealed record Customer(
    string Id,
    string Name,
    string Email,
    string Phone,
    string Country,
    CustomerTier Tier,
    IReadOnlyList<NotificationChannel> Channels,
    string? WebhookUrl = null);

public sealed record CartLine(string Sku, int Quantity);

/// <param name="PaymentMethod">"card", "wallet", "bank" or "cod".</param>
/// <param name="PaymentToken">
/// Card token for "card", mobile number for "wallet", account number for "bank", ignored for "cod".
/// </param>
public sealed record PlaceOrderRequest(
    Customer Customer,
    IReadOnlyList<CartLine> Lines,
    string PaymentMethod,
    string PaymentToken,
    ShippingMethod Shipping,
    string? CouponCode = null);

public sealed record OrderReceipt(
    string OrderId,
    decimal Subtotal,
    decimal Discount,
    decimal ShippingCost,
    decimal Tax,
    decimal PaymentFee,
    decimal Total,
    OrderStatus Status,
    string PaymentReference);

public interface IOrderService
{
    OrderReceipt PlaceOrder(PlaceOrderRequest request);
    OrderStatus GetStatus(string orderId);
    void MarkShipped(string orderId, string trackingNumber);
    void MarkDelivered(string orderId);
    void Cancel(string orderId, string reason);
}

public sealed record Product(string Sku, string Name, string Category, decimal UnitPrice, decimal WeightKg);

public interface IProductCatalog
{
    Product? Find(string sku);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed record AuditEntry(DateTimeOffset At, string Action, string OrderId, string Details);

public interface IAuditSink
{
    void Record(AuditEntry entry);
}

public class OrderValidationException(string message) : Exception(message);

public class PaymentDeclinedException(string reason) : Exception($"Payment declined: {reason}")
{
    public string Reason { get; } = reason;
}

public class OrderNotFoundException(string orderId) : Exception($"Order {orderId} not found")
{
    public string OrderId { get; } = orderId;
}

public class InvalidOrderTransitionException(string message) : InvalidOperationException(message);
