using OrderEngine.Contract;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderEngine.Shipping
{
    public class InternationalExpressShippingStrategy : IShippingCostStrategy
    {
        public decimal Calculate(ShippingMethod shippingMethod, string country, decimal netGoods, decimal billableKg)
        {
            return Math.Round((300 + 25 * billableKg) * 1.5m, 2, MidpointRounding.AwayFromZero);
        }
    }
}
