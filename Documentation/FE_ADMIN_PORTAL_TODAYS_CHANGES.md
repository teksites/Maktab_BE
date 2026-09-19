# Admin Portal: Current Development Changes

## Purpose

This guide describes current Development changes affecting the administrative, school-admin, supervisor, teacher, and assistant portal experiences. It includes the revised role hierarchy, MFA behavior, participant terminology, Surah profile management, and individual attendance editing.

Use the current `MaktabDataContracts` Development package. Existing client code that serializes role values must be updated before the role-management UI is deployed.

## Role Hierarchy And Authorization

### Current role values

```ts
enum UserRoleType {
  None = 0,
  Normal = 1,
  Assistant = 2,
  SchoolTeacher = 4,
  SchoolSupervisor = 8,
  SchoolAdmin = 16,
  Admin = 32,
  SuperUser = 64,
}
```

Roles are flags. A user can hold multiple roles, so use bitwise checks rather than equality checks.

```ts
const hasRole = (roles: number, role: UserRoleType) => (roles & role) === role;
const canUseAdminArea = (roles: number) =>
  hasRole(roles, UserRoleType.Admin) || hasRole(roles, UserRoleType.SuperUser);
```

### Hierarchy

```text
Parent/Normal < Assistant < SchoolTeacher < SchoolSupervisor < SchoolAdmin < Admin < SuperUser
```

`Manager` has been removed. Do not display it in filters, assignment forms, role badges, or saved role values.

### UI authorization rules

- Treat `SuperUser` as the highest tier.
- Keep Admin-level features available to both `Admin` and `SuperUser`.
- Keep school administration features available to `SchoolAdmin`, `Admin`, and `SuperUser` where the backend endpoint permits it.
- Staff assignment choices remain limited to `Assistant`, `SchoolTeacher`, `SchoolSupervisor`, and `SchoolAdmin`; do not assign Admin or SuperUser as an institute/course staff role merely to grant application privileges.
- The backend remains authoritative. Hiding a button is not security; always handle a `401` or `403` response.

### Migration context

Existing database users were migrated as follows:

```text
Previous Admin (128)       -> SuperUser (64)
Previous SuperUser (32)    -> SchoolAdmin (16)
Previous Manager (64)      -> SchoolAdmin (16)
```

There should be no legacy `Manager` option after deployment. A role-management screen should reload user records after the backend and database migration, rather than trusting a cached integer.

## MFA For Admin And SuperUser

### Functional behavior

The email MFA verification code lasts 8 hours for `Admin` and `SuperUser`. It lasts 30 minutes for all other roles, including SchoolAdmin, Supervisor, Teacher, Assistant, and Parent.

This is the code validity period, not an 8-hour JWT, browser login, or token expiry. Continue to respect the API’s existing access-token expiry and logout behavior.

### Required implementation

1. After `POST /api/users/session/login`, inspect `requiresTwoFactorVerification`.
2. Display the API value `twoFactorCodeExpiresOn`; do not derive expiry from the user’s role in the browser.
3. Verify with `POST /api/users/session/{sessionId}/verify-2fa`.
4. Resend with `POST /api/users/session/{sessionId}/resend-2fa`; replace the displayed expiry with the returned value.
5. Do not add an admin UI setting for 4/6/8 hours. The server configuration currently defines the policy as eight hours for Admin and SuperUser.

## Participant Terminology In Emails

Automated enrollment and attendance emails now use `Assalaamu alaikum` and participant wording rather than parent/child language.

This change is server-side. The admin portal should update any preview fixtures, email-history snapshots, test assertions, or explanatory screens that show the old wording.

Expected cancellation content:

```text
Assalaamu alaikum,
The participant's registration has been cancelled.

Assalaamu alaikum,
L'inscription du participant a été annulée.
```

Do not add the prior unpaid-fee reason to an email preview. The cancellation template no longer includes it.

## Child Educational Profile And Surah Status

### Deployment prerequisite

Before this API version is deployed, the backend team must run `Application.Users.Repository.Implementation/Scripts/CreateChildEducationalProfileSurahAssessments.sql` in the target database. Do not use the retired profile-wide `SurahCompletionStatus` migration.

### Use cases

- School staff review or correct Surah information for a participant assigned to one of their active course groups.
- SchoolAdmin, Admin, and SuperUser can manage a participant profile without course-group assignment restrictions.
- A parent may initially provide profile information, but after the catalog is marked provided, school staff own later corrections.

### Endpoints

```text
GET /api/quran/surah-options
GET /api/children/{childId}/educational-profile
PUT /api/children/{childId}/educational-profile
```

### Request contract

```json
{
  "familyId": "a1111111-1111-1111-1111-111111111111",
  "surahAssessments": [
    { "surah": 1, "completionStatus": 2, "remarks": "Completed assessment on 2026-09-18." },
    { "surah": 112, "completionStatus": 1, "remarks": "Revision remains." }
  ]
}
```

Values:

```text
Incomplete = 0
PartiallyCompleted = 1
Completed = 2
```

### Admin UI requirements

