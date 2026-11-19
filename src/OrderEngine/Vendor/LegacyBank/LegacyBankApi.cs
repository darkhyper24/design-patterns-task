using System.Globalization;
using System.Xml.Linq;

namespace OrderEngine.Vendor.LegacyBank;

// Bank-to-Merchant Interface (BMI) 1998, .NET wrapper 2009 (sandbox build). DO NOT MODIFY.
//
// SubmitPayment expects an XML payload:
//   <payment>
//     <account>EG-1234</account>
//     <amount>1234.50</amount>     (invariant culture, exactly two decimals)
//     <currency>818</currency>     (ISO 4217 numeric code; 818 = EGP)
//     <ref>ORD-0001</ref>
//   </payment>
//
// Return codes:
//   0  = approved
//   14 = invalid account
//   30 = format error
//   51 = insufficient funds
//   12 = invalid transaction (reversals)

public sealed record BankPayment(string Account, decimal Amount, string Reference, string ConfirmationCode);

public sealed record BankReversal(string ConfirmationCode, string ReasonCode);

public sealed class LegacyBankApi
{
    public const int Approved = 0;
    public const int InvalidTransaction = 12;
    public const int InvalidAccount = 14;
    public const int FormatError = 30;
    public const int InsufficientFunds = 51;

    private static readonly string[] ValidReasonCodes = ["R01", "R02", "R03", "R04", "R05"];

    private readonly List<BankPayment> _payments = [];
    private readonly List<BankReversal> _reversals = [];

    public IReadOnlyList<BankPayment> Payments => _payments;
    public IReadOnlyList<BankReversal> Reversals => _reversals;

    public int SubmitPayment(string xmlPayload, out string confirmationCode)
    {
        confirmationCode = "";

        XElement root;
        try
        {
            root = XElement.Parse(xmlPayload);
        }
        catch (System.Xml.XmlException)
        {
            return FormatError;
        }

        var account = root.Element("account")?.Value;
        var amountText = root.Element("amount")?.Value;
        var currency = root.Element("currency")?.Value;
        var reference = root.Element("ref")?.Value;

        if (root.Name != "payment" || amountText is null || currency != "818" || string.IsNullOrEmpty(reference))
            return FormatError;

        var dot = amountText.IndexOf('.');
        if (dot < 0 || amountText.Length - dot - 1 != 2 ||
            !decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
            return FormatError;

        if (string.IsNullOrWhiteSpace(account))
            return InvalidAccount;
        if (account.StartsWith("FAIL"))
            return InsufficientFunds;

        confirmationCode = $"BMI{_payments.Count + 1:D8}";
        _payments.Add(new BankPayment(account, amount, reference, confirmationCode));
        return Approved;
    }

    /// <summary>Reverses a payment. <paramref name="reasonCode"/> must be one of R01..R05 (R01 = customer cancellation).</summary>
    public int ReversePayment(string confirmationCode, string reasonCode)
    {
        if (!ValidReasonCodes.Contains(reasonCode))
            return FormatError;
        if (_payments.All(p => p.ConfirmationCode != confirmationCode) ||
            _reversals.Any(r => r.ConfirmationCode == confirmationCode))
            return InvalidTransaction;

        _reversals.Add(new BankReversal(confirmationCode, reasonCode));
        return Approved;
    }
}
