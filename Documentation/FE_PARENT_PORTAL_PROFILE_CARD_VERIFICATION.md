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

## Server-Side Ownership and Correlation

FE does not identify the owner of a card. On every call to `initialize-verification`, the backend:

1. Validates the parent JWT and reads `Session_Info`.
2. Resolves the active session to its server-owned `UserId` and `FamilyId`.
3. Creates a unique internal reference in the form `INV-CARD-VERIFY-<timestamp>-<guid>`.
4. Stores that reference with the resolved `UserId`, `FamilyId`, and the intent to save a card in `helcim_checkout_context`.
5. Sends the reference to Helcim as server-owned checkout metadata.
6. When Helcim webhook processing or authorized invoice sync returns an approved verification, looks up the context by the internal reference.
7. Encrypts and associates the returned reusable token with the same `UserId` and `FamilyId` in `helcim_saved_card`.

This means a caller cannot attach a card to another user by editing browser JSON. `userId`, `familyId`, invoice reference, token, card number, CVV, expiry, and Helcim API key are never valid fields in the parent request.

## Exact FE Requests

### Add a profile card

```ts
const result = await fetch("/api/helcim/saved-cards/initialize-verification", {
  method: "POST",
  headers: {
    Authorization: `Bearer ${accessToken}`,
    Session_Info: activeSessionId,
    "Content-Type": "application/json"
  },
  body: "{}"
});

if (!result.ok) {
  throw await result.json();
}

const { checkoutToken } = await result.json();
```

The response is only a checkout-session token:

```json
{
  "checkoutToken": "..."
}
```

### List cards after verification

```ts
const cards = await fetch("/api/helcim/saved-cards", {
  headers: {
    Authorization: `Bearer ${accessToken}`,
    Session_Info: activeSessionId
  }
}).then(response => response.json());
```

Typical safe display response item:

```json
{
  "cardId": "0f7b2ec0-16b4-47ea-843d-2b346a1a5fb4",
  "cardCompany": "Visa",
  "cardFundingType": "Credit",
  "lastFourDigits": "4242",
  "cardHolderName": "Parent Name",
  "isDefault": true,
  "createdAt": "2026-09-16T03:20:00Z"
}
```

## HelcimPay.js Handoff

The Maktab API does not receive card input. FE must use the same approved HelcimPay.js modal/iframe integration used for normal Helcim checkout and provide only the received checkout token.

Required FE behavior:

1. Disable the Add Card button while the initialize request is pending.
2. On `200`, open HelcimPay.js immediately with `checkoutToken`.
3. Do not expose, log, cache, or append the token to a URL.
4. If HelcimPay.js reports a cancellation, close the modal and leave the saved-card list unchanged.
5. If HelcimPay.js reports a decline/error, show its safe message; do not retry automatically.
6. If HelcimPay.js reports submission/approval, close the modal, show a pending confirmation notice, and begin bounded refresh of the saved-card list.

The HelcimPay.js browser event is not the authoritative source for saving the card. Only backend webhook handling or authorized invoice sync may make a card appear in `GET /api/helcim/saved-cards`.

## UI State Model

| State | Trigger | UI behavior |
| --- | --- | --- |
| `idle` | Payment Methods opens | Show saved cards and Add Card action. |
| `initializing` | Add Card clicked | Disable duplicate clicks; show spinner. |
| `helcim_modal_open` | Checkout token received | Render HelcimPay.js. |
| `cancelled` | Parent closes/cancels Helcim modal | Return to `idle`; no refresh required. |
| `verification_rejected` | Helcim rejection/error | Show safe error; allow a new user-initiated attempt. |
| `awaiting_confirmation` | Helcim browser success event | Show pending message and refresh cards. |
| `saved` | Card appears in card list | Render new card; return to `idle`. |
| `confirmation_delayed` | Bounded refresh ends without card | Show non-error informational message and a manual Refresh action. |

Use at most six refreshes at roughly three-second intervals. Do not indefinitely poll and do not treat a timeout as a failed verification; webhook processing may be delayed.

## Default Card Rules

- The backend marks the first active profile card as the default automatically.
- When more than one card exists, show a `Set as default` action that calls `PUT /api/helcim/saved-cards/{cardId}/default`.
- Refresh the full card list after `204 No Content`; do not optimistically assume another tab has not changed the default.
- Deleting a card calls `DELETE /api/helcim/saved-cards/{cardId}`. A deleted card must disappear from the active list and must never be selectable for a new saved-card charge.
- If the parent has no saved cards after delete, show the Add Card action.

## Use-Case Examples

### A. Parent adds the first card before enrollment payment

The parent selects Add Card from Profile. FE sends `{}` to `initialize-verification`, displays HelcimPay.js, and waits for the card to appear. The card is automatically marked default. No `student_course_transaction`, `course_payment`, or money movement is created.

### B. Parent pays a course and also opts to remember the new card

This is not the profile endpoint. FE uses `POST /api/helcim/initialize-payment` with the real course `paymentCode`, `transactionId`, and `amount`, plus `saveCardInfo: true`. The charge is a normal positive course payment; after provider confirmation, the backend applies one course payment and saves the card.

### C. Parent pays with an existing profile card

FE first loads `GET /api/helcim/saved-cards`, lets the parent select `cardId`, then calls `POST /api/helcim/saved-cards/charge` with the real course payment details and a fresh idempotency key. This is a positive charge and returns `awaiting_confirmation`; it is never an add-card call.

### D. Parent is not signed in

Do not show the profile Add Card action. The verification endpoint requires a valid parent session and must not be called with an API key, an empty session, or a supplied `userId`.

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
