# Parent Portal: Current Development Changes

## Purpose

This guide describes the parent-portal changes delivered in the current Development build. It covers MFA, child educational profiles, attendance viewing, family-member labels, enrollment behavior, and the updated notification language.

The portal must use the current `MaktabDataContracts` Development package. Do not hard-code the previous role values or the previous child-only wording.

## Authentication And MFA

### Functional behavior

The login response can indicate that email MFA must be completed before the session is fully verified.

Normal parents receive an MFA code valid for 30 minutes. The expiry timestamp is returned by the API and must be treated as the source of truth. The parent portal does not offer a duration selector.

### Login flow

1. Call `POST /api/users/session/login` with the existing username and password payload.
2. Store the returned access token and session ID using the portal's existing secure session approach.
3. If `requiresTwoFactorVerification` is `false`, continue to the portal as today.
4. If `requiresTwoFactorVerification` is `true`, route to the MFA-code screen.
5. Render the returned `twoFactorCodeExpiresOn` as an absolute expiry or countdown. Do not assume a fixed duration in JavaScript.
6. Submit the email code to `POST /api/users/session/{sessionId}/verify-2fa`.
7. If the code expires or the user requests another code, call `POST /api/users/session/{sessionId}/resend-2fa` and replace the displayed expiry with the expiry returned from that response.

Example login response handling:

```ts
const login = await api.post<LoginResponse>("/api/users/session/login", credentials);

if (login.requiresTwoFactorVerification) {
  navigate("/verify-login", {
    state: {
      sessionId: login.sessionId,
      expiresOn: login.twoFactorCodeExpiresOn,
    },
  });
  return;
}

navigate("/dashboard");
```

### MFA UX rules

- Show an expired-code state when the API reports expiry or rejects verification.
- Let the backend enforce retry limits; do not implement a client-only attempt counter.
- Do not call family, payments, enrollment, or profile APIs until the existing API authorization flow accepts the verified session.
- Do not display whether a user is an administrator in the parent portal to explain an MFA duration. That is an authorization concern, not a parent UI feature.

## Family Member Display Labels

### Contract fields

`ChildResponse` now includes both persisted `userType` and view-only `displayType`.

```ts
enum UserType {
  Child = 0,
  Self = 1,
}

enum FamilyMemberDisplayType {
  Child = 0,
  Self = 1,
  Spouse = 2,
  OtherAdult = 3,
}
```

`UserType` now has only two valid values: `Child` and `Self`. Do not send, render, or persist legacy `UserType` values `2`, `3`, or `4`. Mother, Father, and Guardian remain relationship concepts supplied by the family/user relationship data, not child-record types.

### Required rendering behavior

Use `displayType` for labels in the logged-in family member's view. It is calculated relative to the authenticated user.

- `Self`: show `Self`.
- `Spouse`: show `Spouse`.
- `Child`: show the existing child presentation.
- `OtherAdult`: show a neutral adult/family-member label.

Do not overwrite `userType` from the UI. Both spouses can be stored as `Self`; the API returns `displayType = Spouse` when one spouse is viewing the other. The actual Mother/Father/Guardian relationship remains in `user_info`. This avoids corrupting persisted relationship data merely to alter a label.

Example:

```ts
const label = {
  0: "Child",
  1: "Self",
  2: "Spouse",
  3: "Other adult",
}[member.displayType] ?? "Family member";
```

## Child Educational Profile And Surah Status

### Deployment prerequisite

Before this API version is deployed, the backend team must run `Application.Users.Repository.Implementation/Scripts/CreateChildEducationalProfileSurahAssessments.sql` in the target database. Do not use the retired profile-wide `SurahCompletionStatus` migration.

### Use case

A parent adds or corrects Quran/Surah information for a child in their own family. Each selected Surah has its own completion state and remarks.

### Endpoints

```text
GET /api/children/{childId}/educational-profile
PUT /api/children/{childId}/educational-profile
GET /api/quran/surah-options
```

The normal authenticated parent is authorized only for a child belonging to the parent’s family. The server validates this. Never offer a child selector populated from another family.

### Contract

