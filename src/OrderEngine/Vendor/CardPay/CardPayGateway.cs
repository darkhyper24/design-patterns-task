namespace OrderEngine.Vendor.CardPay;

// CardPay .NET SDK v2.3 (sandbox build). DO NOT MODIFY.

public sealed record CardChargeResult(bool Approved, string? ChargeId, string? DeclineCode);

public sealed record CardCharge(string CardToken, long AmountMinor, string Currency, string ChargeId);

public sealed record CardRefund(string ChargeId, long AmountMinor);

public sealed class CardPayGateway
{
    private readonly List<CardCharge> _charges = [];
    private readonly List<CardRefund> _refunds = [];

    public IReadOnlyList<CardCharge> Charges => _charges;
    public IReadOnlyList<CardRefund> Refunds => _refunds;

    /// <summary>Charges a tokenized card. <paramref name="amountMinor"/> is in minor units (piastres).</summary>
    public CardChargeResult Charge(string cardToken, long amountMinor, string currency)
    {
        if (currency != "EGP")
            return new CardChargeResult(false, null, "CURRENCY_NOT_SUPPORTED");
        if (amountMinor <= 0)
            return new CardChargeResult(false, null, "INVALID_AMOUNT");
        if (cardToken == "tok_declined")
            return new CardChargeResult(false, null, "DO_NOT_HONOR");
        if (cardToken == "tok_expired")
            return new CardChargeResult(false, null, "EXPIRED_CARD");

        var chargeId = $"ch_{_charges.Count + 1:D4}";
        _charges.Add(new CardCharge(cardToken, amountMinor, currency, chargeId));
        return new CardChargeResult(true, chargeId, null);
    }

    /// <summary>Refunds a previous charge. Returns false if the charge is unknown or already refunded.</summary>
    public bool Refund(string chargeId, long amountMinor)
    {
        var charge = _charges.FirstOrDefault(c => c.ChargeId == chargeId);
        if (charge is null || amountMinor > charge.AmountMinor || _refunds.Any(r => r.ChargeId == chargeId))
            return false;

        _refunds.Add(new CardRefund(chargeId, amountMinor));
        return true;
    }
}
