# Course Restrictions and Custom Requirements: FE Implementation Guide

## 1. Scope and Availability

This feature is implemented in the Maktab course contracts and course APIs.

It adds two course-level fields:

| Field | Type | Default | Purpose |
| --- | --- | --- | --- |
| `isAdultRestricted` | boolean | `false` | Tells the Parent Portal that adults must not be registered in this course. |
| `customRequirements` | array of enum values | `[]` | Tells the UI that the course has additional requirements, such as a Surah requirement. |

These fields are stored at course level. They do not replace enrollment-group prerequisites, registration dates, age validation already enforced elsewhere, or payment validation.

## 2. Enum Contract

`customRequirements` uses integer enum values:

| Value | Name | FE meaning |
| --- | --- | --- |
| `0` | `None` | No custom requirement. Do not persist or display it with other values. |
| `1` | `SurahRequirement` | Show the Surah requirement workflow/content configured for the course. |
| `2` | `Other` | Show generic additional-requirement messaging/details. |

Rules:

- Send enum values as numbers, for example `[1, 2]`.
- Do not send `0` together with other values.
- Treat absent, `null`, or empty array as no requirements.
- De-duplicate selected values before sending.
- Be forward compatible: if a new numeric value is returned later, display a generic `Additional requirement` label rather than failing the page.

## 3. Read Course Data

Available endpoints:

```http
GET /api/courses
GET /api/courses/{courseId}
```

Relevant response example:

```json
{
  "courseId": "11111111-1111-1111-1111-111111111111",
  "instituteId": "22222222-2222-2222-2222-222222222222",
  "name": "Weekend Quran School",
  "isRegistrationOpened": true,
  "isAdultRestricted": true,
  "customRequirements": [1, 2],
  "courseEnrollmentGroups": []
}
```

### 3.1 Parent Portal course catalogue

When loading course cards/details:

1. Read `isAdultRestricted` and `customRequirements` from the course response.
2. Show a visible adult restriction badge when `isAdultRestricted` is true.
3. Render requirement indicators from `customRequirements`.
4. Do not decide eligibility solely from the badge. The registration flow must evaluate the selected child/adult and block the next step when the user is ineligible.
5. Preserve normal display when the fields are missing from an older API response: treat `isAdultRestricted` as `false` and requirements as `[]`.

Suggested labels:

```text
Adults cannot register for this course.
Surah requirement applies.
Additional course requirement applies.
```

### 3.2 Adult restriction behavior

`isAdultRestricted: true` means FE must prevent adult registration for that course.

Recommended Parent Portal behavior:

1. Determine whether each selectable learner is an adult using the existing child/adult profile data and Maktab's current age/business-rule source.
2. Disable ineligible adult selection before the enrollment group step.
3. Show the reason beside disabled learners.
4. If every available learner is an adult, disable `Register` and show an explanatory message.
5. Re-evaluate after a learner is added/edited, a family changes, or course details refresh.

Do not infer adulthood from name, school grade, or a hard-coded age in this feature. Use the existing profile/eligibility logic already agreed by the product team.

Example UI behavior:

```text
Child A - selectable
Adult B - unavailable: This course is restricted to children.
```

The FE restriction improves UX. Backend enrollment validation must remain authoritative; a manipulated browser request must not be relied on for eligibility enforcement.

### 3.3 Custom requirement behavior

Use `customRequirements` to add guidance during course detail and enrollment review.

| Requirement | Required FE behavior |
| --- | --- |
| `SurahRequirement` | Show the Surah requirement information, catalog selection, declaration, or evaluation UI when that content is available. Do not silently enroll without displaying the requirement. |
| `Other` | Show course-defined details or a generic requirement notice. Use existing course `details`/`description` fields until a dedicated requirement-detail contract is added. |

The enum currently signals requirements; it does not itself contain Surah identifiers, free-text requirements, or a pass/fail result. Do not invent a payload field and assume the backend stores it. If FE must submit evidence or selections, that needs a separate backend contract.

## 4. Admin Course Create and Edit

Available endpoints for authorized `Admin`, `SuperUser`, and `SchoolAdmin` roles:

```http
POST /api/courses
PUT /api/courses/{courseId}
```

Use the existing `AddCourse` request shape. Include the two new fields on both create and update.

Example create/update payload excerpt:

```json
{
  "instituteId": "22222222-2222-2222-2222-222222222222",
  "name": "Weekend Quran School",
  "nameFr": "Ecole coranique de fin de semaine",
  "description": "Course description",
  "startDate": "2026-09-01T00:00:00",
  "endDate": "2027-06-30T00:00:00",
  "isActive": true,
  "isRegistrationOpened": true,
  "courseSession": 0,
  "registrationStartDate": "2026-08-01T00:00:00",
  "registrationEndDate": "2026-09-30T00:00:00",
  "registrationFee": 0,
  "isAdultRestricted": true,
  "customRequirements": [1, 2]
}
```

### 4.1 Admin form design

Add a `Registration eligibility` section to the course form:

- Toggle: `Restrict adult registration`.
- Multi-select: `Course custom requirements`.
- Options: `Surah requirement`, `Other`.
- Helper text explaining that requirements appear in Parent Portal and must be supported by course details/process.

Form rules:

- Default adult restriction to off for new courses.
- Default custom requirements to none/empty.
- When editing, load current values from `GET /api/courses/{courseId}`.
- Do not overwrite unspecified existing course fields. Send the complete existing `AddCourse` shape because update uses that contract.
- Warn staff before making a course adult-restricted while registration is open, because current adult applicants may become ineligible in the FE.

### 4.2 Existing courses and backwards compatibility

All existing courses default to:

```json
{
  "isAdultRestricted": false,
  "customRequirements": []
}
```

Existing course behavior must not change until an admin enables a setting.

## 5. Mosque and Institute Context

Mosques use the existing institute data model with `InstituteType = Mosque`; they are not a duplicate standalone entity.

Relevant endpoints already available:

```http
GET /api/mosques
GET /api/mosques/{mosqueId}
POST /api/mosques
PUT /api/mosques/{mosqueId}
DELETE /api/mosques/{mosqueId}
```

Courses can be attached to a mosque through the normal `instituteId`. Admin course forms should use the selected institute/mosque list as the source for `instituteId`.

## 6. Parent Portal Acceptance Criteria

- Course list and detail pages do not break when the new fields are absent.
- Adult learners are visibly prevented from selecting an adult-restricted course.
- Child learners remain eligible when normal course/group rules allow them.
- Surah and Other requirement indicators are displayed consistently.
- Existing courses behave unchanged by default.

## 7. Admin Acceptance Criteria

- Course create sends `isAdultRestricted` and `customRequirements`.
- Course edit reads and preserves both fields.
- Numeric enum values are sent correctly and duplicate/None values are not sent.
- Mosque/institute selection remains based on the existing institute model.
- Staff understand that custom requirements are a UI/business signal, not a replacement for any required backend enrollment validation.