1. Load the canonical list from `GET /api/quran/surah-options`; do not hard-code Surah IDs/names.
2. Present one Surah assessment row per selected Surah, each with its own required `Incomplete`, `Partially completed`, or `Completed` select field.
3. Present Surahs as a multi-select/checklist, then collect one status and remarks value for every selected Surah. Remove duplicates before submitting.
4. Limit remarks to 500 characters per Surah. Show a character count and prevent submission beyond the limit.
5. Send the selected participant’s actual `familyId`; the server rejects a mismatched family ID.
6. Display `createdAt` and `updatedOn` read-only for audit visibility.
7. Show the `hasSurahCatalogBeenProvided` child flag in the profile view. For staff, this means the profile has entered the school-managed stage; it does not block authorized school updates.

### Error handling

- `400`: malformed status, an invalid or duplicate Surah, a mismatched family, or remarks beyond 500 characters for one Surah.
- `403`: a Teacher/Assistant is not assigned to an active group containing that participant.
- `404`: child/profile not found. Refresh the participant list.

## Individual Student Attendance

### Functional behavior

The attendance screen now supports updates for one participant without resubmitting the entire group. When a staff user first loads a group for a date, unsaved roster students are returned as `Present` by default. This default is a UI-ready record, not an already persisted attendance row.

### Endpoints

```text
GET /api/staff/me/course-groups/{courseEnrollmentGroupId}/attendance?attendanceDate=YYYY-MM-DD
PUT /api/staff/me/course-groups/{courseEnrollmentGroupId}/attendance
PUT /api/staff/me/course-groups/{courseEnrollmentGroupId}/attendance/students/{studentCourseEnrollmentId}
```

The endpoint requires at least `Assistant`; higher staff and administrative roles remain subject to the backend’s course-group access rules.

### Single-student update request

```json
{
  "courseId": "c3333333-3333-3333-3333-333333333333",
  "instituteId": "d4444444-4444-4444-4444-444444444444",
  "attendanceDate": "2026-09-18T00:00:00Z",
  "studentCourseAttendanceId": null,
  "attendanceStatus": 0,
  "lateArrivalTime": null,
  "earlyPickupTime": null,
  "pickupContactType": 0,
  "pickupUserId": null,
  "pickupOtherContactId": null,
  "notes": ""
}
```

The route supplies `courseEnrollmentGroupId` and `studentCourseEnrollmentId`. Do not add either identifier to the body as a substitute for the route value.

### Attendance UI rules

1. Load the group attendance before editing; use returned student enrollment IDs, course ID, and institute ID.
2. Initialize every unsaved participant as `Present` from the API response.
3. Use the individual endpoint when only one row changes. This avoids accidental overwrites of other staff edits.
4. Use the whole-group endpoint only for a deliberate bulk-save operation.
5. Preserve `studentCourseAttendanceId` when it exists; use `null` for the first save.
6. Require the appropriate time/pickup values when the selected attendance state requires them. Let the API validate the final request.
7. Refresh the saved row or group after a successful update and render the returned persisted state.

### Parent notifications

Attendance emails are sent by backend policy for notification-worthy changes such as absence. The admin UI must not send a separate client-side notification and must not display the old `Dear parent` or `Cher parent` wording in any preview.

## Family-Member Labels In Admin Views

`ChildResponse` includes:

```text
userType: Child=0, Self=1
displayType: Child=0, Self=1, Spouse=2, OtherAdult=3
```

`UserType` values `2`, `3`, and `4` are retired. Do not expose them in a form, filter, or response mapping. Mother, Father, and Guardian remain values of the separate family relationship data and are still used for business rules such as spouse display, authorized contacts, and pickup details.

Use `displayType` to render relationship labels relative to the active viewer. Do not update `userType` to force a spouse label. In storage, both spouses may correctly be `Self`; the API calculates `Spouse` for the opposite viewer.

## Enrollment And Transaction Behavior

When a family returns to the same course after all prior enrollments were cancelled or soft-deleted, the backend reuses the existing family-course transaction and payment code. The admin portal must:

- Reload enrollment and transaction data after re-enrollment.
- Never create or locally synthesize a new payment code.
- Avoid showing a second transaction as an expected result solely because the prior enrollments were cancelled.

## Standard API Integration Rules

Continue to send the existing authenticated headers:

```http
Authorization: Bearer <access-token>
Session_Info: <session-id>
Content-Type: application/json
```

Handle `401` by returning to sign-in, `403` by hiding/locking the action with an access message, `400` by displaying the backend validation text, and `404` by refreshing the current selection.

## Admin Portal Acceptance Checklist

- Role badges and filters use the new values and do not contain Manager.
- Admin and SuperUser MFA shows the server-supplied 8-hour expiry; all other roles show the server-supplied normal expiry.
- Email previews use `Assalaamu alaikum` and participant language.
- Each Surah's independent status and remarks can be read and saved by authorized staff.
- Remarks cannot exceed 500 characters per Surah.
- The attendance roster shows unsaved students as Present.
- A staff member can save one student’s attendance without submitting other students. Attendance `notes` and course-result `remarks` are optional; omit them when empty.
- Re-enrollment reloads and preserves the existing transaction/payment-code relationship.
