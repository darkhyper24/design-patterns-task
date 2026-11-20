using System.Globalization;
using OrderEngine.Contract;
using OrderEngine.Vendor.LegacyBank;

namespace OrderEngine.Payment.Methods;
public class BankTransferPayment(LegacyBankApi bank, IAuditSink audit, IClock clock, decimal feeFixed) : IPaymentMethod
{
    private const string EgpNumericCurrencyCode = "818";   
    private const string CustomerCancellation = "R01";

    public decimal CalculateFee(decimal amountBeforeFee) => feeFixed;

    public string Pay(string paymentToken, decimal amount, string orderId)
    {
        var xml = "<payment>"
                  + "<account>" + paymentToken + "</account>"
                  + "<amount>" + amount.ToString("0.00", CultureInfo.InvariantCulture) + "</amount>"
                  + "<currency>" + EgpNumericCurrencyCode + "</currency>"
                  + "<ref>" + orderId + "</ref>"
                  + "</payment>";

        var code = bank.SubmitPayment(xml, out var confirmation);
        if (code != 0)
        {
            audit.Record(new AuditEntry(clock.UtcNow, "PaymentDeclined", orderId, "BANK_" + code));
            throw new PaymentDeclinedException("BANK_" + code);
        }
        return confirmation;
    }

    public void Refund(string paymentReference, decimal amount, string orderId)
    {
        var code = bank.ReversePayment(paymentReference, CustomerCancellation);
        if (code != 0)
            throw new InvalidOperationException("Bank reversal failed for " + orderId + " code " + code);
    }
}
