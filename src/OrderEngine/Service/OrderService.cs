using System.Globalization;
using OrderEngine.Contract;
using OrderEngine.Discounts;
using OrderEngine.Discounts.Coupons;
using OrderEngine.Payment;
using OrderEngine.Shipping;
using OrderEngine.Validation;

namespace OrderEngine.Service;

// TODO: split this up one day (written 2019, last touched by "temp" fix in 2024)
public class OrderService : IOrderService
{
    private readonly ExternalSystems _ext;
    private readonly IShippingCostCalculator _shippingCostCalculator;
    private readonly IOrderServiceValidation _validation;
    private readonly PaymentMethodFactory _payments;
    private readonly CouponFactory _coupons;
    private readonly DiscountCalculator _discounts;
    private readonly Dictionary<string, OrderRecord> _orders = new Dictionary<string, OrderRecord>();

    public OrderService(ExternalSystems ext, IOrderServiceValidation validation,
        IShippingCostCalculator shippingCostCalculator, CouponFactory coupons, DiscountCalculator discounts,
        PaymentMethodFactory payments)
    {
        _ext = ext;
        _validation = validation;
        _shippingCostCalculator = shippingCostCalculator;
        _coupons = coupons;
        _discounts = discounts;
        _payments = payments;
    }

    public OrderReceipt PlaceOrder(PlaceOrderRequest request)
    {
        _validation.Validate(request);
        var products = new List<Product>();
        foreach (var line in request.Lines)
            products.Add(_ext.Catalog.Find(line.Sku)!);
        
        
        var pm = request.PaymentMethod.ToLower();
        var country = request.Customer.Country;
        var coupon = _coupons.Create(request.CouponCode);


        // ---------------- subtotal ----------------
        decimal subtotal = 0;
        decimal totalWeight = 0;
        for (int i = 0; i < request.Lines.Count; i++)
        {
            subtotal += products[i].UnitPrice * request.Lines[i].Quantity;
            totalWeight += products[i].WeightKg * request.Lines[i].Quantity;
        }

        // ---------------- discounts ----------------
        var discount = _discounts.Calculate(request.Customer, request.Lines, products, subtotal, coupon);
        var netGoods = subtotal - discount;

        // ---------------- shipping ----------------
        var billableKg = (int) Math.Ceiling(totalWeight);
        var shippingMethod = request.Shipping;
        var shipping = _shippingCostCalculator.CalculateShippingCost(country, shippingMethod, billableKg, coupon, netGoods);

        // ---------------- tax ----------------
        decimal taxRate;
        switch (country)
        {
            case "EG": taxRate = GlobalSettings.Instance.EgyptVat; break;
            case "SA": taxRate = GlobalSettings.Instance.SaudiVat; break;
            case "AE": taxRate = GlobalSettings.Instance.UaeVat; break;
            default: throw new OrderValidationException("We do not ship to " + country);
        }
        var tax = Math.Round((netGoods + shipping) * taxRate, 2, MidpointRounding.AwayFromZero);

        var beforeFee = netGoods + shipping + tax;

        // ---------------- payment fee ----------------
        var payment = _payments.Create(pm);
        var fee = payment.CalculateFee(beforeFee);
        var total = beforeFee + fee;

        if (pm == "cod" && total > GlobalSettings.Instance.CodMaxOrderTotal)
            throw new OrderValidationException("Cash on delivery is not available for orders above " + GlobalSettings.Instance.CodMaxOrderTotal);

        // ---------------- payment ----------------
        var orderId = OrderIdGenerator.Next();
        var paymentRef = payment.Pay(request.PaymentToken, total, orderId);
        var status = pm == "cod" ? "PENDING" : "PAID";

        // ---------------- save ----------------
        var record = new OrderRecord();
        record.Id = orderId;
        record.Customer = request.Customer;
        record.PaymentMethod = pm;
        record.PaymentReference = paymentRef;
        record.Total = total;
        record.Status = status;
        record.PlacedAt = _ext.Clock.UtcNow;
        _orders[orderId] = record;

        _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "OrderPlaced", orderId,
            "total=" + total.ToString("0.00", CultureInfo.InvariantCulture) + ";method=" + pm));

        // ---------------- notifications ----------------
        SendOrderPlacedNotifications(record);

        return new OrderReceipt(orderId, subtotal, discount, shipping, tax, fee, total, ToStatus(status), paymentRef);
    }

    public OrderStatus GetStatus(string orderId)
    {
        return ToStatus(Find(orderId).Status);
    }

    public void MarkShipped(string orderId, string trackingNumber)
    {
        var order = Find(orderId);
        if (string.IsNullOrWhiteSpace(trackingNumber))
            throw new OrderValidationException("Tracking number is required");

        if (order.Status == "PAID" || (order.Status == "PENDING" && order.PaymentMethod == "cod"))
        {
            order.Status = "SHIPPED";
            order.TrackingNumber = trackingNumber;
            _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "OrderShipped", orderId, trackingNumber));
            SendShippedNotifications(order);
        }
        else
        {
            throw new InvalidOrderTransitionException("Cannot ship an order that is " + ToStatus(order.Status));
        }
    }

    public void MarkDelivered(string orderId)
    {
        var order = Find(orderId);
        if (order.Status == "SHIPPED")
        {
            if (order.PaymentMethod == "cod")
            {
                _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "CodCollected", orderId,
                    order.Total.ToString("0.00", CultureInfo.InvariantCulture)));
            }
            order.Status = "DELIVERED";
            _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "OrderDelivered", orderId, ""));
            SendDeliveredNotifications(order);
        }
        else
        {
            throw new InvalidOrderTransitionException("Cannot deliver an order that is " + ToStatus(order.Status));
        }
    }

    public void Cancel(string orderId, string reason)
    {
        var order = Find(orderId);
        if (string.IsNullOrWhiteSpace(reason))
            throw new OrderValidationException("Cancellation reason is required");

        if (order.Status == "PENDING")
        {
            order.Status = "CANCELLED";
            _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "OrderCancelled", orderId, reason));
            SendCancelledNotifications(order, reason, false);
        }
        else if (order.Status == "PAID")
        {
            // refund through whoever took the money
            _payments.Create(order.PaymentMethod).Refund(order.PaymentReference, order.Total, orderId);

            order.Status = "REFUNDED";
            _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "OrderCancelled", orderId, reason));
            _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "OrderRefunded", orderId,
                order.Total.ToString("0.00", CultureInfo.InvariantCulture)));
            SendCancelledNotifications(order, reason, true);
        }
        else
        {
            throw new InvalidOrderTransitionException("Cannot cancel an order that is " + ToStatus(order.Status));
        }
    }

    // ======================================================================
    // notifications (copy-pasted per event, sorry)
    // ======================================================================

    private void SendOrderPlacedNotifications(OrderRecord order)
    {
        var shop = GlobalSettings.Instance.ShopName;
        var total = order.Total.ToString("F2", CultureInfo.InvariantCulture);

        foreach (var channel in order.Customer.Channels)
        {
            try
            {
                if (channel == NotificationChannel.Email)
                {
                    _ext.Mailer.Send(order.Customer.Email,
                        "Order " + order.Id + " confirmed",
                        "Hi " + order.Customer.Name + ",\n\nThanks for your order " + order.Id + ".\nTotal: " + total + " EGP\n\n" + shop);
                }
                else if (channel == NotificationChannel.Sms)
                {
                    _ext.Sms.SendText(order.Customer.Phone, shop + ": order " + order.Id + " confirmed. Total " + total + " EGP");
                }
                else if (channel == NotificationChannel.Webhook)
                {
                    if (order.Customer.WebhookUrl != null)
                    {
                        var json = "{\"event\":\"order.placed\",\"orderId\":\"" + order.Id + "\",\"total\":" + total
                                   + ",\"status\":\"" + ToStatus(order.Status) + "\"}";
                        _ext.Webhooks.Post(order.Customer.WebhookUrl, json);
                    }
                }
            }
            catch (Exception ex)
            {
                _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "NotificationFailed", order.Id, channel + ": " + ex.Message));
            }
        }
    }

    private void SendShippedNotifications(OrderRecord order)
    {
        var shop = GlobalSettings.Instance.ShopName;

        foreach (var channel in order.Customer.Channels)
        {
            try
            {
                if (channel == NotificationChannel.Email)
                {
                    _ext.Mailer.Send(order.Customer.Email,
                        "Order " + order.Id + " shipped",
                        "Hi " + order.Customer.Name + ",\n\nYour order " + order.Id + " is on its way.\nTracking number: " + order.TrackingNumber + "\n\n" + shop);
                }
                else if (channel == NotificationChannel.Sms)
                {
                    _ext.Sms.SendText(order.Customer.Phone, shop + ": order " + order.Id + " shipped. Tracking " + order.TrackingNumber);
                }
                else if (channel == NotificationChannel.Webhook)
                {
                    if (order.Customer.WebhookUrl != null)
                    {
                        var json = "{\"event\":\"order.shipped\",\"orderId\":\"" + order.Id + "\",\"trackingNumber\":\""
                                   + order.TrackingNumber + "\"}";
                        _ext.Webhooks.Post(order.Customer.WebhookUrl, json);
                    }
                }
            }
            catch (Exception ex)
            {
                _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "NotificationFailed", order.Id, channel + ": " + ex.Message));
            }
        }
    }

    private void SendDeliveredNotifications(OrderRecord order)
    {
        var shop = GlobalSettings.Instance.ShopName;

        foreach (var channel in order.Customer.Channels)
        {
            try
            {
                if (channel == NotificationChannel.Email)
                {
                    _ext.Mailer.Send(order.Customer.Email,
                        "Order " + order.Id + " delivered",
                        "Hi " + order.Customer.Name + ",\n\nYour order " + order.Id + " has been delivered. Enjoy!\n\n" + shop);
                }
                else if (channel == NotificationChannel.Sms)
                {
                    _ext.Sms.SendText(order.Customer.Phone, shop + ": order " + order.Id + " delivered");
                }
                else if (channel == NotificationChannel.Webhook)
                {
                    if (order.Customer.WebhookUrl != null)
                    {
                        var json = "{\"event\":\"order.delivered\",\"orderId\":\"" + order.Id + "\"}";
                        _ext.Webhooks.Post(order.Customer.WebhookUrl, json);
                    }
                }
            }
            catch (Exception ex)
            {
                _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "NotificationFailed", order.Id, channel + ": " + ex.Message));
            }
        }
    }

    private void SendCancelledNotifications(OrderRecord order, string reason, bool refunded)
    {
        var shop = GlobalSettings.Instance.ShopName;
        var total = order.Total.ToString("F2", CultureInfo.InvariantCulture);

        foreach (var channel in order.Customer.Channels)
        {
            try
            {
                if (channel == NotificationChannel.Email)
                {
                    var subject = "Order " + order.Id + " cancelled";
                    if (refunded) subject += " and refunded";
                    var body = "Hi " + order.Customer.Name + ",\n\nYour order " + order.Id + " has been cancelled.\nReason: " + reason + "\n";
                    if (refunded) body += "A refund of " + total + " EGP has been issued.\n";
                    body += "\n" + shop;
                    _ext.Mailer.Send(order.Customer.Email, subject, body);
                }
                else if (channel == NotificationChannel.Sms)
                {
                    var text = shop + ": order " + order.Id + " cancelled";
                    if (refunded) text += ". Refund " + total + " EGP";
                    _ext.Sms.SendText(order.Customer.Phone, text);
                }
                else if (channel == NotificationChannel.Webhook)
                {
                    if (order.Customer.WebhookUrl != null)
                    {
                        var json = "{\"event\":\"order.cancelled\",\"orderId\":\"" + order.Id + "\",\"reason\":\""
                                   + reason.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"refunded\":"
                                   + (refunded ? "true" : "false") + "}";
                        _ext.Webhooks.Post(order.Customer.WebhookUrl, json);
                    }
                }
            }
            catch (Exception ex)
            {
                _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "NotificationFailed", order.Id, channel + ": " + ex.Message));
            }
        }
    }

    // ======================================================================
    // helpers
    // ======================================================================

    private OrderRecord Find(string orderId)
    {
        if (orderId == null || !_orders.ContainsKey(orderId))
            throw new OrderNotFoundException(orderId ?? "(null)");
        return _orders[orderId];
    }

    private static OrderStatus ToStatus(string status)
    {
        switch (status)
        {
            case "PENDING": return OrderStatus.Pending;
            case "PAID": return OrderStatus.Paid;
            case "SHIPPED": return OrderStatus.Shipped;
            case "DELIVERED": return OrderStatus.Delivered;
            case "CANCELLED": return OrderStatus.Cancelled;
            case "REFUNDED": return OrderStatus.Refunded;
            default: throw new InvalidOperationException("Unknown status " + status);
        }
    }

    private class OrderRecord
    {
        public string Id = "";
        public Customer Customer = null!;
        public string PaymentMethod = "";
        public string PaymentReference = "";
        public decimal Total;
        public string Status = "";
        public string? TrackingNumber;
        public DateTimeOffset PlacedAt;
    }
}
