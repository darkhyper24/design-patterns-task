# FutureMakers — Week 3 Assignment: Rescue the OrderEngine

**Work in pairs · Submit a repo link**

## The story

FutureShop's `OrderEngine` takes customer orders: it prices them, takes the payment, notifies the customer,
and moves each order through its lifecycle. It works. Customers are happy. Developers are not.

Almost everything lives in one 500-line class, `OrderService.cs`. Every new payment method, coupon or
notification channel means editing the same giant methods, and every edit risks breaking something else.

The business has two feature requests waiting (see below). Your team lead says:

> "Don't just bolt them on. Refactor first so that adding them is *easy*, then add them."

## Getting started

```bash
dotnet test        # 66 tests, all green. Keep them green.
```

Read the code in this order: `Contract/Contract.cs` → `OrderService.cs` → `tests/`.

## The rules

1. **Do not modify** anything in `src/OrderEngine/Contract/` or `src/OrderEngine/Vendor/`.
   Vendor code represents third-party SDKs you can't change; Contract is the public API other teams use.
2. **Keep the signature** of `Composition/Bootstrap.Build(ExternalSystems)`. Its body is yours.
3. **Do not modify the existing tests.** They describe current behaviour (they're *characterization tests*).
   If they stay green, you haven't broken anything. You may **add** tests.
4. Everything else (`OrderService.cs`, `GlobalSettings.cs`, new folders and classes) is yours to change.

Your submission is checked automatically against these structural rules, so treat them as requirements:

| # | Rule |
|---|---|
| A1 | The class that implements `IOrderService` must not use any `Vendor` SDK directly. |
| A2 | Apart from `Composition/`, no class may use more than one vendor SDK. (`Messaging` counts as one SDK.) |
| A3 | No mutable `static` state (no static fields or properties that can change). |
| A4 | No classic Singletons (no class exposing a static instance of itself). |
| A5 | No source file over **200 lines** (`Contract/` and `Vendor/` excluded). |

## Part 1 — Refactor

Restructure the engine using **at least 5** design patterns from the session. Expect to need most of these:

- **Adapter**: each payment SDK has a completely different API (piastres, mutable DTOs, XML from 1998...)
- **Strategy**: discounts, coupons, shipping rules and payment fees all change independently
- **Factory / registry**: turn `"card"` or `"FREESHIP"` into the right object
- **Observer**: placing or shipping an order triggers audit + email + SMS + webhook
- **Decorator**: "a failing notification channel must never fail the order" is a cross-cutting concern
- **State**: an order's allowed moves depend on its status (and cash-on-delivery orders behave differently)
- **Facade**: `IOrderService` should orchestrate, not decide
- **Stretch**: Chain of Responsibility for validation rules, Null Object, Template Method

Use a pattern only where it solves a real problem in *this* code. One unnecessary pattern costs you more
than one missing pattern.

## Part 2 — Feature requests

Implement these **after** your refactor. If the refactor is good, each one should be small.

### Feature 1: InstaPay payments

- New payment method `"instapay"` (case-insensitive). The `PaymentToken` is the customer's InstaPay address (IPA), e.g. `mona@instapay`.
- Available in **Egypt only**; anywhere else → `OrderValidationException`.
- Fee: **0.1%** of the amount before fees, rounded to 2 decimals (midpoint away from zero), but **at least 0.50 EGP** and **at most 20.00 EGP**.
- Collect payment with `InstaPayClient.RequestTransfer(ipa, total, note)`, with the **order id** as the note.
  - `Accepted` → the order is `Paid`, and `PaymentReference` is the transfer `Id`.
  - `Rejected` → audit `PaymentDeclined`, throw `PaymentDeclinedException` with the `RejectionReason` as the reason.
- Cancelling a paid InstaPay order calls `CancelTransfer(transferId)`, and the order becomes `Refunded`, like other paid orders.

### Feature 2: "Buy 2, get 1 free" coupon

- New coupon code `B2G1` (case-insensitive, trimmed, like the others). Any customer tier may use it.
- For **each cart line**, every third unit is free: `free units = quantity / 3` (integer division).
  Discount = Σ free units × unit price.
- It stacks with tier and bulk discounts, and the existing **30% cap** on total discount still applies.

Example: 3 × Wireless Mouse (450 each) → 1 free = 450, but the cap is 30% × 1350 = **405**.

## Business rules (as currently implemented)

The tests are the source of truth. This summary helps you read them.

**Order of calculation:** subtotal → discounts (capped) → shipping → tax → payment fee → total.
Every percentage is rounded to 2 decimals, midpoint away from zero.

| Area | Rule |
|---|---|
| Validation | Cart not empty; quantity 1–100; known SKUs; country EG, SA or AE; known payment method & coupon |
| Tier discount | Gold 5%, Platinum 10% of subtotal |
| Bulk discount | 5% off any line with quantity ≥ 10 |
| Coupons | `WELCOME10` 10% (Regular tier only) · `FLAT100` 100 EGP if subtotal ≥ 1000 · `FREESHIP` free *Standard* shipping |
| Discount cap | Total discount ≤ 30% of subtotal |
| Shipping EG | Standard 50 + 5/kg over 5 kg (free if subtotal after discount ≥ 2000) · Express 120 + 10/kg over 5 kg · Pickup free |
| Shipping SA/AE | Standard 300 + 25/kg · Express 1.5× Standard · no Pickup |
| Weight | Sum of all units, rounded **up** to the next kg |
| VAT | EG 14% · SA 15% · AE 5%, applied to (goods after discount + shipping) |
| Payment fees | card 2.9% + 3 · wallet 1.5% (EG only) · bank 15 flat · cod 25 flat (EG only, total ≤ 10,000) |
| Lifecycle | Paid → Shipped → Delivered. COD orders start Pending, can ship while Pending, and are "collected" on delivery. Cancel: Pending → Cancelled; Paid → refund → Refunded; anything else is rejected |
| Notifications | On placed/shipped/delivered/cancelled, notify every channel the customer chose. A failing channel is audited as `NotificationFailed` and never fails the operation |

## Deliverables

1. **Your code**, in a Git repo (GitHub/GitLab/Azure DevOps, or a branch of the shared intern repo), with a sensible commit history.
   Commit after every green step: *small steps, tests green, commit, repeat*.
2. **`DESIGN.md`** at the repo root, using the template in `DESIGN.template.md`.
3. All 66 original tests green, plus any tests you add.
4. A **10-minute "defend your design"** talk: walk through your structure, then show how you added Feature 1.

Send the repo link to your mentor.

## Tips

- **Don't rewrite from scratch.** Move one responsibility at a time out of `OrderService`, then run the tests.
- Start with the biggest pain point. The payment `if/else` chains are a good first target.
- When a test fails after a refactor, you changed behaviour. Undo, and take a smaller step.
- If you can't explain *why* a pattern is there, delete it.
