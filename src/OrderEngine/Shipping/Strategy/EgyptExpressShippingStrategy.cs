using OrderEngine.Contract;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderEngine.Shipping.Strategy
{
    public class EgyptExpressShippingStrategy : IShippingCostStrategy
    {
        public decimal Calculate(ShippingMethod shippingMethod, string country, decimal netGoods, decimal billableKg)
        {
            return 120 + 10 * Math.Max(0, billableKg - 5);
        }
    }
}
