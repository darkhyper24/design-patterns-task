namespace OrderEngine.Vendor.Wallet;

// MobiWallet merchant client 4.1 (sandbox build). DO NOT MODIFY.

public sealed class WalletDebitRequest
{
    public string MobileNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public string MerchantReference { get; set; } = "";
}

public sealed class WalletResponse
{
    /// <summary>"SUCCESS", "INSUFFICIENT_FUNDS", "INVALID_NUMBER" or "NOT_FOUND".</summary>
    public string Status { get; set; } = "";
    public string? TransactionRef { get; set; }
}

public sealed record WalletDebit(string MobileNumber, decimal Amount, string MerchantReference, string TransactionRef);

public sealed class WalletClient
{
    private readonly List<WalletDebit> _debits = [];
    private readonly List<string> _reversals = [];

    public IReadOnlyList<WalletDebit> Debits => _debits;
    public IReadOnlyList<string> Reversals => _reversals;

    public WalletResponse Debit(WalletDebitRequest request)
    {
        var number = request.MobileNumber;
        if (number.Length != 11 || !number.StartsWith("01") || !number.All(char.IsDigit))
            return new WalletResponse { Status = "INVALID_NUMBER" };
        if (number == "01000000000")
            return new WalletResponse { Status = "INSUFFICIENT_FUNDS" };

        var txRef = $"MW{_debits.Count + 1:D6}";
        _debits.Add(new WalletDebit(number, request.Amount, request.MerchantReference, txRef));
        return new WalletResponse { Status = "SUCCESS", TransactionRef = txRef };
    }

    public WalletResponse Reverse(string transactionRef)
    {
        if (_debits.All(d => d.TransactionRef != transactionRef) || _reversals.Contains(transactionRef))
            return new WalletResponse { Status = "NOT_FOUND" };

        _reversals.Add(transactionRef);
        return new WalletResponse { Status = "SUCCESS", TransactionRef = transactionRef };
    }
}