```ts
enum SurahCompletionStatus {
  Incomplete = 0,
  PartiallyCompleted = 1,
  Completed = 2,
}

type QuranSurahAssessmentRequest = {
  surah: number;
  completionStatus: SurahCompletionStatus;
  remarks: string;
};

type UpsertChildEducationalProfileRequest = {
  familyId: string;
  surahAssessments: QuranSurahAssessmentRequest[];
};
```

Example save request:

```json
{
  "familyId": "a1111111-1111-1111-1111-111111111111",
  "surahAssessments": [
    { "surah": 1, "completionStatus": 2, "remarks": "Can recite independently." },
    { "surah": 112, "completionStatus": 1, "remarks": "Revision is still needed." }
  ]
}
```

### UI rules

1. Load the Surah catalog from `GET /api/quran/surah-options`; do not maintain a separate client list.
2. Render one assessment row per selected Surah. Each row has its own required status selector and remarks textarea.
3. Store numeric enum values exactly as returned. Do not submit duplicate Surah IDs.
4. Limit each assessment's `remarks` to 500 characters in the UI and show a counter. The server rejects longer text.
5. Render the response assessment `isActive`, `createdAt`, and `updatedOn` as read-only information if useful; they are not editable fields.
6. Handle `403` by disabling editing and explaining that the Surah catalog has already been provided and can now only be changed by school staff.

### Important locking behavior

After `hasSurahCatalogBeenProvided` becomes true for the child, a normal parent cannot update the educational profile. Do not present this as a failed save or silently retry. Show a read-only view and direct the parent to school staff for a correction.

## Attendance View

### Use case

A parent views records and summary reports only for their own family.

### Endpoints

```text
POST /api/attendance/families/{familyId}/records
POST /api/attendance/families/{familyId}/report
```

The route family ID must be the family ID in the active session. Do not allow the parent to type or substitute an arbitrary family ID.

Typical records request:

```json
{
  "childId": "b2222222-2222-2222-2222-222222222222",
  "startDate": "2026-09-01T00:00:00Z",
  "endDate": "2026-09-30T00:00:00Z",
  "filter": 0
}
```

Use the API’s enum values for filter and grouping options from the shared contract. Treat dates as date-only in the UI; the backend normalizes attendance days.

### Parent UX rules

- Attendance is read-only in the parent portal.
- Clearly show present, absent, late arrival, early pickup, and notes when returned.
- Do not infer an absence from a missing record. Staff attendance screens default unsaved students to `Present`; parent records should display only returned persisted history.

## Enrollment And Payment Behavior

### Re-enrollment

When all enrollments for a family/course were cancelled or soft-deleted and the family enrolls again, the backend reuses the existing family-course transaction and payment code. The parent portal must not generate a payment code or assume every new enrollment submission produces a new transaction ID.

After enrollment, reload the transaction and enrollment state from the API rather than retaining a cached cancelled transaction view.

### Notification copy

Generated notices now use a neutral greeting and participant wording. This is server-rendered email content; the parent portal has no email-template editor to implement.

Expected cancellation wording:

```text
Assalaamu alaikum,
The participant's registration has been cancelled.

Assalaamu alaikum,
L'inscription du participant a été annulée.
```

Do not reproduce the removed unpaid-fee explanation in portal notifications unless a future product requirement explicitly restores it.

## Shared API Requirements

Use the standard authentication headers already used by the portal:

```http
Authorization: Bearer <access-token>
Session_Info: <session-id>
Content-Type: application/json
```

Handle API responses consistently:

- `400`: show the validation message, such as invalid Surah state or remarks longer than 500 characters.
- `401`: clear the client session and return to sign-in.
- `403`: show an access-boundary message; do not attempt to bypass it by changing IDs in the request.
- `404`: the selected child/profile no longer exists or is unavailable; refresh the family data.

## Parent Portal Acceptance Checklist

- A normal parent sees an MFA expiry based on the API response and can resend a code.
- Family members render `Self` and `Spouse` through `displayType`, without mutating `userType`.
- A parent can save a profile for their own child with an independent status and up to 500 remarks characters for each Surah.
- A parent receives a read-only/forbidden state after the Surah catalog is marked provided.
- A parent can view their family’s attendance but cannot submit attendance changes.
- Re-enrollment refreshes and displays the existing payment transaction rather than creating a client-side replacement.
