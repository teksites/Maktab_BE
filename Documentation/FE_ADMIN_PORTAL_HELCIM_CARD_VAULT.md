# Admin Portal: Helcim Saved Card Operations and Sandbox Validation

## 1. Purpose and Access Boundary

This guide defines the Admin Portal responsibilities for Helcim saved-card support.

The Admin Portal does **not** collect card details, receive Helcim tokens, list a parent's saved cards, select a parent's card, or charge a saved card. Those actions belong only to the authenticated Parent Portal and the backend.

Admin responsibilities are:

- Review payment and transaction status through existing transaction views.
- Trigger invoice sync when Helcim has processed a payment but webhook delivery is delayed or missing.
- Run reconciliation for controlled support and operational recovery.
- Diagnose whether a saved-card attempt is awaiting confirmation, confirmed, declined, or failed.
- Validate the sandbox release flow before enabling production.

## 2. Required Admin Authorization

All administrative Helcim endpoints require normal authentication, `Session_Info`, and an authorized role:

- `Admin`
- `SuperUser`
- `SchoolAdmin`
- `SchoolSupervisor`

```http
Authorization: Bearer <JWT>
Session_Info: <admin-session-guid>
Content-Type: application/json
```

Do not expose admin sync/reconciliation operations in the Parent Portal.

## 3. What Admins Can See

The existing transaction details screen remains the operational source for Helcim activity. It should show at minimum:

- Maktab payment code and Maktab transaction ID.
- Helcim transaction ID and invoice number.
- Payment amount and currency.
- Payment source and card display information where available.
- Card company and funding type, for example `Visa / Debit`.
- Transaction type, for example purchase, refund, or reverse.
- Helcim transaction status and invoice status.
- Raw response only in privileged troubleshooting views; never expose raw token data to normal users.

For saved-card payment support, add an operational status display from `helcim_payment_attempt` if the Admin API/report already exposes it. The implementation status values are:

| Database status | Meaning | Admin action |
| --- | --- | --- |
| `0 Created` | Attempt reserved before provider request. | Wait briefly; investigate only if stale. |
| `1 Submitted` | Reserved/submitted intermediate state. | Wait or use invoice sync after provider result is known. |
| `2 ApprovedAwaitingConfirmation` | Helcim accepted the charge; course payment is not yet final. | Trigger invoice sync if webhook does not finalize promptly. |
| `3 Declined` | Helcim declined the saved-card charge. | Do not sync as a paid transaction. Parent must choose another method. |
| `4 Confirmed` | Webhook/sync applied the course payment exactly once. | No action. |
| `5 Failed` | Provider/request/infrastructure failure. | Review error and retry only through an intentional parent action. |

The Parent Portal receives user-facing strings such as `awaiting_confirmation`, `confirmed`, `declined`, and `failed`. Do not display the byte values directly to staff.

## 4. Manual Invoice Sync

Use invoice sync when Helcim shows an approved transaction but the Maktab payment status has not updated after a reasonable webhook delay.

```http
POST /api/helcim/byadmin/sync-invoice/{invoiceReference}
```

`invoiceReference` can be either:

- Helcim invoice number, for example `INV-PAY001-202609151200-1`.
- Helcim invoice ID, for example `70203966`.

The alias below is also available for invoice number use:

```http
POST /api/helcim/byadmin/sync-invoice-number/{invoiceReference}
```

Example response:

```json
{
  "success": true,
  "duplicate": false,
  "invoiceId": 70203966,
  "transactionId": 9001,
  "invoiceNumber": "INV-PAY001-202609151200-1",
  "paymentFlow": "card"
}
```

Interpretation:

| Field | Meaning |
| --- | --- |
| `success: true` | Helcim invoice/transaction was resolved and processed. |
| `duplicate: false` | New Maktab Helcim transaction details were stored. |
| `duplicate: true` | Transaction was already stored; this is safe and not an error. The ledger path is idempotent. |
| `paymentFlow: card` | Card transaction flow. |
| `paymentFlow: ach` | ACH transaction flow. |

For a saved-card purchase, invoice sync does all relevant backend work:

1. Retrieves invoice and card transaction from Helcim.
2. Applies one `course_payment` record using the external Helcim transaction ID as the idempotency marker.
3. Stores/updates normalized `helcim_transaction` information.
4. If the payment attempt is `ApprovedAwaitingConfirmation`, marks it `Confirmed` after course-payment application.
5. Does not create duplicate cards when the same webhook or sync is processed again.

### 4.1 Sync failure behavior

| HTTP status | Meaning | Admin action |
| --- | --- | --- |
| `200` | Sync completed or safely recognized a duplicate. | Refresh transaction view. |
| `400` | Invalid invoice reference or provider result cannot be processed. | Verify invoice number and Helcim dashboard status. |
| `404` | Maktab/Helcim invoice or transaction could not be resolved. | Verify tenant, payment code, invoice number, and provider environment. |
| `5xx` | Provider/API infrastructure issue. | Do not create a manual course payment solely from this error. Retry later with the same invoice reference. |

## 5. Reconciliation

Reconciliation is a support and recovery operation. It fetches Helcim card and ACH transactions over a date range and stores/applies eligible records idempotently.

### 5.1 Default reconciliation

```http
POST /api/helcim/byadmin/reconcile/run
```

The backend uses configured lookback settings.

### 5.2 Date-range reconciliation

```http
POST /api/helcim/byadmin/reconcile
```

Example request:

```json
{
  "startDate": "2026-09-15T00:00:00",
  "endDate": "2026-09-15T00:00:00"
}
```

