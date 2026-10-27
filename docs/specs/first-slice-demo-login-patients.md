# Demo login, dashboard, and protected patient browsing

Status: specification approved and published; implementation complete and manually
reviewed by the human in the running application; local commit approved, 2026-10-27.

Canonical specification: [Demo login, dashboard, and protected patient browsing](https://github.com/dayloryanes/onco-match/issues/8).
Delivery checkpoints are recorded on the canonical issue and development overview.

The human approved this specification's contents, publication, and subsequently
this bounded implementation slice with necessary dependencies and agreed tests.
The local implementation is complete on feat/demo-login-patients. The human ran
the application locally, reported that it looked good, and explicitly approved
staging the first-slice files and making a local commit, including this copy.
Pushes, PR publication, merges, additional tickets, and subsequent slices remain
unauthorized. Manual application review does not claim a human source-code audit.

Context: [development overview](https://github.com/dayloryanes/onco-match/issues/1),
[remaining-decision map](https://github.com/dayloryanes/onco-match/issues/2), and
[first-slice readiness decision](https://github.com/dayloryanes/onco-match/issues/3).
The agreed [POC 1 baseline](../planning/poc-1-baseline.md) and
[development workflow](../agents/development-workflow.md) remain authoritative.
GitHub records the approved specification, the readiness decision's resolution,
and the completed publication checkpoint in the map and overview.

## Problem Statement

The repository currently offers application templates, not a usable OncoMatch
workflow. A visitor cannot sign in, reach a dashboard, or inspect the fictional
patient information that later study screening will use. The first slice must
establish a small, demonstrable journey with protected API access and meaningful
tests before importing studies or integrating inference.

## Solution

Provide a login page with visible demonstration credentials for a Viewer and an
Operator. After ordinary login, open a protected dashboard containing the current
username, role, and a working Patients link. Both roles can browse the same 10
fictional patients and open directly addressable patient details. Logout returns
the browser to login and removes protected content.

Dashboard is the application's home page. This slice introduces its navigation
shell; study navigation, import controls, and matching operations arrive in their
respective slices. A login prompted by a protected patient link returns to that
patient destination instead of the dashboard.

The records are synthetic demonstration fixtures. Approving an initial set does
not freeze their contents permanently. Later reviewed revisions update the
fixtures and affected tests together, while the agreed POC scope remains exactly
10 fictional patients. No particular matching outcome is promised by this slice.

## User Stories

1. As a visitor, I want to see that OncoMatch is a fictional-patient portfolio POC, so that I understand the demonstration's purpose.
2. As a visitor, I want visible demo credentials for both roles, so that I can explore without registering.
3. As a visitor, I want to enter a username and password, so that I can sign in as the selected demo user.
4. As a visitor, I want a generic invalid-credentials message, so that I can correct a failed login without receiving account-existence information.
5. As a visitor, I want required-field feedback, so that I can correct an incomplete login submission.
6. As a visitor, I want a service-error message when login cannot be completed, so that I can distinguish an unavailable service from incorrect credentials.
7. As a signed-in user, I want ordinary login to open Dashboard, so that I enter the application's central workspace.
8. As a signed-in user, I want my username and role visible, so that I know which demo account I am using.
9. As a signed-in user, I want a working Patients link on Dashboard, so that I can browse the available records.
10. As a Viewer, I want to browse patients, so that I can inspect the shared fictional data.
11. As an Operator, I want the same patient browsing access, so that I can inspect the data before later operational features exist.
12. As a signed-in user, I want all 10 patients in stable identifier order, so that I can find the same record consistently.
13. As a signed-in user, I want each summary to show identifier, age, sex, and cancer type, so that I can choose a patient to inspect.
14. As a signed-in user, I want a distinct fictional identifier rather than a personal name, so that the demonstration avoids implying real patient identities.
15. As a signed-in user, I want details to include stage, its classification system, and disease state, so that I can inspect the agreed assessment facts.
16. As a signed-in user, I want unknown and not applicable labeled distinctly, so that I do not mistake missing information for a clinical conclusion.
17. As a signed-in user, I want links from details to Patients and Dashboard, so that I can navigate without relying on browser history.
18. As a visitor following a protected patient link, I want login to return me to that destination, so that authentication does not lose my place.
19. As a signed-in user, I want refreshing a protected page to preserve a valid login, so that I can continue browsing.
20. As a signed-in user, I want a loading state while data is requested, so that I understand why content is not yet displayed.
21. As a signed-in user, I want retrieval failures to offer Retry, so that I can recover without seeing a misleading empty list.
22. As a signed-in user, I want an unknown identifier to show Patient not found, so that I can distinguish it from a service failure.
23. As a user whose authentication expires, I want protected content cleared and login displayed, so that the UI reflects my current access.
24. As a signed-in user, I want logout to end this browser session, so that subsequent protected access requires login.
25. As a signed-in user, I want failed logout to show an error and Retry, so that the application does not falsely report that logout succeeded.
26. As a user with multiple tabs, I want them to detect logout when focused or when requesting data, so that they stop presenting stale authenticated content.
27. As a user in another browser, I want that browser's login to remain independent, so that logging out one session does not sign out every demo session.
28. As the project maintainer, I want repeatable API, React, and browser checks, so that the first workflow can evolve with evidence of its behavior.

## Implementation Decisions

### Architecture and current starting point

- Retain one controller-based ASP.NET Core API, organized by Auth and Patients
  features, and the independent React/TypeScript/Vite frontend.
- Use standard ASP.NET Core cookie authentication, claims, and explicit
  authorization policies. Grant patient browsing to Viewer and Operator
  explicitly; do not assume an implicit role hierarchy.
- Load the approved 10 patient records from JSON into shared process memory.
  Both roles see the same records. Logout does not remove them; restart loads
  the fixture again. There is no patient editing API.
- The backend currently has a weather template and empty xUnit tests. The
  frontend has a Vite/React template, build/lint scripts, and no configured
  behavior or browser tests. Authentication, patient loading, routing, the HTTPS
  proxy arrangement, and the agreed test harnesses still need implementation.
- Replace boilerplate only as relevant to this slice. Keep useful configuration
  and infrastructure. Do not introduce additional production projects or a
  speculative data-access layer.

### Browser routes and navigation

Approved browser routes:

| Route | Behavior |
| --- | --- |
| `/login` | Public login page with demo credentials |
| `/` | Protected Dashboard and ordinary post-login destination |
| `/patients` | Protected list of all 10 patients |
| `/patients/{id}` | Protected patient details or Patient not found |

- Dashboard is available to both roles and shows the current username and role,
  a Patients link, and Logout. Do not add inactive Studies/import/matching controls.
- Display the fictional-data notice on the patient views. Use readable labels,
  keyboard-operable links/buttons, and associated login field labels.
- Preserve only a recognized local protected route as the return destination.
  Never redirect to an external URL supplied through a login query parameter.
  After successful login, use that destination once; otherwise open Dashboard.
- Visiting Login with a valid session opens Dashboard. Logout clears any previous
  return destination; following a protected link afterward establishes a new one.
- Direct navigation, browser Back/Forward, and refresh must resolve application
  routes. Resolve authentication before showing protected content. Discard
  responses from requests that belonged to a session the UI has already cleared.

### Demo accounts and session behavior

- Provide one predefined Viewer and one predefined Operator. Credentials are
  intentionally public demonstration credentials, displayed on Login. There is
  no registration, password reset, user administration, or real-user onboarding.
- Approved fixture credentials: username `viewer`, password `ViewerDemo!1`, role
  `Viewer`; username `operator`, password `OperatorDemo!1`, role `Operator`.
  Do not reuse these credentials for any other service.
- Trim surrounding username whitespace and compare it
  case-insensitively; compare passwords exactly without trimming. Missing or
  whitespace-only required inputs receive validation feedback. A supplied but
  incorrect username/password receives the same generic credentials message.
- Use a Secure, HttpOnly, nonpersistent authentication cookie, with an appropriate
  same-site policy for the agreed browser-facing same-origin setup. No Remember
  me option. React must not store the password or authentication ticket.
- Use standard ASP.NET Core sliding cookie expiration with a 30-minute ticket
  lifetime. The human approved this behavior during draft review, replacing the
  earlier exact-idle-time wording. An authenticated request made more than halfway
  through the current ticket lifetime renews it for another 30 minutes. Earlier
  requests do not reset the timer. Mouse movement alone does not renew a ticket;
  do not add background polling that keeps an unattended session alive. Once the
  ticket expires, protected requests are rejected and the UI returns to Login.
- Refresh preserves a still-valid login. Browser closure is not a guaranteed
  immediate invalidation boundary: a nonpersistent cookie is not a promise about
  browser session-restoration behavior.
- Logout applies to the current browser cookie context, including tabs sharing
  it. Independently authenticated browsers remain signed in. Clear protected
  client state after confirmed logout; a transport/server failure shows an error
  and Retry rather than reporting success. If a follow-up session check confirms
  the session is already gone, show Login.
- On tab focus, temporarily withhold protected content while checking the current
  session. On an authenticated API request returning 401, clear protected state
  immediately. No real-time cross-tab channel or polling loop is required.
- Prevent duplicate form submission while a login/logout request is pending.
  Missing-field, bad-credential, and unavailable-service messages are distinct.

Standard sliding expiration is the agreed session-expiration behavior; a custom
exact idle timer is not required. Standard cookie logout removes the browser's
authentication cookie and clears protected client content. The human accepted
that it does not revoke a previously copied authentication ticket on the server;
such a ticket remains subject to normal expiration and renewal rules. Do not add
a server-side revocation mechanism for this slice. Framework
reference: [cookie authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0)
and [sliding expiration](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.cookies.cookieauthenticationoptions.slidingexpiration?view=aspnetcore-10.0).

### API contract

All API requests use relative `/api/...` URLs through Vite's proxy. Local HTTPS
must work at the browser-facing origin and the backend; verify secure-cookie
behavior through that arrangement in browser tests. Production hosting is deferred.

| Request | Request body | Success | Access |
| --- | --- | --- | --- |
| `GET /api/auth/csrf` | None | 200 with `requestToken` | Anonymous or signed in |
| `POST /api/auth/login` | `username`, `password` strings | 200 with current-user response; sets authentication cookie | Anonymous; valid CSRF token required |
| `GET /api/auth/me` | None | 200 with current-user response | Signed in |
| `POST /api/auth/logout` | None | 204; clears authentication cookie | Valid CSRF token required |
| `GET /api/patients` | None | 200 with an array of patient summaries | Viewer or Operator |
| `GET /api/patients/{id}` | None | 200 with patient details | Viewer or Operator |

- Current-user response: `username` and one `role`, whose value is `Viewer` or
  `Operator`. Never expose passwords or authentication tickets in responses.
- Login with incorrect supplied credentials returns 401. Malformed input or
  missing required fields returns 400. Unsupported request content types return
  415. Login while already signed in is not an account-switching workflow; the
  frontend directs a valid session to Dashboard.
- Unauthenticated requests to protected API endpoints return 401, not an HTML
  login redirect. An authenticated principal without an allowed role receives
  403. An authenticated request for an absent patient receives 404. Never reveal
  patient existence before checking access.
- Use structured Problem Details for application errors, with stable `code`
  values such as `invalid_credentials`, `authentication_required`,
  `patient_not_found`, and `csrf_invalid`. Field validation includes field errors.
  Do not expose stack traces. Unexpected failures remain failures, not empty data.
- Apply no-store response caching to authentication and protected patient data.
- The CSRF support endpoint supplements the five business endpoints. Use
  ASP.NET Core antiforgery services to issue a request token and
  its companion cookie. Send the request token as `X-CSRF-TOKEN` for login/logout;
  keep it in memory. Refresh the token after login and when identity changes.
  Missing, invalid, or identity-stale tokens reject the mutation without changing
  authentication state. Obtain a current token before explicitly retrying.
- Return 400 with `csrf_invalid` for antiforgery rejection. A valid-token logout
  when already signed out returns 204, making a lost-response retry safe. This
  does not bypass CSRF validation for anonymous requests.
- Token delivery must be uncached and must not expose credentials. Cross-origin
  token-reading access is not part of this slice.

ASP.NET Core supports request-token delivery to JavaScript and token validation
for cookie-authenticated mutations; authentication transitions require attention
to the token's identity context. The particular endpoint/header contract above is
the approved application contract, not a framework-mandated naming convention. Reference:
[ASP.NET Core antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).

### Patient response and fixture contract

Patient summaries contain `id`, `ageYears`, `sex`, and `cancerType`. Details contain
the same fields plus `stage` and `diseaseState`. Use the identifier itself as the
visible record label. Return summaries in ascending identifier order.

| Field | Representation |
| --- | --- |
| `id` | Stable unique fictional string, such as `P001` |
| `ageYears` | Nonnegative whole years; null means unknown; no date of birth or aging calculation |
| `sex` | `female`, `male`, or `unknown`; do not infer from cancer type |
| `cancerType` | Supplied diagnosis text; null means unknown |
| `stage.status` | `known`, `unknown`, or `notApplicable` |
| `stage.value` | Supplied stage label when known; otherwise null |
| `stage.classificationSystem` | Supplied system label when known; null means unspecified, not a universal default |
| `diseaseState.status` | `known`, `unknown`, or `notApplicable` |
| `diseaseState.value` | `newlyDiagnosed`, `recurrent`, or `refractory` when known; otherwise null |

- Render null age/cancer type and unknown sex as Unknown. Render stage and disease
  state according to their explicit status; never use an undifferentiated dash
  for both Unknown and Not applicable. A supplied stage with an unspecified
  classification system retains its label and displays System unknown.
- Treat stage as a supplied stage-at-diagnosis fact; disease state is separate.
  Do not change stage merely because disease is recurrent. Do not infer TNM
  components, staging-system editions, biomarkers, or treatment history.
- A known stage requires a nonblank value. Unknown/not-applicable stages have no
  value or classification-system label. Disease-state status/value combinations
  must be consistent. Ages and identifiers must be valid; identifiers are unique.
- Reject malformed fixture structure at startup rather than quietly loading fewer
  than 10 records or converting invalid values into invented clinical facts. Error
  messages identify the bad fictional record and field for correction.
- This is the patient-display/data contract, not the assessment/evidence contract.
  That contract remains in its separate decision issue. Any later change to the
  patient contract must be reviewed rather than silently altering these meanings.

### Approved initial 10 fictional patients

Every row is invented for software demonstration and has been reviewed and
approved by the human for the initial slice.
Age is in completed years. Unknown means a deliberately missing supplied fact.
The mixture illustrates UI representations; it is not an estimate of population
frequency or a clinically validated evaluation dataset.

| ID | Age | Sex | Cancer type | Stage | Classification system | Disease state |
| --- | --- | --- | --- | --- | --- | --- |
| P001 | 7 | Female | Acute lymphoblastic leukemia | Not applicable | Not applicable | Newly diagnosed |
| P002 | 15 | Male | Acute myeloid leukemia | Not applicable | Not applicable | Refractory |
| P003 | 12 | Female | Hodgkin lymphoma | Unknown | Unknown | Newly diagnosed |
| P004 | 17 | Male | Osteosarcoma | Unknown | Unknown | Recurrent |
| P005 | 42 | Female | Breast cancer | II | TNM stage group; edition unspecified | Newly diagnosed |
| P006 | 68 | Male | Non-small cell lung cancer | IV | TNM stage group; edition unspecified | Newly diagnosed |
| P007 | 56 | Female | Colon cancer | III | TNM stage group; edition unspecified | Recurrent |
| P008 | 73 | Male | Prostate cancer | II | TNM stage group; edition unspecified | Unknown |
| P009 | Unknown | Unknown | Melanoma | III | TNM stage group; edition unspecified | Recurrent |
| P010 | 61 | Female | Ovarian cancer | Unknown | Unknown | Unknown |

P001-P004 provide pediatric/adolescent examples; P005-P008 and P010 provide known
adult ages. P009 deliberately does not assert an age group or sex. P001/P002 use
not-applicable stage, while other rows demonstrate missing stage or disease state.
The table's human-readable labels map to the explicit statuses above.

The staging distinction follows NCI's explanation that cancers use different
staging systems and that stage at diagnosis is distinct from later changes.
NCI describes no standard staging system for ALL or AML. These references support
those limited representation choices; they do not validate the invented patients
or establish study eligibility:
[cancer staging](https://www.cancer.gov/about-cancer/diagnosis-staging/staging),
[ALL](https://www.cancer.gov/types/leukemia/patient/adult-all-treatment-pdq), and
[AML](https://www.cancer.gov/types/leukemia/patient/adult-aml-treatment-pdq).

Review future fixture changes whenever later study examples expose gaps.
The current records intentionally retain unspecified staging editions; a specific
edition has not been selected. Revisit age granularity, diagnosis specificity, or staging editions
if the assessment design needs them. Do not change a fixture simply to force a
favorable outcome on a live study page. Expected matching results require a
separate review alongside study fixtures.

## Testing Decisions

The human agreed the test boundaries below before this draft. Prefer observable
behavior at the highest useful boundary. Do not assert private method structure,
component internals, DTO property storage, or arbitrary test counts. Exercise
different responsibilities at each layer rather than duplicating every scenario.
All automated checks are deterministic and require neither ClinicalTrials.gov
nor Ollama. This drafting session has not installed tools or run application tests.

### API integration: xUnit and WebApplicationFactory

Use the real controller/authentication/antiforgery pipeline and cookie exchange,
not a test authentication handler that bypasses the behavior being tested.
Isolate application instances and browser cookie contexts where necessary.

- Valid login and session lookup for each role; malformed inputs, bad credentials,
  and generic error behavior. Verify no authentication ticket is created on failure.
- Anonymous access rejected for Dashboard's session lookup and patient APIs;
  unauthorized patient requests must not reveal whether an identifier exists.
- Both roles can retrieve the exact approved fixture summaries in stable order
  and retrieve matching details, preserving Unknown versus Not applicable.
- Unknown patient yields 404 after authentication. An unsupported authenticated
  role yields 403; do not invent an Operator-only feature for this slice.
- Real CSRF token acquisition; rejection of missing/invalid tokens on both login
  and logout; identity-token refresh across login; valid-token logout retry after
  a previously completed logout. Test that failed CSRF checks do not mutate state.
- Logout followed by a protected request without authentication; logout preserves
  shared patient data; independently authenticated browser contexts remain usable.
- Standard sliding expiry and renewal, with controlled time rather than sleeping
  30 minutes. Verify that a request before the halfway point does not renew the
  ticket, a valid request after the halfway point does renew it, and an expired
  ticket cannot renew itself through a late request.
- Verify important cookie flags, JSON/status behavior, and no-store responses.

Prior art: the existing integration test project contains only an empty passing
placeholder, with no application reference or WebApplicationFactory dependency.
Establish that harness during the authorized implementation, not during drafting.

### React behavior: Vitest and React Testing Library

Render the relevant screens/router and control HTTP responses at the network
boundary. Use accessible labels and user actions rather than component internals.

- Login input validation, generic credential error, service error, and pending
  submission behavior; ordinary login opens Dashboard.
- Dashboard shows username/role and working Patients navigation for both roles.
- A protected direct link is restored after login; reject external return URLs.
- Summaries and details display approved data and distinct missing-value labels.
- Loading, retrieval error with Retry, and Patient not found are distinct states;
  a failed list request never renders as a successful empty list.
- Authentication discovery withholds protected content; expiry clears it.
- Successful logout clears state and returns to Login; failed logout offers Retry;
  late responses cannot repopulate content after logout.
- Returning focus rechecks the session before protected content is revealed.

Prior art: React is present but there is no configured Vitest/Testing Library
harness. Add only the necessary tools during approved implementation. The choice
of router and HTTP mocking helper is an implementation detail unless it changes
the agreed behavior or introduces a significant dependency tradeoff.

### Browser workflow: Playwright against both applications

Use the real frontend, proxy, backend, authentication, and fixture data. Run
against the agreed local HTTPS arrangement. No external services are involved.

- For each role: login -> Dashboard -> Patients -> details -> logout. Verify 10
  patients, correct selected details, and protected access requiring login again.
- A small navigation check covers a signed-out patient deep link, return after
  login, and refresh while authenticated.
- Verify logout is detected when another tab is focused. Do not reproduce every
  API/error scenario in browser tests; those belong at the boundaries above.

Prior art: Playwright is agreed but not configured in the current frontend.

### Unit tests and validation

Add focused xUnit tests only for meaningful independent rules introduced by the
implementation, such as rejecting inconsistent fixture records. Prefer the API
integration boundary for behavior already covered there; do not extract a module
just to create unit tests. Replace/remove empty test placeholders as relevant.

The implementation review must record actual build, lint, automated test, and
manual HTTPS setup results, including limitations. No live inference evaluation
belongs in this slice. A new developer should be able to start both applications,
find the displayed demo credentials, and perform the agreed workflow using the
slice's setup instructions.

## Out of Scope

- Study browsing, importing, batches, matching, assessment results, inference
  integration, evaluation experiments, and dashboard operational controls.
- Patient searching, filtering, pagination, editing, deletion, and additional
  clinical attributes such as biomarkers, labs, or treatment history.
- User registration/administration, password recovery, JWT, bearer authentication,
  mobile clients, and universal inference-provider support.
- Database persistence, independent background execution, scheduling, queues,
  refresh/staleness handling, and reassessment workflows.
- A clinical eligibility claim, predetermined matching outcomes, or a universally
  numbered staging model. This slice displays supplied fictional facts only.
- Blanket template cleanup, a production hosting design, a license selection,
  dependency installation during drafting, an implementation-ticket breakdown,
  or server-side revocation of copied authentication cookies.

## Further Notes

### Terminology used in this specification

- **Demo user:** a predefined account for exploring the POC; distinct from a patient.
- **Patient:** one fictional oncology record whose supplied facts can later be
  assessed against study criteria; patients do not log in.
- **Dashboard:** the application home after ordinary login, with navigation in
  this slice and operational features in later slices.
- **Unknown:** the relevant supplied fact is missing or unspecified.
- **Not applicable:** the concept does not apply to that record; not a synonym
  for Unknown or for a study having no identified restriction.
- **Disease state:** a supplied newly diagnosed, recurrent, or refractory status,
  represented separately from stage and its classification system.

These definitions capture the agreed discussion. No separate glossary or
ADR has been created as part of the local-specification authorization.

### Approval checkpoint and next action

Content review is complete. The human approved the routes, demo credentials,
request/response and error contracts, CSRF exchange, all 10 initial patient records,
their data representation, and the testing boundaries. Standard sliding expiration
and standard cookie logout are settled. Patient fixture contents remain revisable
through future reviewed changes.

The human approved the grouped publication step. The specification is published
with the established `ready-for-agent` label, the readiness decision is resolved
and closed with a resolution comment, and the map and overview link to the result.
The published bodies, resolution comment, issue states, and specification label
were verified by reading them back from GitHub. The label indicates specification
readiness only; it does not grant implementation permission.

The human subsequently approved implementing this as one bounded vertical slice
using implement with tdd, including required dependencies and agreed tests.
Implementation is now complete locally. Automated review covered Standards and
Spec separately: Standards had no actionable findings; all three Spec findings
were fixed and rechecked. Validation passed: 7 xUnit fixture rule tests, 24 API
integration tests, 19 React behavior tests, 3 Chromium workflows over HTTPS,
frontend production build, lint, and whitespace checks. Desktop and mobile
screenshots were inspected. Chromium ignores local certificate trust errors;
interactive operating-system trust and other browsers have not been validated.

The human subsequently reviewed the running application and approved the local
commit with subject: feat: add demo login and patient browsing. Pushes, PR
publication, merges, new implementation tickets, and subsequent slices remain
unauthorized. Later assessment/evaluation decisions remain open.
See the canonical GitHub issue and overview for the recorded checkpoint.

Local validation for this specification: read the project guidance, baseline, GitHub
overview and readiness issue, inspected the application/test scaffolding, and
checked the linked primary references. No application behavior is implemented or
validated by this document. Other assessment/evaluation decisions remain open.
