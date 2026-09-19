# Donation Campaigns: FE and BA Implementation Specification

## 1. Status and Scope

This is the agreed implementation specification for Maktab donation campaigns.

**Important: the Maktab donation-campaign backend module is not implemented yet.** The current system has Zeffy donation data, but that is a separate integration and must not be treated as the new Maktab/Helcim campaign API.

Do not build production FE calls to the proposed campaign routes until the campaign backend, contracts, schema, authentication rules, Helcim metadata handling, webhook handling, and tests are delivered.

The target feature supports:

- Mosque or school-owned donation campaigns.
- Public anonymous donations and authenticated donations.
- Preset donation amounts plus an optional custom amount.
- Helcim hosted checkout for a new card/bank payment.
- Future saved-card donation support only for authenticated users.
- Provider-confirmed donation payment finalization through webhook or admin sync.

## 2. Domain Model

### 2.1 Ownership

Campaigns belong to an institute. An institute can be a school or mosque through the existing `InstituteType` field.

```text
Institute (School or Mosque)
  -> DonationCampaign
      -> DonationCampaignPresetAmount
      -> DonationPayment
```

Use the existing `institutes` table and `InstituteType`; do not create a second mosque ownership model.

### 2.2 Campaign fields required from backend

The future campaign response should contain at least:

```json
{
  "campaignId": "guid",
  "instituteId": "guid",
  "title": "Support the Mosque",
  "titleFr": "Soutenez la mosquee",
  "description": "Donation description",
  "descriptionFr": "Description francaise",
  "imageUrl": null,
  "goalAmount": 10000.00,
  "totalRaisedAmount": 2250.00,
  "currency": "CAD",
  "startsAt": "2026-09-01T00:00:00Z",
  "endsAt": null,
  "isActive": true,
  "isPublic": true,
  "allowCustomAmount": true,
  "presets": [
    { "presetId": "guid", "amount": 5.00, "displayOrder": 1, "isCustomAmountOption": false },
    { "presetId": "guid", "amount": 10.00, "displayOrder": 2, "isCustomAmountOption": false },
    { "presetId": "guid", "amount": 25.00, "displayOrder": 3, "isCustomAmountOption": false },
    { "presetId": "guid", "amount": 100.00, "displayOrder": 4, "isCustomAmountOption": false },
    { "presetId": "guid", "amount": 0.00, "displayOrder": 5, "isCustomAmountOption": true }
  ]
}
```

`amount: 0` is not a zero-dollar donation. It is the explicit `Custom` UI option and is valid only when `isCustomAmountOption: true` and `allowCustomAmount: true`.

### 2.3 Donation payment fields required from backend

The future normalized payment response should include:

- Donation payment ID.
- Campaign ID.
- Nullable User ID.
- Anonymous flag or display donor name policy.
- Amount and currency.
- Payment provider and provider transaction/invoice IDs.
- Payment status.
- Card company, funding type, last four, and cardholder name only where provider returns them.
- Linked saved card ID only when an authenticated saved card was used.
- Created/confirmed/refunded timestamps.

Do not expose provider token, encrypted token fields, full card number, CVV, encryption keys, or raw webhook secrets.

## 3. Roles and Application Responsibilities

| App | Responsibilities |
| --- | --- |
| Public/Parent Portal | Browse public campaigns, select amount, complete donation, optionally identify authenticated donor, display confirmation. |
| Parent Portal | May use a saved card only when authenticated and when campaign saved-card support is delivered. |
| Admin Portal | Create/manage campaigns and presets, view donation payments, trigger safe provider sync, process refunds through approved operations. |
| Backend | Validates campaign status/amount, owns identity, creates provider metadata, processes webhook/sync exactly once, calculates totals. |

An anonymous API key must only permit public campaign read, donation initialization, and donation status access. It must not grant course, user, saved-card, refund, or admin access.

## 4. Proposed Campaign APIs (Not Yet Implemented)

### 4.1 Public reads

```http
GET /api/donation-campaigns/public
GET /api/donation-campaigns/public/{campaignId}
```

Only return active, public, in-date campaigns. Do not return admin-only fields or donor data.

### 4.2 Admin campaign CRUD

```http
GET    /api/donation-campaigns
GET    /api/donation-campaigns/{campaignId}
POST   /api/donation-campaigns
PUT    /api/donation-campaigns/{campaignId}
DELETE /api/donation-campaigns/{campaignId}
```

Admin request example:

```json
{
  "instituteId": "guid",
  "title": "Winter Relief Fund",
  "titleFr": "Fonds d'aide hivernale",
  "description": "Help families in need.",
  "descriptionFr": "Aidez les familles dans le besoin.",
  "goalAmount": 10000.00,
  "currency": "CAD",
  "startsAt": "2026-11-01T00:00:00Z",
  "endsAt": "2026-12-31T23:59:59Z",
  "isActive": true,
  "isPublic": true,
  "allowCustomAmount": true
}
```

### 4.3 Admin preset CRUD

