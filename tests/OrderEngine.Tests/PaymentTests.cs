using OrderEngine.Contract;
using OrderEngine.Tests.Support;
using static OrderEngine.Tests.Support.A;

namespace OrderEngine.Tests;

public class PaymentTests
{
    private readonly TestWorld _world = new();

    [Fact]
    public void Card_is_charged_the_total_in_piastres()
    {
        var r = _world.Service.PlaceOrder(Order(token: "tok_visa"));

        var charge = Assert.Single(_world.CardPay.Charges);
        Assert.Equal("tok_visa", charge.CardToken);
        Assert.Equal(58953L, charge.AmountMinor);
        Assert.Equal("EGP", charge.Currency);
        Assert.Equal(charge.ChargeId, r.PaymentReference);
        Assert.Equal(OrderStatus.Paid, r.Status);
    }

    [Theory]
    [InlineData("tok_declined", "DO_NOT_HONOR")]
    [InlineData("tok_expired", "EXPIRED_CARD")]
    public void Declined_card_throws_and_nothing_is_sent(string token, string reason)
    {
        var ex = Assert.Throws<PaymentDeclinedException>(() => _world.Service.PlaceOrder(Order(token: token)));

        Assert.Equal(reason, ex.Reason);
        Assert.Empty(_world.Mailer.Sent);
        var entry = Assert.Single(_world.Audit.Entries);
        Assert.Equal("PaymentDeclined", entry.Action);
    }

    [Fact]
    public void Wallet_is_debited_with_the_order_id_as_reference()
    {
        var r = _world.Service.PlaceOrder(Order(payment: "wallet", token: "01012345678"));

        var debit = Assert.Single(_world.Wallet.Debits);
        Assert.Equal("01012345678", debit.MobileNumber);
        Assert.Equal(r.Total, debit.Amount);
        Assert.Equal(r.OrderId, debit.MerchantReference);
        Assert.Equal(debit.TransactionRef, r.PaymentReference);
    }

    [Theory]
    [InlineData("01000000000", "INSUFFICIENT_FUNDS")]
    [InlineData("12345", "INVALID_NUMBER")]
    public void Failed_wallet_debit_throws(string mobile, string reason)
    {
        var ex = Assert.Throws<PaymentDeclinedException>(() =>
            _world.Service.PlaceOrder(Order(payment: "wallet", token: mobile)));

        Assert.Equal(reason, ex.Reason);
    }

    [Fact]
    public void Bank_payment_is_submitted_with_amount_account_and_reference()
    {
        var r = _world.Service.PlaceOrder(Order(payment: "bank", token: "EG-1234", lines: Line("MN-27")));

        var payment = Assert.Single(_world.LegacyBank.Payments);
        Assert.Equal("EG-1234", payment.Account);
        Assert.Equal(7000m + 980m + 15m, payment.Amount);
        Assert.Equal(r.OrderId, payment.Reference);
        Assert.Equal(payment.ConfirmationCode, r.PaymentReference);
    }

    [Fact]
    public void Failed_bank_payment_throws_with_the_bank_code()
    {
        var ex = Assert.Throws<PaymentDeclinedException>(() =>
            _world.Service.PlaceOrder(Order(payment: "bank", token: "FAIL-ACC")));

        Assert.Equal("BANK_51", ex.Reason);
    }

    [Fact]
    public void Cash_on_delivery_does_not_call_any_provider_and_stays_pending()
    {
        var r = _world.Service.PlaceOrder(Order(payment: "cod"));

        Assert.Equal(OrderStatus.Pending, r.Status);
        Assert.Equal("COD", r.PaymentReference);
        Assert.Empty(_world.CardPay.Charges);
        Assert.Empty(_world.Wallet.Debits);
        Assert.Empty(_world.LegacyBank.Payments);
    }

    [Fact]
    public void Each_order_gets_a_unique_id()
    {
        var a = _world.Service.PlaceOrder(Order());
        var b = _world.Service.PlaceOrder(Order());

        Assert.False(string.IsNullOrWhiteSpace(a.OrderId));
        Assert.NotEqual(a.OrderId, b.OrderId);
    }
}
