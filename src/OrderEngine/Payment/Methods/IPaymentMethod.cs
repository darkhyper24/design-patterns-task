namespace OrderEngine.Payment.Methods;
public interface IPaymentMethod
{
    decimal CalculateFee(decimal amountBeforeFee);
    string Pay(string paymentToken, decimal amount, string orderId);
    void Refund(string paymentReference, decimal amount, string orderId);
}
