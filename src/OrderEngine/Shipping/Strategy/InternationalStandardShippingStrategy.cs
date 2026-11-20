using System;
using System.Collections.Generic;
using System.Text;
using OrderEngine.Contract;


namespace OrderEngine.Shipping.Strategy
{
    internal class InternationalStandardShippingStrategy : IShippingCostStrategy
    {
        public decimal Calculate(ShippingMethod shippingMethod, string country, decimal netGoods, decimal billableKg)
        {
            return 300 + 25 * billableKg;
        }
    }
}
