using OrderEngine.Contract;
using OrderEngine.Discounts.Coupons;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderEngine.Shipping
{
    public interface IShippingCostCalculator
    {
        public decimal CalculateShippingCost(string country, ShippingMethod shippingMethod, decimal totalWeight, ICouponStrategy? coupon, decimal netGoods);

    }
}
