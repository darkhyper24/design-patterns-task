using OrderEngine.Contract;
using OrderEngine.Shipping.Strategy;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderEngine.Shipping
{
    public class ShippingCostFactory
    {
        public static IShippingCostStrategy GetShippingCostStrategy(string country, ShippingMethod shippingMethod)
        {
            if (shippingMethod == ShippingMethod.Pickup)
            {
                return new PickupShippingStrategy();
            }
            else if (country == "EG" && shippingMethod == ShippingMethod.Express)
            {
                return new EgyptExpressShippingStrategy();
            }
            else if (country == "EG" && shippingMethod == ShippingMethod.Standard)
            {
                return new EgyptStandardShippingStrategy();
            }
            else if (country != "EG" && shippingMethod == ShippingMethod.Standard)
            {
                return new InternationalStandardShippingStrategy();
            }
            else if (country != "EG" && shippingMethod == ShippingMethod.Express)
            {
                return new InternationalExpressShippingStrategy();
            }
            else
            {
                throw new NotSupportedException("Unsupported shipping method or country.");
            }
        }
    }
}
