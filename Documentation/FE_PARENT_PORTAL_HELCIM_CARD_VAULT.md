# Parent Portal: Helcim Saved Cards and Payment Flow

## 1. Purpose

This guide defines the Parent Portal implementation for Helcim payments and the saved-card feature.

There are two distinct checkout paths:

1. **New card or ACH checkout:** the existing HelcimPay.js flow. The parent enters payment data in Helcim's hosted UI. The parent may opt in to save a card after a successful card payment.
2. **Saved card checkout:** the Parent Portal sends a selected saved-card ID to Maktab. Maktab validates ownership and charges the Helcim token server-side. The portal never receives a card token or card number.

The backend is the source of truth for payment completion. A browser success response or a saved-card API approval is not sufficient to mark an enrollment paid. Maktab finalizes the course payment after the Helcim webhook or an admin invoice sync.

## 2. Authentication and Security

All endpoints in this guide require the normal parent authentication token and session header.

```http
Authorization: Bearer <JWT>
Session_Info: <session-guid>
Content-Type: application/json
```

Do not send or store these values in the Parent Portal:

- Helcim API token.
- Helcim webhook signature secret.
- Saved card token.
- PAN, CVV, card expiry, or any payment field outside HelcimPay.js.
- User ID or Family ID as a payment ownership value. Maktab derives both from `Session_Info`.

## 3. Payment Screen Requirements

For every payable course transaction, display:

- Payment code.
- Maktab transaction ID.
- Current amount due, from the transaction API.
- A `Use a new card / bank account` action.
- Saved cards when at least one active card exists.
- A `Save this card for future payments` checkbox only on the new-card checkout path.

The save-card checkbox must:

- Default to `false`.
- Be visible only to authenticated parents.
- Be disabled for ACH or bank-debit selection if the product UX cannot guarantee a card payment. Card storage is only performed from card transaction data.
- Clearly explain that Maktab stores a secure payment-provider token, not the card number.

Suggested wording:

> Save this card securely for future Maktab payments. You can remove it at any time.

## 4. New Card / ACH Checkout: Existing HelcimPay.js Flow

### 4.1 Initialize payment

Call Maktab before loading or submitting HelcimPay.js.

```http
POST /api/helcim/initialize-payment
```

Example request for a new card that should be eligible to save:

```json
{
  "paymentCode": "PAY001",
  "transactionId": "11111111-1111-1111-1111-111111111111",
  "amount": 150.00,
  "userIp": "203.0.113.10",
  "saveCardInfo": true
}
```

Example response:

```json
{
  "checkoutToken": "helcim_checkout_token"
}
```

Rules:

- `paymentCode`, `transactionId`, and `amount` must describe the course transaction currently being paid.
- The portal must not generate an invoice number. Maktab generates it.
- Set `saveCardInfo` to `true` only when the parent explicitly opted in.
- Do not reuse a checkout token after a completed, cancelled, or expired hosted checkout.

### 4.2 Submit through HelcimPay.js

Use the existing Helcim JavaScript integration and pass the returned `checkoutToken` exactly as currently implemented. Stripe-style client secrets are not used here.

Helcim hosts the payment form. The Parent Portal must not add custom fields for card number, CVV, or expiry.

### 4.3 Notify Maktab of browser completion

After HelcimPay.js returns its completion data, send the unmodified response object to Maktab.

```http
POST /api/helcim/complete-payment
```

```json
{
  "rawDataResponse": {
    "transactionId": 53977185,
    "invoiceNumber": "INV-PAY001-202609151200-1"
  }
}
```

Typical response:

```json
{
  "success": true,
  "duplicate": false,
  "invoiceId": 70203966,
  "transactionId": 53977185,
  "invoiceNumber": "INV-PAY001-202609151200-1",
  "paymentFlow": "card"
}
```

`duplicate: true` is safe and means the same provider transaction was already processed. Do not display an error or create another payment attempt.

### 4.4 Parent-facing completion behavior

1. Display `Payment submitted. Confirming with Helcim...` immediately after hosted checkout succeeds.
2. Refresh the course transaction/payment data after the completion response.
3. If payment status is not yet updated, poll the existing transaction endpoint with a bounded retry, for example every 3 to 5 seconds for up to 60 seconds.
4. If still pending, show `Payment is being confirmed. Do not submit another payment.`
5. Refresh on next page load. Admin invoice sync and Helcim webhook processing may complete later.

If `saveCardInfo` was true, do not assume the card appears instantly. Refresh saved cards after payment confirmation. Maktab saves the encrypted Helcim token only when an approved card transaction is received through webhook or sync.

## 5. Saved Cards

### 5.1 Get active cards

```http
GET /api/helcim/saved-cards
```

Example response:

```json
[
  {
    "cardId": "22222222-2222-2222-2222-222222222222",
    "cardCompany": "Visa",
    "cardFundingType": "Credit",
    "lastFourDigits": "4242",
    "cardHolderName": "Test Parent",
    "expiryMonth": null,
    "expiryYear": null,
    "isDefault": true,
    "createdAt": "2026-09-15T18:20:00Z"
  }
]
```

Display only the returned masked data. `expiryMonth` and `expiryYear` can be `null`; Helcim transaction responses do not reliably provide expiry data.

Suggested label:

```text
Visa Credit ending in 4242 (Default)
```

Do not infer card type from the last four digits. Use `cardCompany` and `cardFundingType` from the API.

