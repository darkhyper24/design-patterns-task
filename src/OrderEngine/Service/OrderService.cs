using System.Globalization;
using OrderEngine.Contract;
using OrderEngine.Discounts;
using OrderEngine.Discounts.Coupons;
using OrderEngine.Validation;
using OrderEngine.Vendor.Wallet;

namespace OrderEngine.Service;

// TODO: split this up one day (written 2019, last touched by "temp" fix in 2024)
public class OrderService : IOrderService
{
    private readonly ExternalSystems _ext;
    private readonly IOrderServiceValidation _validation;
    private readonly CouponFactory _coupons;
    private readonly DiscountCalculator _discounts;
    private readonly Dictionary<string, OrderRecord> _orders = new Dictionary<string, OrderRecord>();

    public OrderService(ExternalSystems ext, IOrderServiceValidation validation,
        CouponFactory coupons, DiscountCalculator discounts)
    {
        _ext = ext;
        _validation = validation;
        _coupons = coupons;
        _discounts = discounts;
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
        decimal shipping = 0;
        var billableKg = (int)Math.Ceiling(totalWeight);
        if (request.Shipping == ShippingMethod.Pickup)
        {
            shipping = 0;
        }
        else if (country == "EG")
        {
            if (request.Shipping == ShippingMethod.Standard)
            {
                if (netGoods >= 2000)
                    shipping = 0;
                else
                    shipping = 50 + 5 * Math.Max(0, billableKg - 5);
            }
            else if (request.Shipping == ShippingMethod.Express)
            {
                shipping = 120 + 10 * Math.Max(0, billableKg - 5);
            }
        }
        else
        {
            // international (SA, AE)
            if (request.Shipping == ShippingMethod.Standard)
            {
                shipping = 300 + 25 * billableKg;
            }
            else if (request.Shipping == ShippingMethod.Express)
            {
                shipping = Math.Round((300 + 25 * billableKg) * 1.5m, 2, MidpointRounding.AwayFromZero);
            }
        }

        if (coupon is { FreeStandardShipping: true } && request.Shipping == ShippingMethod.Standard)
            shipping = 0;

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
        decimal fee = 0;
        switch (pm)
        {
            case "card":
                fee = Math.Round(beforeFee * GlobalSettings.Instance.CardFeePercent + GlobalSettings.Instance.CardFeeFixed, 2, MidpointRounding.AwayFromZero);
                break;
            case "wallet":
                fee = Math.Round(beforeFee * GlobalSettings.Instance.WalletFeePercent, 2, MidpointRounding.AwayFromZero);
                break;
            case "bank":
                fee = GlobalSettings.Instance.BankFeeFixed;
                break;
            case "cod":
                fee = GlobalSettings.Instance.CodFeeFixed;
                break;
        }

        var total = beforeFee + fee;

        if (pm == "cod" && total > GlobalSettings.Instance.CodMaxOrderTotal)
            throw new OrderValidationException("Cash on delivery is not available for orders above " + GlobalSettings.Instance.CodMaxOrderTotal);

        // ---------------- payment ----------------
        var orderId = OrderIdGenerator.Next();
        string paymentRef;
        string status;

        if (pm == "card")
        {
            var result = _ext.CardPay.Charge(request.PaymentToken, (long)(total * 100), GlobalSettings.Instance.Currency);
            if (!result.Approved)
            {
                _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "PaymentDeclined", orderId, result.DeclineCode ?? "UNKNOWN"));
                throw new PaymentDeclinedException(result.DeclineCode ?? "UNKNOWN");
            }
            paymentRef = result.ChargeId!;
            status = "PAID";
        }
        else if (pm == "wallet")
        {
            var req = new WalletDebitRequest();
            req.MobileNumber = request.PaymentToken;
            req.Amount = total;
            req.MerchantReference = orderId;
            var resp = _ext.Wallet.Debit(req);
            if (resp.Status != "SUCCESS")
            {
                _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "PaymentDeclined", orderId, resp.Status));
                throw new PaymentDeclinedException(resp.Status);
            }
            paymentRef = resp.TransactionRef!;
            status = "PAID";
        }
        else if (pm == "bank")
        {
            var xml = "<payment>"
                      + "<account>" + request.PaymentToken + "</account>"
                      + "<amount>" + total.ToString("0.00", CultureInfo.InvariantCulture) + "</amount>"
                      + "<currency>818</currency>"
                      + "<ref>" + orderId + "</ref>"
                      + "</payment>";
            string confirmation;
            var code = _ext.LegacyBank.SubmitPayment(xml, out confirmation);
            if (code != 0)
            {
                _ext.Audit.Record(new AuditEntry(_ext.Clock.UtcNow, "PaymentDeclined", orderId, "BANK_" + code));
                throw new PaymentDeclinedException("BANK_" + code);
            }
            paymentRef = confirmation;
            status = "PAID";
        }
        else
        {
            // cod: customer pays the courier
            paymentRef = "COD";
            status = "PENDING";
        }

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
            if (order.PaymentMethod == "card")
            {
                var ok = _ext.CardPay.Refund(order.PaymentReference, (long)(order.Total * 100));
                if (!ok) throw new InvalidOperationException("Card refund failed for " + orderId);
            }
            else if (order.PaymentMethod == "wallet")
            {
                var resp = _ext.Wallet.Reverse(order.PaymentReference);
                if (resp.Status != "SUCCESS") throw new InvalidOperationException("Wallet reversal failed for " + orderId);
            }
            else if (order.PaymentMethod == "bank")
            {
                var code = _ext.LegacyBank.ReversePayment(order.PaymentReference, "R01"); // R01 = customer cancellation
                if (code != 0) throw new InvalidOperationException("Bank reversal failed for " + orderId + " code " + code);
            }

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
