namespace OrderEngine.Payment.Methods;
public class CashOnDeliveryPayment(decimal feeFixed) : IPaymentMethod
{
    public decimal CalculateFee(decimal amountBeforeFee) => feeFixed;

    public string Pay(string paymentToken, decimal amount, string orderId) => "COD";

    public void Refund(string paymentReference, decimal amount, string orderId)
    {
    }
}
