# Vendor SDKs — DO NOT MODIFY

Everything under `Vendor/` simulates third-party SDKs that, in real life, you would get as NuGet packages
and could not change. Each one records the calls it receives so the tests can check them.

| SDK | Used for | Notes |
|---|---|---|
| `CardPay` | Card payments | Amounts in **piastres** (`long`, 1 EGP = 100 piastres) |
| `Wallet` | Mobile wallet payments | Mutable request/response DTOs, string status codes |
| `LegacyBank` | Bank transfers | XML payloads, numeric return codes, `out` parameters. Built in 1998. |
| `InstaPay` | Instant payments | Not used anywhere yet |
| `Messaging` | Email, SMS, webhooks | Throws on invalid input |

If an SDK's API is awkward, that's realistic. Don't edit it; work around it in your own code.
