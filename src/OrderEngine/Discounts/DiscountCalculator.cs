using OrderEngine.Contract;
using OrderEngine.Discounts.Coupons;

namespace OrderEngine.Discounts;

public class DiscountCalculator
{
    private readonly decimal _maxDiscountPercent;

    public DiscountCalculator(decimal maxDiscountPercent)
    {
        _maxDiscountPercent = maxDiscountPercent;
    }

    public decimal Calculate(Customer customer, IReadOnlyList<CartLine> lines,
        IReadOnlyList<Product> products, decimal subtotal, ICouponStrategy? coupon)
    {
        var discount = TierDiscount(customer.Tier, subtotal)
                       + BulkDiscount(lines, products)
                       + (coupon?.Discount(subtotal) ?? 0);

        var maxDiscount = Round(subtotal * _maxDiscountPercent);
        return Math.Min(discount, maxDiscount);
    }

    // Gold 5%, Platinum 10% of the subtotal.
    private static decimal TierDiscount(CustomerTier tier, decimal subtotal) => tier switch
    {
        CustomerTier.Gold => Round(subtotal * 0.05m),
        CustomerTier.Platinum => Round(subtotal * 0.10m),
        _ => 0,
    };

    // 5% off every line with 10 or more units, rounded per line.
    private static decimal BulkDiscount(IReadOnlyList<CartLine> lines, IReadOnlyList<Product> products)
    {
        decimal discount = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Quantity >= 10)
                discount += Round(products[i].UnitPrice * lines[i].Quantity * 0.05m);
        }
        return discount;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
