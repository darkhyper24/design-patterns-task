using OrderEngine.Payment.Methods;

namespace OrderEngine.Payment;

public class PaymentMethodFactory
{
    private readonly Dictionary<string, IPaymentMethod> _methods;
    public PaymentMethodFactory(IDictionary<string, IPaymentMethod> methods)
    {
        _methods = new Dictionary<string, IPaymentMethod>(methods, StringComparer.OrdinalIgnoreCase);
    }
    public IPaymentMethod Create(string paymentMethod)
    {
        if (_methods.TryGetValue(paymentMethod, out var method))
            return method;

        throw new NotSupportedException("Unsupported payment method " + paymentMethod);
    }
}
