# Parent Portal: Add a Card to Profile

## Status

This document describes the dedicated authenticated profile-card flow. It is separate from a course payment and from `POST /api/helcim/saved-cards/charge`.

Route:

```http
POST /api/helcim/saved-cards/initialize-verification
```

The route requires the normal parent JWT and `Session_Info` header. It does not accept anonymous or API-key access.

## Purpose

Use this route when a signed-in parent selects **Add a card** in Profile or Payment Methods before they are paying for a course.

The backend creates a HelcimPay.js checkout session using:

```json
{
  "paymentType": "verify",
  "amount": 0,
  "paymentMethod": "cc"
}
```

Helcim performs a zero-dollar card verification. No course payment, enrollment payment, charge, refund, or card token is created in the browser. The backend stores the returned Helcim token only after provider confirmation arrives through the signed webhook or an authorized invoice sync.

## Request and Response

### Request

There is no request body and no payment-specific fields:

```http
POST /api/helcim/saved-cards/initialize-verification
Authorization: Bearer <parent-jwt>
Session_Info: <active-session-guid>
```

If the HTTP client requires a body, send `{}` only. Do not send a `paymentCode`, `transactionId`, `amount`, `cardId`, `userId`, `familyId`, card number, expiry, CVV, or Helcim API token.

### Success response: `200 OK`

```json
{
  "checkoutToken": "helcim-one-time-checkout-token"
}
```

The checkout token is short-lived and is used only to display HelcimPay.js immediately. Do not store it in local storage, session storage, analytics, application logs, or URL query parameters.

### Error responses

| HTTP | Code | FE action |
| --- | --- | --- |
| `400` | `saved_card_verification_not_available` | Show a general unavailable message. This usually means vault configuration is disabled or invalid. |
| `400` | `helcim_verification_rejected` | Show Helcim's safe error text and allow a new attempt. |
| `401`/`403` | Normal authentication failure | Ask the parent to sign in again. |
| `502` | `helcim_unavailable` | Show a retry-later message. Do not retry automatically in a tight loop. |

## Parent Flow

1. Parent opens **Profile > Payment methods**.
2. FE loads existing cards using `GET /api/helcim/saved-cards`.
3. Parent selects **Add a card**.
4. FE calls `POST /api/helcim/saved-cards/initialize-verification`.
5. FE passes the returned `checkoutToken` to HelcimPay.js; card fields remain inside Helcim's iframe/modal.
6. After HelcimPay.js reports success, FE displays `Card verification submitted. Your payment method will appear shortly.` Do not claim that the card is saved yet.
7. Webhook processing or an authorized admin invoice sync receives Helcim's approved result and persists the encrypted token plus safe display metadata.
8. FE polls `GET /api/helcim/saved-cards` for a short bounded period, or refreshes when the user returns to Payment methods.
9. Display the new card once it appears. The first active card is marked default by backend behavior.

## TypeScript Example

```ts
type VerificationResponse = { checkoutToken: string };

async function beginAddCard(): Promise<void> {
  const response = await api.post<VerificationResponse>(
    "/api/helcim/saved-cards/initialize-verification",
    {}
  );

  // Use the existing HelcimPay.js integration. Do not call Helcim initialize from FE.
  window.appendHelcimPayIframeToken(response.checkoutToken);
}

async function refreshCardsUntilSaved(): Promise<void> {
  for (let attempt = 0; attempt < 6; attempt += 1) {
    await new Promise(resolve => setTimeout(resolve, 3000));
    const cards = await api.get<SavedCard[]>("/api/helcim/saved-cards");
    if (cards.some(card => card.isDefault) || cards.length > 0) {
      setCards(cards);
      return;
    }
  }

  showInfo("Verification was submitted. Refresh Payment methods in a few minutes if the card is not visible.");
}
```

The exact HelcimPay.js success-event function must follow the application's existing Helcim integration. The event is not final payment confirmation; server webhook/sync is authoritative.

## Payload Matrix

| Field | Profile add-card verification | Normal new-card course checkout | Existing saved-card course charge |
| --- | --- | --- | --- |
| `paymentCode` | Never send | Required | Required |
| `transactionId` | Never send | Required | Required |
| `amount` | Never send; backend uses `0` | Required positive amount | Required positive amount |
| `cardId` | Never send | Never send | Required: value from saved-card list |
| `saveCardInfo` | Never send; this endpoint always saves after approval | Optional; send `true` only when user opts in | Not applicable |
| `idempotencyKey` | Not applicable | Not applicable for current HelcimPay.js checkout contract | Required; a FE-generated UUID for one payment click |
| `userIp` | Never send | Optional only if existing checkout integration already supplies it | Current charge contract accepts it, but it should not be used as an identity field |
| `userId` / `familyId` | Never send | Never send | Never send |
| PAN, CVV, expiry, API key | Never send to Maktab API | Never send to Maktab API | Never send to Maktab API |

## Existing Saved-Card Charge Example

This endpoint is for paying a course balance with a card that already exists in the saved-card list:

```http
POST /api/helcim/saved-cards/charge
Authorization: Bearer <parent-jwt>
Session_Info: <active-session-guid>
Content-Type: application/json
```

```json
{
  "cardId": "0f7b2ec0-16b4-47ea-843d-2b346a1a5fb4",
  "paymentCode": "KU5MK8",
  "transactionId": "4fd4ddd4-8fbd-7a49-920c-fc843831132b",
  "amount": 150.00,
  "userIp": "",
  "idempotencyKey": "384a05d4-1d03-43b3-b2a4-5d4a3ed7bd4f"
}
```

The backend validates that the saved card belongs to the session user, the payment code and Maktab transaction match, and the requested amount is payable. It returns `awaiting_confirmation`; FE must wait for webhook/sync before showing payment as complete.

## Card Management

```http
GET    /api/helcim/saved-cards
PUT    /api/helcim/saved-cards/{cardId}/default
DELETE /api/helcim/saved-cards/{cardId}
```

Only show safe card information returned by `GET`, such as card company, funding type, last four digits, cardholder name, default flag, and created date. Deletion deactivates the local card; FE must refresh the list after a `204 No Content` response.

## Acceptance Tests

- Profile Add Card makes one backend call with no payment fields.
- FE never sends PAN/CVV, a Helcim API token, or a fake payment code.
- HelcimPay.js receives only the backend-issued checkout token.
- Successful browser event is shown as pending, not finalized.
- An approved webhook/sync creates exactly one saved card or safely ignores a duplicate callback.
- A declined or unavailable verification never appears as an active saved card.
- Saved-card charging uses a fresh idempotency key per user click and reuses it only to retry the same click.
