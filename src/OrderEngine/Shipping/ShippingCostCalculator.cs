using OrderEngine.Contract;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderEngine.Shipping
{
    public class ShippingCostCalculator : IShippingCostCalculator
    {
        public decimal CalculateShippingCost(string country, ShippingMethod shippingMethod, decimal totalWeight, string? coupon, decimal netGoods)
        {
            var shippingCostStrategy = ShippingCostFactory.GetShippingCostStrategy(country, shippingMethod);
            var shippingCost = shippingCostStrategy.Calculate(shippingMethod, country, netGoods, totalWeight);
            if (coupon == "FREESHIP" && shippingMethod == ShippingMethod.Standard)
            {
                shippingCost = 0;
            }
            return shippingCost;
        }
    }
}
