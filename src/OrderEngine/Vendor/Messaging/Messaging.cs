namespace OrderEngine.Vendor.Messaging;

// Messaging SDKs (sandbox builds). DO NOT MODIFY.

public sealed record SentEmail(string To, string Subject, string Body);

public sealed class SmtpMailer
{
    private readonly List<SentEmail> _sent = [];
    public IReadOnlyList<SentEmail> Sent => _sent;

    public void Send(string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(to) || !to.Contains('@'))
            throw new ArgumentException($"Invalid email address '{to}'", nameof(to));

        _sent.Add(new SentEmail(to, subject, body));
    }
}

public sealed record SentSms(string Phone, string Text);

public sealed class SmsGateway
{
    public const int MaxLength = 160;

    private readonly List<SentSms> _sent = [];
    public IReadOnlyList<SentSms> Sent => _sent;

    public void SendText(string phone, string text)
    {
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Phone number is required", nameof(phone));
        if (text.Length > MaxLength)
            throw new ArgumentException($"SMS text exceeds {MaxLength} characters", nameof(text));

        _sent.Add(new SentSms(phone, text));
    }
}

public sealed record WebhookCall(string Url, string JsonBody);

public sealed class WebhookClient
{
    private readonly List<WebhookCall> _calls = [];
    public IReadOnlyList<WebhookCall> Calls => _calls;

    public void Post(string url, string jsonBody)
    {
        if (url.Contains("fail", StringComparison.OrdinalIgnoreCase))
            throw new HttpRequestException($"POST {url} failed: 503 Service Unavailable");

        _calls.Add(new WebhookCall(url, jsonBody));
    }
}
