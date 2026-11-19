using OrderEngine.Contract;
using OrderEngine.Discounts.Coupons;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderEngine.Shipping
{
    public class ShippingCostCalculator : IShippingCostCalculator
    {
        public decimal CalculateShippingCost(string country, ShippingMethod shippingMethod, decimal totalWeight, ICouponStrategy? coupon, decimal netGoods)
        {
            var shippingCostStrategy = ShippingCostFactory.GetShippingCostStrategy(country, shippingMethod);
            var shippingCost = shippingCostStrategy.Calculate(shippingMethod, country, netGoods, totalWeight);
            if (coupon is { FreeStandardShipping: true } && shippingMethod == ShippingMethod.Standard)
            {
                shippingCost = 0;
            }
            return shippingCost;
        }
    }
}
