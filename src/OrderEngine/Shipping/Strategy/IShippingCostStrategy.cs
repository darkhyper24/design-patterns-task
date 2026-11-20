using OrderEngine.Contract;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderEngine.Shipping.Strategy
{
    public interface IShippingCostStrategy
    {
        public decimal Calculate(ShippingMethod shippingMethod, string country, decimal netGoods, decimal billableKg);
    }
}