```http
GET    /api/donation-campaigns/{campaignId}/presets
POST   /api/donation-campaigns/{campaignId}/presets
PUT    /api/donation-campaigns/{campaignId}/presets/{presetId}
DELETE /api/donation-campaigns/{campaignId}/presets/{presetId}
```

Preset request example:

```json
{
  "amount": 25.00,
  "displayOrder": 3,
  "isCustomAmountOption": false,
  "isActive": true
}
```

Custom option request:

```json
{
  "amount": 0.00,
  "displayOrder": 5,
  "isCustomAmountOption": true,
  "isActive": true
}
```

Backend validation must reject a custom option when campaign `allowCustomAmount` is false, reject negative/zero standard presets, and prevent more than one active custom option per campaign.

## 5. Donation Checkout Contract (Not Yet Implemented)

### 5.1 New card/ACH donation initialization

The future Helcim initialize request extends the existing payment request without breaking course payments:

```json
{
  "amount": 25.00,
  "userIp": "203.0.113.10",
  "isDonation": true,
  "campaignId": "guid",
  "transactionId": null,
  "paymentCode": null,
  "saveCardInfo": false
}
```

Rules:

- `isDonation: false` or omitted means normal course payment behavior.
- `isDonation: true` requires a valid active campaign ID and must not require a course transaction/payment code.
- Backend derives `userId` from session when authenticated. It does not accept a donor User ID from FE.
- Anonymous calls have no user session and persist `UserId = NULL` in donation payment records.
- The backend must calculate campaign eligibility and validate the selected amount; FE validation is only UX.

### 5.2 Required Helcim metadata

For donation checkout, backend must send provider metadata that can be recovered in webhook and manual sync:

```json
{
  "isDonation": true,
  "campaignId": "guid",
  "userId": "guid-or-null"
}
```

This metadata is created server-side. FE must not generate or alter it. Existing course checkout metadata continues to identify course transaction/payment code when `isDonation` is false.

### 5.3 Provider confirmation

The donation success page must show `Donation submitted. Confirming payment...` until webhook or sync persists the donation payment.

Do not increase campaign totals from browser success. The backend increments totals only after provider-confirmed processing.

## 6. Public/Parent Campaign UI

### 6.1 Campaign list/detail

Display:

- Campaign title, description, institute/mosque, goal, raised amount, progress, and end date.
- Preset buttons in `displayOrder`.
- Custom amount input only when `allowCustomAmount` is true and an active custom preset exists.
- Closed/inactive state without a donation action.

### 6.2 Amount selection rules

1. Selecting a standard preset uses its exact positive amount.
2. Selecting Custom clears prior preset selection and displays a currency-formatted numeric input.
3. Reject zero, negative, non-numeric, and unsupported decimal precision before submission.
4. Backend remains authoritative for minimum/maximum rules once defined.
5. Persist selected amount only in temporary checkout state; do not persist card/payment data in browser storage.

### 6.3 Anonymous versus authenticated donation

Anonymous visitor:

- Uses public campaign API-key authorization only.
- Can complete a new hosted checkout.
- Cannot see or use saved cards.
- Is stored as anonymous when no authenticated session exists.

Authenticated parent:

- Uses normal JWT/session.
- Backend links donation to session User ID.
- May later use saved-card donation only after the dedicated saved-card donation contract is released.

## 7. Admin Campaign UI

### 7.1 Campaign management

Admin form needs:

- Institute/mosque selector.
- English/French title and description.
- Active/public switches.
- Start/end dates.
- Goal amount and currency.
- Allow custom amount switch.
- Preset amount grid with add/edit/delete/reorder.

Prevent admins from deleting campaigns with provider-confirmed donations. Prefer deactivation/archiving to preserve audit history.

### 7.2 Donation payments/reporting

Admin donation list should filter by campaign, institute, date range, provider status, payment source, anonymous/authenticated donor, and refund status.

Use provider-confirmed statuses only for financial totals. Pending browser/provider submissions must not be counted as raised funds.

## 8. Required Backend Work Before FE Integration

The following is not delivered today and must be implemented before FE activates campaign screens:

- Campaign, preset, and donation-payment data contracts.
- Database schema and migration.
- Campaign/preset CRUD repositories, services, controllers, and role authorization.
- Public read endpoints with API-key-only policy and rate limiting.
- `IsDonation`, nullable course transaction/payment code, and `CampaignId` support in Helcim contracts.
- Server-created provider metadata and webhook/manual-sync donation routing.
- Donation payment exact-once persistence and campaign-total calculation.
- Anonymous donor handling with nullable User ID.
- Donation refund workflow and reporting.
- Tests for campaign eligibility, presets, anonymous/authenticated checkout, webhook retries, sync, idempotency, totals, and refunds.

## 9. BA Acceptance Criteria

- Campaigns are owned by schools or mosques through the existing institute model.
- Staff can manage active/public state and preset amounts.
- `$0` preset means Custom only, never a donation amount.
- Anonymous donations are supported without creating a user account.
- Authenticated donations link to the session user, not a FE-supplied user ID.
- Campaign totals use confirmed provider payments only.
- Refunds reduce/adjust totals through provider-confirmed records.
- Course payments and campaign donations remain separate financial domains.
