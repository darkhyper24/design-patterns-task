using System.Text.Json;
using OrderEngine.Contract;
using OrderEngine.Tests.Support;
using static OrderEngine.Tests.Support.A;

namespace OrderEngine.Tests;

public class NotificationTests
{
    private readonly TestWorld _world = new();

    private static readonly NotificationChannel[] AllChannels =
        [NotificationChannel.Email, NotificationChannel.Sms, NotificationChannel.Webhook];

    [Fact]
    public void Order_placed_sends_a_confirmation_email()
    {
        var r = _world.Service.PlaceOrder(Order());

        var email = Assert.Single(_world.Mailer.Sent);
        Assert.Equal("mona@example.com", email.To);
        Assert.Equal($"Order {r.OrderId} confirmed", email.Subject);
        Assert.Contains("Hi Mona", email.Body);
        Assert.Contains("Total: 589.53 EGP", email.Body);
    }

    [Fact]
    public void Order_placed_notifies_every_chosen_channel()
    {
        var customer = Customer(channels: AllChannels, webhookUrl: "https://hooks.example.com/orders");
        var r = _world.Service.PlaceOrder(Order(customer));

        Assert.Single(_world.Mailer.Sent);

        var sms = Assert.Single(_world.Sms.Sent);
        Assert.Equal("01012345678", sms.Phone);
        Assert.Equal($"FutureShop: order {r.OrderId} confirmed. Total 589.53 EGP", sms.Text);

        var call = Assert.Single(_world.Webhooks.Calls);
        Assert.Equal("https://hooks.example.com/orders", call.Url);
        var json = JsonDocument.Parse(call.JsonBody).RootElement;
        Assert.Equal("order.placed", json.GetProperty("event").GetString());
        Assert.Equal(r.OrderId, json.GetProperty("orderId").GetString());
        Assert.Equal(589.53m, json.GetProperty("total").GetDecimal());
        Assert.Equal("Paid", json.GetProperty("status").GetString());
    }

    [Fact]
    public void Only_chosen_channels_are_used()
    {
        _world.Service.PlaceOrder(Order(Customer(channels: [NotificationChannel.Sms])));

        Assert.Empty(_world.Mailer.Sent);
        Assert.Single(_world.Sms.Sent);
        Assert.Empty(_world.Webhooks.Calls);
    }

    [Fact]
    public void No_channels_means_no_notifications()
    {
        _world.Service.PlaceOrder(Order(Customer(channels: [])));

        Assert.Empty(_world.Mailer.Sent);
        Assert.Empty(_world.Sms.Sent);
        Assert.Empty(_world.Webhooks.Calls);
    }

    [Fact]
    public void Webhook_channel_without_a_url_is_skipped()
    {
        var r = _world.Service.PlaceOrder(Order(Customer(channels: [NotificationChannel.Webhook])));

        Assert.Empty(_world.Webhooks.Calls);
        Assert.DoesNotContain("NotificationFailed", _world.AuditActionsFor(r.OrderId));
    }

    [Fact]
    public void A_failing_channel_is_audited_but_does_not_fail_the_order_or_other_channels()
    {
        var customer = Customer(channels: AllChannels, webhookUrl: "https://fail.example.com/hook");
        var r = _world.Service.PlaceOrder(Order(customer));

        Assert.Equal(OrderStatus.Paid, _world.Service.GetStatus(r.OrderId));
        Assert.Single(_world.Mailer.Sent);
        Assert.Single(_world.Sms.Sent);
        var failure = Assert.Single(_world.Audit.Entries, e => e.Action == "NotificationFailed");
        Assert.StartsWith("Webhook:", failure.Details);
    }

    [Fact]
    public void An_invalid_email_address_is_audited_as_a_failed_notification()
    {
        var r = _world.Service.PlaceOrder(Order(Customer(email: "not-an-email")));

        Assert.Empty(_world.Mailer.Sent);
        var failure = Assert.Single(_world.Audit.Entries, e => e.Action == "NotificationFailed");
        Assert.Equal(r.OrderId, failure.OrderId);
        Assert.StartsWith("Email:", failure.Details);
    }

    [Fact]
    public void Shipping_notifies_with_the_tracking_number()
    {
        var customer = Customer(channels: AllChannels, webhookUrl: "https://hooks.example.com/orders");
        var r = _world.Service.PlaceOrder(Order(customer));

        _world.Service.MarkShipped(r.OrderId, "TRK-998");

        Assert.Equal($"Order {r.OrderId} shipped", _world.Mailer.Sent[1].Subject);
        Assert.Contains("Tracking number: TRK-998", _world.Mailer.Sent[1].Body);
        Assert.Equal($"FutureShop: order {r.OrderId} shipped. Tracking TRK-998", _world.Sms.Sent[1].Text);
        var json = JsonDocument.Parse(_world.Webhooks.Calls[1].JsonBody).RootElement;
        Assert.Equal("order.shipped", json.GetProperty("event").GetString());
        Assert.Equal("TRK-998", json.GetProperty("trackingNumber").GetString());
    }

    [Fact]
    public void Delivery_notifies_the_customer()
    {
        var customer = Customer(channels: AllChannels, webhookUrl: "https://hooks.example.com/orders");
        var r = _world.Service.PlaceOrder(Order(customer));
        _world.Service.MarkShipped(r.OrderId, "TRK-1");

        _world.Service.MarkDelivered(r.OrderId);

        Assert.Equal($"Order {r.OrderId} delivered", _world.Mailer.Sent[2].Subject);
        Assert.Equal($"FutureShop: order {r.OrderId} delivered", _world.Sms.Sent[2].Text);
        var json = JsonDocument.Parse(_world.Webhooks.Calls[2].JsonBody).RootElement;
        Assert.Equal("order.delivered", json.GetProperty("event").GetString());
    }

    [Fact]
    public void Cancelling_a_paid_order_tells_the_customer_about_the_refund()
    {
        var customer = Customer(channels: AllChannels, webhookUrl: "https://hooks.example.com/orders");
        var r = _world.Service.PlaceOrder(Order(customer));

        _world.Service.Cancel(r.OrderId, "Changed my \"mind\"");

        var email = _world.Mailer.Sent[1];
        Assert.Equal($"Order {r.OrderId} cancelled and refunded", email.Subject);
        Assert.Contains("Reason: Changed my \"mind\"", email.Body);
        Assert.Contains("A refund of 589.53 EGP has been issued.", email.Body);
        Assert.Equal($"FutureShop: order {r.OrderId} cancelled. Refund 589.53 EGP", _world.Sms.Sent[1].Text);
        var json = JsonDocument.Parse(_world.Webhooks.Calls[1].JsonBody).RootElement;
        Assert.Equal("order.cancelled", json.GetProperty("event").GetString());
        Assert.Equal("Changed my \"mind\"", json.GetProperty("reason").GetString());
        Assert.True(json.GetProperty("refunded").GetBoolean());
    }

    [Fact]
    public void Cancelling_an_unpaid_order_does_not_mention_a_refund()
    {
        var r = _world.Service.PlaceOrder(Order(payment: "cod"));

        _world.Service.Cancel(r.OrderId, "Duplicate order");

        var email = _world.Mailer.Sent[1];
        Assert.Equal($"Order {r.OrderId} cancelled", email.Subject);
        Assert.DoesNotContain("refund", email.Body, StringComparison.OrdinalIgnoreCase);
    }
}
