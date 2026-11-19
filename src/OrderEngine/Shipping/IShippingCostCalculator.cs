using OrderEngine.Contract;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderEngine.Shipping
{
    public interface IShippingCostCalculator
    {
        public decimal CalculateShippingCost(string country, ShippingMethod shippingMethod, decimal totalWeight, string? coupon, decimal netGoods);

    }
}
