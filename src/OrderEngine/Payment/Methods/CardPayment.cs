using OrderEngine.Contract;
using OrderEngine.Vendor.CardPay;

namespace OrderEngine.Payment.Methods;
public class CardPayment(
    CardPayGateway gateway, IAuditSink audit, IClock clock,
    string currency, decimal feePercent, decimal feeFixed) : IPaymentMethod
{
    public decimal CalculateFee(decimal amountBeforeFee) =>
        Math.Round(amountBeforeFee * feePercent + feeFixed, 2, MidpointRounding.AwayFromZero);

    public string Pay(string paymentToken, decimal amount, string orderId)
    {
        var result = gateway.Charge(paymentToken, ToPiastres(amount), currency);
        if (!result.Approved)
        {
            var reason = result.DeclineCode ?? "UNKNOWN";
            audit.Record(new AuditEntry(clock.UtcNow, "PaymentDeclined", orderId, reason));
            throw new PaymentDeclinedException(reason);
        }
        return result.ChargeId!;
    }

    public void Refund(string paymentReference, decimal amount, string orderId)
    {
        if (!gateway.Refund(paymentReference, ToPiastres(amount)))
            throw new InvalidOperationException("Card refund failed for " + orderId);
    }

    private static long ToPiastres(decimal amount) => (long)(amount * 100);
}