### 5.2 Make a card default

```http
PUT /api/helcim/saved-cards/{cardId}/default
```

Success response: `204 No Content`.

On success, refresh `GET /api/helcim/saved-cards`. Do not optimistically assume the default changed if the request failed.

### 5.3 Delete a saved card

```http
DELETE /api/helcim/saved-cards/{cardId}
```

Success response: `204 No Content`.

The card is deactivated, not physically removed from payment audit history. Remove it from the visible list after success.

For safety, ask for confirmation before deletion. If it is the default card, the portal should simply refresh the list after deletion; Maktab does not promise another card becomes default automatically.

## 6. Saved Card Payment

### 6.1 Start a deliberate payment attempt

Generate one UUID in the FE for each deliberate click on `Pay`. This is the idempotency key. Keep the same key only when retrying the same request after a timeout or network failure.

```javascript
const idempotencyKey = crypto.randomUUID();
```

Never reuse this key for a different card, amount, or course transaction.

### 6.2 Charge selected saved card

```http
POST /api/helcim/saved-cards/charge
```

Example request:

```json
{
  "cardId": "22222222-2222-2222-2222-222222222222",
  "paymentCode": "PAY001",
  "transactionId": "11111111-1111-1111-1111-111111111111",
  "amount": 150.00,
  "userIp": "203.0.113.10",
  "idempotencyKey": "33333333-3333-3333-3333-333333333333"
}
```

Maktab validates all of the following before calling Helcim:

- The active session has a non-empty user and family.
- The card belongs to that user and is active.
- The payment code resolves to an active course transaction.
- The supplied transaction ID matches the payment code.
- The transaction belongs to the active family.
- The amount is positive and does not exceed the remaining payable amount.
- The idempotency key has not already created a different attempt.

### 6.3 Success response: not yet final

```json
{
  "paymentAttemptId": "44444444-4444-4444-4444-444444444444",
  "status": "awaiting_confirmation",
  "acceptedByHelcim": true,
  "awaitingConfirmation": true,
  "helcimTransactionId": 9001,
  "invoiceNumber": "INV-PAY001-202609151200-1",
  "error": null
}
```

This response means Helcim accepted the charge request. It does **not** mean the course ledger is final. The UI must:

1. Disable the Pay button.
2. Persist `paymentAttemptId`, `invoiceNumber`, and idempotency key in current page state.
3. Show `Payment submitted. Confirming with Helcim...`.
4. Refresh the course transaction/payment status using the existing transaction API.
5. Show success only when the course transaction reflects the payment or the page is refreshed with confirmed payment data.

### 6.4 Idempotent retry behavior

If the browser loses the response or times out, retry the same request with the exact same idempotency key. Maktab returns the existing attempt and does not send another Helcim purchase request.

Possible returned statuses:

| Status | Parent Portal behavior |
| --- | --- |
| `created` | Rare intermediate state. Keep payment disabled and retry transaction refresh. |
| `submitted` | Keep payment disabled and wait for confirmation. |
| `awaiting_confirmation` | Do not charge again. Wait for webhook/sync and refresh transaction data. |
| `confirmed` | Refresh payment data and show paid success. |
| `declined` | Re-enable payment. Show error and let parent choose another saved card or new checkout. |
| `failed` | Re-enable payment after showing error. A new deliberate payment must use a new idempotency key. |

### 6.5 Error responses

| HTTP status | API code | Parent behavior |
| --- | --- | --- |
| `400` | `invalid_saved_card_payment_request` | Correct missing/invalid fields. Do not retry automatically. |
| `400` | `saved_card_payment_not_allowed` | Show API error. Common causes: amount exceeds due, wrong family/transaction, card vault disabled. |
| `400` | `helcim_payment_rejected` | Card/payment was rejected by Helcim. Let parent select another card or new checkout. |
| `404` | `saved_card_or_transaction_not_found` | Refresh cards and transaction; do not retry that card automatically. |
| `502` | `helcim_unavailable` | Preserve the idempotency key. Offer a retry with the same key after a short delay. |

The response error object has this shape:

```json
{
  "error": "The selected saved card is unavailable.",
  "code": "saved_card_or_transaction_not_found"
}
```

## 7. Parent Portal State Machine

```text
Idle
  -> NewCardInitializing
  -> HelcimHostedCheckout
  -> AwaitingProviderConfirmation
  -> Confirmed | Failed | Cancelled

Idle
  -> SavedCardSubmitting
  -> AwaitingProviderConfirmation
  -> Confirmed | Declined | Failed
```

Rules:

- Do not permit multiple active payment submissions for the same visible transaction.
- Do not treat a Helcim browser callback alone as final ledger success.
- Do not retry a saved-card request with a new idempotency key unless the prior attempt definitively failed or the parent deliberately starts a new payment.
- If a pending attempt survives a page refresh, reload transaction payment data before exposing another pay action.

## 8. Parent Acceptance Checklist

- New Helcim checkout works unchanged when `saveCardInfo` is false.
- Opt-in new card checkout creates one saved card after approved webhook/sync.
- Saved cards display only masked data.
- Parent can set default and delete only their own cards.
- Saved-card payment shows `awaiting_confirmation` before ledger confirmation.
- Repeated request with same idempotency key produces no duplicate provider charge.
- Declined, unavailable, invalid, and Helcim unavailable errors have distinct UI behavior.
