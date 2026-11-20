using OrderEngine.Contract;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderEngine.Shipping.Strategy
{
    public class EgyptStandardShippingStrategy : IShippingCostStrategy
    {
        public decimal Calculate(ShippingMethod shippingMethod, string country, decimal netGoods, decimal billableKg)
        {
            if (netGoods >= 2000)
                return 0;
            return 50 + 5 * Math.Max(0, billableKg - 5);
        }
    }
}
