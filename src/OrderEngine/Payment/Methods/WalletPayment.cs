using OrderEngine.Contract;
using OrderEngine.Vendor.Wallet;

namespace OrderEngine.Payment.Methods;
public class WalletPayment(WalletClient wallet, IAuditSink audit, IClock clock, decimal feePercent) : IPaymentMethod
{
    public decimal CalculateFee(decimal amountBeforeFee) =>
        Math.Round(amountBeforeFee * feePercent, 2, MidpointRounding.AwayFromZero);

    public string Pay(string paymentToken, decimal amount, string orderId)
    {
        var request = new WalletDebitRequest
        {
            MobileNumber = paymentToken,
            Amount = amount,
            MerchantReference = orderId
        };

        var response = wallet.Debit(request);
        if (response.Status != "SUCCESS")
        {
            audit.Record(new AuditEntry(clock.UtcNow, "PaymentDeclined", orderId, response.Status));
            throw new PaymentDeclinedException(response.Status);
        }
        return response.TransactionRef!;
    }

    public void Refund(string paymentReference, decimal amount, string orderId)
    {
        if (wallet.Reverse(paymentReference).Status != "SUCCESS")
            throw new InvalidOperationException("Wallet reversal failed for " + orderId);
    }
}
