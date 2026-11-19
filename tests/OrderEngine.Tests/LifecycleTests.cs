using OrderEngine.Contract;
using OrderEngine.Tests.Support;
using static OrderEngine.Tests.Support.A;

namespace OrderEngine.Tests;

public class LifecycleTests
{
    private readonly TestWorld _world = new();
    private IOrderService Service => _world.Service;

    private string PlacePaidOrder(string payment = "card", string token = "tok_visa") =>
        Service.PlaceOrder(Order(payment: payment, token: token)).OrderId;

    private string PlaceCodOrder() => Service.PlaceOrder(Order(payment: "cod")).OrderId;

    [Fact]
    public void Paid_order_goes_through_shipped_to_delivered()
    {
        var id = PlacePaidOrder();
        Assert.Equal(OrderStatus.Paid, Service.GetStatus(id));

        Service.MarkShipped(id, "TRK-1");
        Assert.Equal(OrderStatus.Shipped, Service.GetStatus(id));

        Service.MarkDelivered(id);
        Assert.Equal(OrderStatus.Delivered, Service.GetStatus(id));

        Assert.Equal(["OrderPlaced", "OrderShipped", "OrderDelivered"], _world.AuditActionsFor(id));
    }

    [Fact]
    public void Cash_on_delivery_order_can_ship_while_pending_and_is_collected_on_delivery()
    {
        var id = PlaceCodOrder();
        Assert.Equal(OrderStatus.Pending, Service.GetStatus(id));

        Service.MarkShipped(id, "TRK-2");
        Service.MarkDelivered(id);

        Assert.Equal(OrderStatus.Delivered, Service.GetStatus(id));
        Assert.Equal(["OrderPlaced", "OrderShipped", "CodCollected", "OrderDelivered"], _world.AuditActionsFor(id));
    }

    [Fact]
    public void Cancelling_a_pending_order_needs_no_refund()
    {
        var id = PlaceCodOrder();

        Service.Cancel(id, "Customer called");

        Assert.Equal(OrderStatus.Cancelled, Service.GetStatus(id));
        Assert.Equal(["OrderPlaced", "OrderCancelled"], _world.AuditActionsFor(id));
    }

    [Fact]
    public void Cancelling_a_card_order_refunds_the_full_amount()
    {
        var r = Service.PlaceOrder(Order());

        Service.Cancel(r.OrderId, "Changed my mind");

        Assert.Equal(OrderStatus.Refunded, Service.GetStatus(r.OrderId));
        var refund = Assert.Single(_world.CardPay.Refunds);
        Assert.Equal(r.PaymentReference, refund.ChargeId);
        Assert.Equal(58953L, refund.AmountMinor);
        Assert.Equal(["OrderPlaced", "OrderCancelled", "OrderRefunded"], _world.AuditActionsFor(r.OrderId));
    }

    [Fact]
    public void Cancelling_a_wallet_order_reverses_the_debit()
    {
        var r = Service.PlaceOrder(Order(payment: "wallet", token: "01012345678"));

        Service.Cancel(r.OrderId, "Changed my mind");

        Assert.Equal(OrderStatus.Refunded, Service.GetStatus(r.OrderId));
        Assert.Equal([r.PaymentReference], _world.Wallet.Reversals);
    }

    [Fact]
    public void Cancelling_a_bank_order_reverses_with_reason_R01()
    {
        var r = Service.PlaceOrder(Order(payment: "bank", token: "EG-1234"));

        Service.Cancel(r.OrderId, "Changed my mind");

        var reversal = Assert.Single(_world.LegacyBank.Reversals);
        Assert.Equal(r.PaymentReference, reversal.ConfirmationCode);
        Assert.Equal("R01", reversal.ReasonCode);
    }

    [Fact]
    public void Shipped_orders_cannot_be_cancelled()
    {
        var id = PlacePaidOrder();
        Service.MarkShipped(id, "TRK-3");

        Assert.Throws<InvalidOrderTransitionException>(() => Service.Cancel(id, "Too late"));
        Assert.Equal(OrderStatus.Shipped, Service.GetStatus(id));
        Assert.Empty(_world.CardPay.Refunds);
    }

    [Fact]
    public void Orders_cannot_be_delivered_before_they_ship()
    {
        var id = PlacePaidOrder();

        Assert.Throws<InvalidOrderTransitionException>(() => Service.MarkDelivered(id));
    }

    [Fact]
    public void Orders_cannot_ship_twice()
    {
        var id = PlacePaidOrder();
        Service.MarkShipped(id, "TRK-4");

        Assert.Throws<InvalidOrderTransitionException>(() => Service.MarkShipped(id, "TRK-5"));
    }

    [Fact]
    public void Cancelled_orders_cannot_ship()
    {
        var id = PlaceCodOrder();
        Service.Cancel(id, "Customer called");

        Assert.Throws<InvalidOrderTransitionException>(() => Service.MarkShipped(id, "TRK-6"));
    }

    [Fact]
    public void Refunded_orders_cannot_be_cancelled_again()
    {
        var id = PlacePaidOrder();
        Service.Cancel(id, "Changed my mind");

        Assert.Throws<InvalidOrderTransitionException>(() => Service.Cancel(id, "Again"));
        Assert.Single(_world.CardPay.Refunds);
    }

    [Fact]
    public void Delivered_orders_are_final()
    {
        var id = PlacePaidOrder();
        Service.MarkShipped(id, "TRK-7");
        Service.MarkDelivered(id);

        Assert.Throws<InvalidOrderTransitionException>(() => Service.Cancel(id, "Too late"));
        Assert.Throws<InvalidOrderTransitionException>(() => Service.MarkShipped(id, "TRK-8"));
        Assert.Throws<InvalidOrderTransitionException>(() => Service.MarkDelivered(id));
    }

    [Fact]
    public void Shipping_requires_a_tracking_number()
    {
        var id = PlacePaidOrder();

        Assert.Throws<OrderValidationException>(() => Service.MarkShipped(id, " "));
    }

    [Fact]
    public void Cancelling_requires_a_reason()
    {
        var id = PlacePaidOrder();

        Assert.Throws<OrderValidationException>(() => Service.Cancel(id, ""));
    }

    [Fact]
    public void Unknown_orders_are_reported()
    {
        Assert.Throws<OrderNotFoundException>(() => Service.GetStatus("ORD-NOPE"));
        Assert.Throws<OrderNotFoundException>(() => Service.MarkShipped("ORD-NOPE", "TRK"));
        Assert.Throws<OrderNotFoundException>(() => Service.MarkDelivered("ORD-NOPE"));
        Assert.Throws<OrderNotFoundException>(() => Service.Cancel("ORD-NOPE", "x"));
    }

    [Fact]
    public void Audit_entries_use_the_clock()
    {
        var id = PlacePaidOrder();
        _world.Clock.UtcNow = _world.Clock.UtcNow.AddHours(5);
        Service.MarkShipped(id, "TRK-9");

        var entries = _world.Audit.Entries.Where(e => e.OrderId == id).ToList();
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero), entries[0].At);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 15, 0, 0, TimeSpan.Zero), entries[1].At);
    }
}
