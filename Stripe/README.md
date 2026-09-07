# Stripe Integration Module

The Stripe module is provider-specific but application-independent. It does not reference Maktab courses, families, transactions, or database repositories.

## Supported operations

- Create and retrieve Stripe PaymentIntents.
- Create refunds by PaymentIntent or Charge.
- Verify Stripe webhook signatures from the unmodified request body.

## Integration requirements

- Amounts are supplied in the smallest currency unit, such as cents for CAD/USD.
- Each create/refund request requires a caller-supplied idempotency key.
- Create one PaymentIntent per order or payment session. Reuse that PaymentIntent if checkout resumes.
- The create response contains the safe `publishableKey` and `clientSecret` needed by Stripe.js. Never expose `SecretKey` or `WebhookSigningSecret`.
- Store only non-sensitive correlation IDs in Stripe metadata.
- Verify the `Stripe-Signature` header against the raw request body before any fulfilment or ledger operation.
- Treat `processing` as non-final. Apply local payment effects only from a verified terminal Stripe event/status.

## Configuration

```json
"Stripe": {
  "Enabled": false,
  "SecretKey": "",
  "PublishableKey": "",
  "WebhookSigningSecret": ""
}
```

Secrets must come from deployment configuration or a secrets vault, not source control.
