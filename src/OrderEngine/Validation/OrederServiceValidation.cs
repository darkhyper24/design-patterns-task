using OrderEngine.Contract;
using OrderEngine.Discounts.Coupons;

namespace OrderEngine.Validation
{
    public class OrederServiceValidation : IOrderServiceValidation
    {
        private readonly ExternalSystems _externalSystems;
        private readonly CouponFactory _coupons;

        public OrederServiceValidation(ExternalSystems externalSystems, CouponFactory coupons)
        {
            _externalSystems = externalSystems;
            _coupons = coupons;
        }
        public void Validate(PlaceOrderRequest orderRequest)
        {
            ValidateRequestExistance(orderRequest);
            ValidateCustomer(orderRequest);
            ValidateCartLines(orderRequest);
            ValidateCustomerCountry(orderRequest);
            ValidatePaymentMethod(orderRequest);
            ValidateShipping(orderRequest);
            ValidateCouponCode(orderRequest);
        }

        private void ValidateRequestExistance(PlaceOrderRequest orderRequest)
        {
            if (orderRequest == null) throw new ArgumentNullException(nameof(orderRequest));
        }

        private void ValidateCustomer(PlaceOrderRequest orderRequest)
        {
            var customer = orderRequest.Customer;
            if (customer == null) throw new OrderValidationException("Customer is required");
        }

        private void ValidateCartLines(PlaceOrderRequest orderRequest)
        {
            var lines = orderRequest.Lines;
            if (lines == null || lines.Count == 0) throw new OrderValidationException("Cart is empty");
            foreach (var line in lines)
            {
                if (line.Quantity < 1 || line.Quantity > 100)
                    throw new OrderValidationException("Invalid quantity for " + line.Sku);
                var product = _externalSystems.Catalog.Find(line.Sku);
                if (product == null)
                    throw new OrderValidationException("Unknown product " + line.Sku);
            }
        }

        private void ValidateCustomerCountry(PlaceOrderRequest orderRequest)
        {
            var country = orderRequest.Customer?.Country;
            if (country != "EG" && country != "SA" && country != "AE")
                throw new OrderValidationException("We do not ship to " + country);
        }

        private void ValidatePaymentMethod(PlaceOrderRequest orderRequest)
        {
            var paymentMethod = orderRequest.PaymentMethod;
            var country = orderRequest.Customer?.Country;
            var pm = paymentMethod?.ToLower();
            if (pm != "card" && pm != "wallet" && pm != "bank" && pm != "cod")
                throw new OrderValidationException("Unsupported payment method " + paymentMethod);
            if (pm == "wallet" && country != "EG")
                throw new OrderValidationException("Wallet payments are only available in Egypt");
            if (pm == "cod" && country != "EG")
                throw new OrderValidationException("Cash on delivery is only available in Egypt");
        }

        private void ValidateShipping(PlaceOrderRequest orderRequest)
        {
            var shipping = orderRequest.Shipping;
            var country = orderRequest.Customer?.Country;
            if (shipping == ShippingMethod.Pickup && country != "EG")
                throw new OrderValidationException("Pickup is only available in Egypt");
        }

        private void ValidateCouponCode(PlaceOrderRequest orderRequest)
        {
            _coupons.Create(orderRequest.CouponCode)?.CheckEligibility(orderRequest.Customer);
        }
    }
}
