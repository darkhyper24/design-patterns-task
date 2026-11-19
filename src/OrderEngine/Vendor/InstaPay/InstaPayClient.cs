namespace OrderEngine.Vendor.InstaPay;

// InstaPay merchant SDK 1.0 (sandbox build). DO NOT MODIFY.

public enum TransferState { Accepted, Rejected, Cancelled }

public sealed record InstaPayTransfer(
    string Id,
    string Ipa,
    decimal AmountEgp,
    string Note,
    TransferState State,
    string? RejectionReason);

public sealed class InstaPayClient
{
    private readonly List<InstaPayTransfer> _transfers = [];

    public IReadOnlyList<InstaPayTransfer> Transfers => _transfers;

    /// <summary>
    /// Requests a transfer from the customer's InstaPay address (IPA), e.g. "mona@instapay".
    /// Always returns a transfer; check <see cref="InstaPayTransfer.State"/>.
    /// </summary>
    public InstaPayTransfer RequestTransfer(string ipa, decimal amountEgp, string note)
    {
        var id = $"IPN-{_transfers.Count + 1:D5}";

        InstaPayTransfer transfer;
        if (!ipa.EndsWith("@instapay"))
            transfer = new(id, ipa, amountEgp, note, TransferState.Rejected, "INVALID_IPA");
        else if (ipa.StartsWith("blocked"))
            transfer = new(id, ipa, amountEgp, note, TransferState.Rejected, "ACCOUNT_BLOCKED");
        else
            transfer = new(id, ipa, amountEgp, note, TransferState.Accepted, null);

        _transfers.Add(transfer);
        return transfer;
    }

    /// <summary>Cancels (refunds) an accepted transfer. Returns false if it can't be cancelled.</summary>
    public bool CancelTransfer(string transferId)
    {
        var index = _transfers.FindIndex(t => t.Id == transferId);
        if (index < 0 || _transfers[index].State != TransferState.Accepted)
            return false;

        _transfers[index] = _transfers[index] with { State = TransferState.Cancelled };
        return true;
    }
}