Example response:

```json
{
  "startDate": "2026-09-15T00:00:00",
  "endDate": "2026-09-15T00:00:00",
  "alreadyRunning": false,
  "message": "Helcim reconciliation completed.",
  "cardTransactionsFetched": 3,
  "achTransactionsFetched": 0,
  "storedTransactions": 1,
  "appliedPayments": 1,
  "appliedRefunds": 0,
  "skippedDuplicates": 2,
  "skippedPendingAchTransactions": 0,
  "storedNonSettledAchTransactions": 0,
  "unmatchedTransactions": 0
}
```

Admin UI behavior:

- Disable the Run button while the request is in progress.
- If `alreadyRunning` is true, show that another reconciliation is in progress; do not queue another.
- Treat `skippedDuplicates` as expected behavior, not a failure.
- Investigate `unmatchedTransactions` rather than manually marking courses paid.
- Display counts and date range in the activity/audit view.

## 6. Refunds and Reversals

Saved-card purchases use the same Helcim transaction/refund model as regular card purchases once confirmed. Use the existing admin refund interfaces and endpoints; do not attempt to reverse a card by deleting the saved-card record.

Key rules:

- Deleting a saved card prevents future token charges; it does not refund past payments.
- A refund/reverse must reference the actual Helcim transaction.
- For card debit (`DB` / Interac), backend may return an error that refund must be completed in person with Helcim hardware. Show this as a non-retryable operational message.
- Refund/reverse results must be confirmed from Helcim and reflected through normal webhook/sync processing.
- Do not manually reduce `course_payment` data without a provider-backed refund/reverse record.

## 7. Sandbox Release Validation Script

Run this in `maktab_dev` only after the API is deployed and the App Service has:

```text
Helcim__CardVault__Enabled = true
Helcim__CardVault__EncryptionKey = <environment-specific Base64 32-byte key>
```

### 7.1 Create a test payable transaction

1. Create or select a family and a course transaction with an unpaid balance.
2. Record payment code, Maktab transaction ID, family, amount due, and expected course payment count.
3. Confirm the user is authenticated in the Parent Portal.

### 7.2 Save a card from regular checkout

1. In Parent Portal, choose new card checkout.
2. Select `Save this card for future payments`.
3. Complete Helcim sandbox card payment.
4. Wait for webhook handling. If it does not arrive, call admin invoice sync using the generated Helcim invoice number.
5. Confirm exactly one `helcim_saved_card` row is created for the parent and one course payment is applied.
6. Confirm repeat webhook/sync does not create a second saved card or course payment.

Verification SQL for privileged support only:

```sql
SELECT CardId, UserId, FamilyId, CardCompany, CardFundingType,
       LastFourDigits, IsDefault, IsActive, SourceHelcimTransactionId, CreatedAt
FROM helcim_saved_card
WHERE UserId = UNHEX('<dotnet-guid-byte-order-user-id>');
```

Never select or display `CardTokenCiphertext`, `CardTokenNonce`, `CardTokenTag`, or `CardTokenHash` in UI/support exports.

### 7.3 Validate saved-card charge

1. Create or select another unpaid course transaction for the same parent.
2. In Parent Portal, select the saved card and submit one payment.
3. Confirm the initial response is `awaiting_confirmation`.
4. Confirm exactly one `helcim_payment_attempt` row exists with the same idempotency key.
5. Wait for webhook, or use admin invoice sync with the returned invoice number.
6. Confirm attempt status becomes `Confirmed` and exactly one course payment exists for the Helcim transaction ID.
7. Retry the same FE request with the same idempotency key. Confirm no new Helcim purchase is created.

Privileged verification query:

```sql
SELECT PaymentAttemptId, PaymentCode, InvoiceNumber, Amount, Status,
       HelcimTransactionId, FailureReason, CreatedAt, UpdatedOn
FROM helcim_payment_attempt
WHERE InvoiceNumber = '<invoice-number>';
```

### 7.4 Validate operational failures

Perform and record these negative tests:

- Select a deleted card. Parent receives `404 saved_card_or_transaction_not_found`.
- Submit more than the remaining amount. Parent receives `400 saved_card_payment_not_allowed`.
- Use a Helcim decline test case. Attempt becomes `Declined`; no course payment is applied.
- Simulate a client timeout and retry with the same idempotency key. Verify no duplicate charge.
- Delay webhook and use admin invoice sync. Verify ledger confirmation occurs exactly once.

## 8. Production Enablement Checklist

1. Confirm `maktab` has `helcim_saved_card`, `helcim_payment_attempt`, and `helcim_checkout_context` tables. The current schema already contains them.
2. Deploy the tested API release to production.
3. Set a production-specific encryption key. Do not reuse the development key.
4. Verify Helcim production webhook URL and signature configuration.
5. Run one controlled internal payment/save-card test with a real authorized account and approved operational process.
6. Confirm webhook/sync, attempt confirmation, and reconciliation metrics.
7. Enable the Parent Portal saved-card UI gradually.

## 9. Support Rules

- Never request card numbers, CVVs, provider tokens, or encryption keys from parents.
- Never copy database encrypted-token fields into tickets, screenshots, logs, or exports.
- Never change an environment encryption key after cards are stored without an explicit key-rotation migration. Existing tokens would be unreadable.
- Use invoice sync/reconciliation for delayed provider confirmation; do not create manual ledger entries as a substitute for an unknown provider result.
- Record the Helcim invoice number and transaction ID in support tickets, not card data.
