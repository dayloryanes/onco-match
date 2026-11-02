# Study importing and browsing

Status: specification approved and published with explicit human approval.
The human approved this bounded implementation and its agreed deterministic tests
on 2026-11-02. Implementation is complete locally on `feat/study-import-browsing`,
with automated validation/review complete. The maintainer reported that the manual
application check looked good and separately approved committing this implementation.
Pushes, merges, further publication, and inference remain unapproved.

Canonical specification: [Study importing and browsing](https://github.com/dayloryanes/onco-match/issues/16).
The overview and Wayfinder map link to the published specification. This local
copy and the implementation are included in the approved local implementation commit.

Context: [development overview](https://github.com/dayloryanes/onco-match/issues/1),
[remaining-decision map](https://github.com/dayloryanes/onco-match/issues/2),
[POC 1 baseline](../planning/poc-1-baseline.md), and
[assessment contract](../planning/condition-label-assessment-design.md).
The development workflow's separate approval boundaries continue to apply.

## Problem Statement

OncoMatch users can sign in and inspect ten fictional patients, but cannot obtain
or browse studies in the application. Operators need a controlled way to import
real registry studies, understand what each attempt imported or rejected, and
recover from failures. Both roles need to inspect the same imported records
before a later slice introduces pair assessments.

## Solution

Add study importing and import history to Dashboard and separate Studies list
and detail views. Operators explicitly request one registry page at a time.
Import usable records, skip duplicates, record individual rejections, and advance
using the registry's next-page token. A malformed individual record does not
discard the page's usable studies or trigger replacement requests.

Both Viewer and Operator can browse studies and history. Only Operator can
import or retry. Shared process memory preserves completed imports across logout
and clears on backend restart. Importing never starts matching or inference.

## User Stories

1. As a signed-in user, I want Dashboard, Patients, and Studies navigation, so that I can move between the available workflows.
2. As an Operator, I want to explicitly import the first page, so that I control when the registry is contacted.
3. As an Operator, I want Load Next 10 to request the next registry page, so that I can add studies incrementally.
4. As a signed-in user, I want a fixed search and visible explanation of its scope, so that I understand how studies were selected.
5. As a signed-in user, I want studies preserved in import order, so that I can follow the registry's returned ordering.
6. As an Operator, I want usable studies retained when another record is malformed, so that one rejection does not prevent useful imports.
7. As a signed-in user, I want fetched, imported, duplicate, and rejected counts, so that I understand each completed page.
8. As a signed-in user, I want rejection reasons and record positions, so that I can identify a problem even when an NCT identifier is missing.
9. As a signed-in user, I want duplicate identifiers recorded without overwriting stored studies, so that later pages do not change earlier records silently.
10. As an Operator, I want duplicate-only and rejection-only pages to advance normally, so that I can continue without repeatedly requesting the same page.
11. As an Operator, I want exhaustion clearly displayed, so that I know there are no more pages available.
12. As an Operator, I want failed requests to preserve existing data and page position, so that retry does not skip studies.
13. As an Operator, I want an explicit retry with a separate history entry, so that failed attempts remain inspectable.
14. As a signed-in user, I want unknown counts displayed as unavailable, so that a failed request is not presented as a successful empty page.
15. As an Operator, I want simultaneous imports prevented across browsers, so that pagination cannot be advanced by competing requests.
16. As an Operator, I want stale import controls detected, so that another user's import does not cause me to request an unexpected page.
17. As an Operator, I want a registry request to time out after 30 seconds, so that an unavailable service does not leave me waiting indefinitely.
18. As an Operator, I want leaving Dashboard or logging out to request cancellation, so that an unfinished import does not intentionally continue independently.
19. As a signed-in user, I want completed imports retained despite later navigation or logout, so that cancellation cannot undo published data.
20. As a Viewer, I want to inspect import history without import controls, so that my read-only access is clear.
21. As a signed-in user, I want all imported studies in a list, so that I can choose a record to inspect.
22. As a signed-in user, I want study details and a registry link, so that I can understand the supplied record and inspect its source.
23. As a signed-in user, I want missing fields clearly labeled, so that I do not mistake absent data for an eligibility conclusion.
24. As a signed-in user, I want data loaded on page entry and an explicit Refresh action, so that I can retrieve imports made by another user.
25. As a signed-in user, I want loading, empty, error, and not-found states distinguished, so that I know what action to take.
26. As a visitor following a protected study link, I want login to return me to that study, so that authentication preserves my destination.
27. As a user whose session ends, I want protected study information cleared, so that the interface reflects my current access.
28. As a signed-in user, I want the existing patient workflow preserved, so that study importing does not regress delivered behavior.
29. As the maintainer, I want repeatable tests without live registry or model calls, so that failures indicate application behavior rather than external changes.

## Implementation Decisions

### Starting point and ownership

- Retain one feature-oriented, controller-based ASP.NET Core API and the separate
  React/TypeScript frontend. Introduce the Studies feature and the minimum shared
  in-memory import coordination required for this slice.
- Existing patient pages establish loading/error/retry/not-found patterns.
  Existing API helpers already handle cookie authentication, CSRF-protected
  POSTs, cancellation signals, and Problem Details errors.
- Prefer those conventions. Keep registry HTTP access behind one replaceable
  outbound HTTP boundary, rather than adding a provider framework or database.
- The existing patient catalog is read-only; it is not a concurrency design for
  the new mutable study/import state. Publish consistent snapshots to readers.
- The names and shapes below were included in the local specification and
  approved during its content review, following the behavior discussion.

### Browser behavior

| Route | Behavior |
| --- | --- |
| `/` | Dashboard with import availability, history, and Operator-only controls |
| `/studies` | All imported studies in first-import order |
| `/studies/{nctId}` | Stored study details, or Study not found |

- Keep existing patient routes and login/logout behavior. Recognize the new
  study routes as safe local login return destinations; never accept an external
  redirect target. Direct links, reload, and Back/Forward must work.
- Both roles see Dashboard, Patients, and Studies navigation. Viewer sees the
  same history but no import/retry buttons. API authorization remains decisive.
- Show Import first 10 before any completed page, Load Next 10 after a completed
  page with a next token, and Retry import after the latest failure/cancellation
  while its page remains current. A completed zero-import page still counts as
  a completed page. No button retries an older history entry.
- Show importing while known active. Disable import controls during an active
  import or after exhaustion. Another browser may remain stale until Refresh;
  server-side conflict handling must still prevent competing work.
- Read Dashboard and Studies data on entry. Provide explicit Refresh to reload
  application state; it never contacts the registry or refreshes stored studies.
  Details are read again on entry. No periodic polling or automatic imports.
- Immediately update the initiating Dashboard after its own completed import.
  After an error, reload history/state without automatically retrying the POST.
  A lost response may hide a completed import; inspect current server state first.
- Distinguish an empty study collection from failed retrieval. Show Not available
  for absent display information. Keep nonblank source text visible without
  rendering registry strings as executable markup.
- Retain accessible labels, keyboard navigation, loading announcements, and
  readable narrow-screen layouts. Clear protected content on session expiry and
  discard responses from abandoned requests or a cleared session.

### Registry request contract

Use the ClinicalTrials.gov v2 studies endpoint with these fixed parameters:

| Parameter | Value |
| --- | --- |
| `format` | `json` |
| `query.cond` | `cancer` |
| `filter.overallStatus` | `RECRUITING,NOT_YET_RECRUITING` |
| `filter.advanced` | `AREA[StudyType](INTERVENTIONAL OR OBSERVATIONAL)` |
| `sort` | `@relevance` |
| `pageSize` | `10` |
| `pageToken` | Absent initially; exact prior successful response token thereafter |

- Request exactly one page per accepted import. The registry can return fewer
  than ten records. Do not fetch replacements for duplicates or rejections.
- Preserve the filters, relevance ordering, and opaque token across pages. Do not
  add geography, age, or editable user search/filter controls.
- Apply a 30-second outbound request deadline, including reading the response.
  No automatic HTTP retries or redirected requests to another registry host.
  Cancellation also applies while decoding/classifying records before commit.
- Relevance is to the fixed cancer search, not to a patient. It guarantees
  neither cancer-type alignment nor a particular pediatric/adult distribution.
  The live registry can change between page requests or retries.
- A completed page stores its next token, or marks exhaustion if none remains.
  A malformed envelope, unreadable JSON, non-success upstream response, transport
  failure, or timeout does not advance pagination.
- Envelope validation is separate from record validation. Require an object with
  a `studies` array and an absent or nonempty-string `nextPageToken`.
  Reject an invalid envelope as one failed attempt. An empty studies array is a
  valid page; its token still determines whether another page exists.
- Unknown extra registry fields are allowed and are not record rejection reasons.
  No new live lookup or model request is authorized by writing this contract.

### Study representation and individual validation

Use the registry NCT identifier as application study identity and deduplication
key. Do not generate a second public study ID. Require uppercase `NCT` followed
by eight ASCII digits; do not silently repair an invalid identifier.

| Application field | Registry field under `protocolSection` | API representation |
| --- | --- | --- |
| `nctId` | `identificationModule.nctId` | Required string |
| `title` | `identificationModule.briefTitle` | String or null |
| `recruitmentStatus` | `statusModule.overallStatus` | Supplied string or null |
| `studyType` | `designModule.studyType` | Supplied string or null |
| `conditions` | `conditionsModule.conditions` | Array of original strings or null |
| `briefSummary` | `descriptionModule.briefSummary` | String or null; details only |
| `minimumAge` | `eligibilityModule.minimumAge` | Supplied string or null; details only |
| `maximumAge` | `eligibilityModule.maximumAge` | Supplied string or null; details only |
| `sex` | `eligibilityModule.sex` | Supplied string or null; details only |
| `registryUrl` | Derived from validated NCT identifier | ClinicalTrials.gov study URL; details only |

- Summaries expose the first five fields; details include all fields. Return
  strings without trimming or rewriting source content. Missing or JSON-null
  optional scalar fields become null. Blank display text is shown as unavailable
  without changing its stored value. Do not substitute title text for conditions.
- Preserve missing/null conditions, an empty array, blank entries, duplicate
  labels, original order, and original decoded text. Keep a detached source record
  snapshot in memory so later assessment can distinguish absent fields from null
  and assign condition source positions without reconstructing the input.
- Missing/null optional modules mean missing data. Present modules must have the
  expected object shape. Present selected scalar fields must be strings or null;
  a present conditions collection must be an array of strings. Non-object study
  entries, missing/invalid identity, or incompatible shapes reject that record.
- Validate the fields consumed by this slice, not every unrelated registry field.
  Preserve additional source fields without interpreting them.
- Unfamiliar textual age/sex values are not structural failures. Do not convert
  them into valid eligibility rules or apply patient screening during import.
  Titles, summaries, recruitment status, and study type also retain supplied text;
  registry filtering selects scope, rather than a new local clinical classifier.
- Classify records in response order: reject an invalid record; otherwise skip it
  as a duplicate if its NCT identifier was already stored or accepted earlier in
  this page; otherwise accept it. A rejected record does not reserve its identity.
  Each fetched position has exactly one disposition. A later duplicate never
  replaces the first accepted source snapshot, even if its content differs.
- For completed pages, fetched = imported + duplicate + rejected. Preserve
  original source positions in rejection details, using a documented one-based
  `recordNumber`. Record its NCT identifier when a usable identifier is available.
  Do not invent an identifier for a malformed record.
- Retain duplicate-only, rejection-only, and empty completed pages in history.
  Advance using their returned token, including when zero records were imported.
  Individual rejections do not offer a per-record retry, repair, or replacement.

### State, concurrency, and cancellation

- Studies, history, and cursor state are shared for the backend process lifetime.
  Logout preserves completed data; restart clears all study/import state. Keep
  exactly the delivered ten fictional patients unchanged.
- Admit at most one import across the application. Reject a competitor promptly;
  do not queue it or create an attempt for a request that was never admitted.
- Expose an opaque state version. Every admission and terminal state transition
  changes it; a fresh process uses a distinct version. Compare the submitted
  version atomically with import admission. An old version cannot start work.
- Stage the page's accepted studies, duplicate/rejection details, and next token.
  At commit, publish all of them and the completed history entry together. This
  atomic publication protects state; it does not make a rejected record invalidate
  the other records on that page. Readers never see half a committed page.
- Cancellation detected before commit discards staged additions and retains the
  cursor. Once committed, navigation, cancellation, or a lost response cannot undo
  the import. Classify the attempt as completed even if the caller disconnects
  immediately after commit. Always release the import gate on terminal outcomes.
- Abort the initiating browser request on Dashboard exit, logout, or session
  clearance. Observe request cancellation in backend and registry I/O. Browser
  close/disconnect is best effort once detected, not a guarantee of instant notice.
- Logout must also explicitly signal cancellation of the initiating authenticated
  session's active import in the backend, including logout in another tab sharing
  that session. Cookie invalidation alone does not cancel an already admitted
  request. Associate operation ownership with the session, not merely the public
  demo username; independently logged-in browsers must not cancel each other's
  work. This extends the existing logout path, without a public cancellation API.
- A cancelled attempt remains inspectable after returning/signing in. It can be
  retried only while its current page remains unresolved. Retry is a new request
  from the start, not a continuation of partially processed response bytes.

### Application API contracts

All routes require authentication. Explicitly allow Viewer and Operator on reads;
only Operator may POST. Use the existing secure-cookie, same-origin proxy,
`X-CSRF-TOKEN`, uncached API, and Problem Details conventions.

| Endpoint | Successful response |
| --- | --- |
| `GET /api/studies` | 200, array of study summaries in first-import order |
| `GET /api/studies/{nctId}` | 200, study details; no registry lookup on a miss |
| `GET /api/study-imports` | 200, current import state and history |
| `POST /api/study-imports` | 200, current import state/history after a committed page |

- POST accepts exactly the client's `expectedVersion` string. It never accepts
  registry tokens, query parameters, filters, page sizes, or a historical attempt
  to replay. Missing/invalid input returns 400 `invalid_request`.
- A normal import and explicit retry use the same POST. The server selects only
  the current cursor. It links a retry to the most recent failed/cancelled attempt
  for that cursor; a completed page's successor starts a new page.
- Import-state fields are `version` (opaque string), `isImporting` (boolean),
  `isExhausted` (boolean), `nextAction` (`initial`, `next`, `retry`, or `none`),
  `totalImportedStudyCount` (integer), and `attempts` (newest first).
  While active or exhausted, `nextAction` is `none`; the separate flags explain
  why. Initial empty state has `initial`, not exhaustion.
- Each attempt has `id`, UTC `startedAt`, nullable UTC `finishedAt`, `status`
  (`running`, `completed`, `failed`, `cancelled`), nullable `retryOfAttemptId`,
  nullable `fetchedCount`, `importedCount`, `duplicateCount`, `rejectedCount`,
  `importedNctIds`, `duplicateNctIds`, `rejections`, and nullable `failureCode`.
- Completed attempts have all four counts and their identifier/detail arrays.
  Imported IDs are unique; duplicate IDs may repeat to account for repeated
  positions. Rejections have `recordNumber`, nullable `nctId`, `code`, and a safe
  human-readable `reason`. Rejection codes are `invalid_record`,
  `invalid_nct_id`, and `invalid_field_shape`.
- Running attempts have null counts. Failed/cancelled attempts publish no staged
  dispositions: their final counts are null, not fabricated zeroes; their detail
  arrays are empty. Failure/cancellation does not appear as a completed empty
  batch. Do not expose raw upstream bodies, exception details, or cursor tokens.
- Only completed attempts define batches for future matching; retain the exact
  newly imported identifiers for each. No matching execution state is introduced
  by this slice, and zero-import batches never acquire matching work here.

Approved error contract:

| HTTP status | Problem Details `code` | Meaning and state effect |
| --- | --- | --- |
| 400 | `invalid_request` | Invalid application input; no admitted import |
| 400 | `csrf_invalid` | Existing antiforgery rejection; no admitted import |
| 401 | `authentication_required` | Sign-in required; clear protected client state |
| 403 | `access_denied` | Viewer cannot import; no admitted import |
| 404 | `study_not_found` | Malformed/nonexistent study identifier on a read |
| 409 | `import_in_progress` | Another import active; no queue or new attempt |
| 409 | `import_state_changed` | Stale expected version; refresh before another POST |
| 409 | `imports_exhausted` | No remaining page; no registry request |
| 502 | `registry_unavailable` | Transport failure or upstream non-success; failed attempt |
| 502 | `registry_invalid_response` | Malformed/unreadable envelope; failed attempt |
| 504 | `registry_timeout` | 30-second deadline reached; failed attempt |
| 500 | `server_error` | Unexpected failure; preserve pre-commit state and release gate |

- Evaluate authorization, CSRF, and valid application input before admission.
  Under the admission lock, busy takes precedence over stale state; otherwise
  compare version, then exhaustion. This makes conflicts deterministic.
- An admitted failure's Problem Details includes `attemptId` when a response can
  still be delivered. Record an admitted unexpected error as failed as well.
  Read history to reconcile after errors; do not automatically resubmit imports.
- A disconnected caller may receive no cancellation response. The durable-for-
  process history outcome, not a fabricated HTTP success, is the source of truth.
- Existing logout failure/retry behavior remains. Requesting cancellation does
  not mean logout succeeded, and failed logout must not clear a valid session
  merely because its active import was cancelled.

## Testing Decisions

The human agreed backend, frontend, and browser testing with controlled registry
data. Use the highest useful existing boundary and test observable behavior.
Do not assert private methods, lock choices, component internals, or incidental
HTTP-client construction. The detailed coverage below was approved with the spec.

### Backend: API integration boundary first

- Extend the existing WebApplicationFactory approach with controlled outbound
  registry HTTP responses. Exercise real routing, cookie authentication,
  authorization, CSRF, response serialization, and shared state together.
- Verify all fixed query parameters, first/next token handling, exactly one page
  per import, and no registry request for denied/stale/busy/exhausted requests.
- Cover normal/short/empty pages, duplicates across and within pages, mixed valid
  and rejected records, duplicate-only and rejection-only pages, missing optional
  fields, unfamiliar strings, malformed shapes, and source-position preservation.
- Verify completed count arithmetic, first accepted snapshot retention, original
  study order, history order, retry linkage, exhaustion, and absent final counts
  for failed/cancelled attempts. Failure never changes studies or cursor.
- Cover upstream non-success, malformed envelopes/JSON, transport error, deadline,
  unexpected failure, lost-response reconciliation, and explicit retry. No real
  30-second sleeps: use controlled transport/time rather than timing assumptions.
- Use deterministic barriers to hold a response and prove concurrency rejection,
  stale-version rejection, cancellation-before-commit, and completion-before-
  cancellation. Verify all terminal paths release the gate.
- Prove logout cancels only its initiating session's operation, including shared
  tabs, and that independently logged-in instances of the same account remain
  independent. Prove completed imports survive logout and a fresh host is empty.
- Add focused xUnit rule tests only for substantial independent validation rules
  that need additional combinations. Do not duplicate API tests at every layer
  or extract interfaces only to satisfy a unit-testing structure.

### Frontend: React behavior boundary

- Follow the existing Vitest/React Testing Library and MSW patterns. Verify role
  controls, action labels, pending/disabled states, history counts/rejections,
  duplicate-only/rejection-only results, explicit refresh, and stale/busy errors.
- Verify study list/details, unavailable fields, empty/error/retry/not-found
  states, protected deep-link return, session expiry, and abandoned-response
  handling. A retrieval Retry must not accidentally perform an import POST.
- Verify failed imports refresh state before another explicit action, lost
  responses do not trigger automatic resubmission, and navigation/logout aborts
  the initiating request. Existing patient navigation remains functional.

### Browser: a few complete workflows

- Keep real frontend, proxy, backend, cookies, CSRF, and study storage. Replace
  only the outbound registry with a controlled local HTTP service. Browser-side
  API interception alone cannot establish backend import behavior.
- Cover Operator login, mixed-page import, counts/history, study details, next
  page/exhaustion, and explicit retry after a controlled failure.
- Cover a separate Viewer session reading the same studies/history after Refresh
  without import controls, plus a protected study deep link through login.
- Give mutable import scenarios isolated backend instances or an explicitly
  serial, isolated workflow. Existing parallel browser tests must not acquire
  an ordering dependency on shared imported state. Do not add a public reset API.
- Follow the existing local HTTPS Chromium setup. Record its certificate-trust
  limitation; do not claim validation of other browsers or OS trust configuration.

### Validation reporting

- During later approved implementation, run relevant backend/frontend tests,
  browser workflows, frontend build/lint, and existing login/patient regression
  checks. Introduce the feature with meaningful tests using the agreed TDD flow.
- Separately report automated tests, automated code review, maintainer manual
  application checks, and any separately approved live-registry smoke check.
  A live check is not a deterministic automated test or inference acceptance.
- Use new controlled import fixtures. Do not repurpose reserved model acceptance
  fixtures, seed live pages with fixtures, or call Ollama. No test execution or
  external service experiment is authorized by specification approval alone.

## Out of Scope

- Matching buttons, inference, age/sex pair checks, condition-label interpretation,
  pair results, streaming progress, and changes to the selected development model.
- Registry search controls, changing fixed filters/sort/page size, geography or
  pediatric balancing, replacement-page fetching, and automatic imports/retries.
- Automatic refresh/polling, updating already imported records, per-record retry,
  rejected-record repair tools, and replaying successful/old pages from history.
- Database persistence, independent background jobs, queues, scheduling, and
  completed-import rollback. No changes to the ten fictional patient fixtures.
- Additional study detail sections such as phase, locations, full eligibility
  narrative, detailed descriptions, and editable study data in this slice's UI.
- New authentication schemes, account administration, a production hosting
  design, license selection, inference optimization, or acceptance runs.
- Implementation-ticket breakdown, implementation, dependency installation,
  commits, pushes, and merges under the present specification-publication
  authorization. Publication does not authorize a live-registry smoke check.

## Further Notes

### Agreed decisions and review status

The human approved the slice boundary and Q2-Q12 behavior, including the revised
partial-page policy: keep usable studies, count rejections, and advance to the
next page without replacing skipped records. NCT identity, endpoint
routes, 30-second deadline, session cancellation, manual refresh, and the broad
testing boundaries are settled at that level.

The human subsequently approved the complete local specification, including
response field names, state/version transitions, validation precedence, error
codes, exact envelope requirements, session-specific logout coordination, and
detailed test scenarios. The human then separately approved publication and
linked overview/map updates. No separate glossary, ADR, map, or implementation
ticket was created.

The rejected-record count extends the baseline's existing import-history counts
with the subsequently agreed partial-page policy. The specification is linked
from the overview/map as the detailed authority for this behavior. Any future
baseline cross-reference can be included in an approved repository change;
this publication does not silently rewrite the established baseline.

### References and evidence

- [ClinicalTrials.gov API documentation](https://clinicaltrials.gov/data-api/api)
- [ClinicalTrials.gov official OpenAPI schema](https://clinicaltrials.gov/api/oas/v2)
- [ClinicalTrials.gov study data structure](https://clinicaltrials.gov/data-api/about-api/study-data-structure)
- [Delivered login and patient-browsing specification](https://github.com/dayloryanes/onco-match/issues/8)
- [Delivered first slice](https://github.com/dayloryanes/onco-match/pull/9)

The fixed query values and field mapping were cross-checked against the project's
retained registry-query provenance and study snapshots. Read-only inspection of
the official OpenAPI schema (reported API version 2.0.5) confirmed that `studies`
is required, an empty studies array is valid, and the last page omits
`nextPageToken`. The project deliberately keeps page size fixed even though the
upstream API permits changing it between pages. No live study page was requested.
This was documentation inspection, not an import experiment or a guarantee that
the live registry will always conform to its schema.

### Current authorization and next action

The human approved this bounded implementation and its agreed deterministic
tests on 2026-11-02, using implement with tdd. Local work includes the preserved
specification rename/index changes from the preceding session. No implementation
ticket breakdown or dependency installation was needed.

Automated validation: 51 API integration tests, seven fixture unit tests, 35 React
behavior tests, and four HTTPS Chromium workflows passed. Frontend typechecking,
production build, lint, and tracked whitespace checks passed. Registry responses
were controlled; no live-registry lookup, model request, or acceptance run occurred.
The response-loss barrier verifies that completed imports survive logout and a
lost response, with the next cursor intact. Focus/session checks preserve an
active import until logout or confirmed expiry. Legacy cookies without a distinct
session identity must sign in again before importing.

Separate automated source reviews against starting main
`5d84e1ce252366bf9149875d5ad2aeed1faf037a` found no remaining actionable Standards
or Spec findings after fixes and re-review. This is not maintainer manual
validation or clinical validation. Browser checks use Chromium and accept the
local self-signed certificate; no OS certificate-trust claim is made.

The maintainer subsequently tested the application manually, including connecting
to the real registry, and reported that it looked good. This is a reported manual
check, not an exhaustive scenario-by-scenario validation record. A separate direct
request using the fixed query returned HTTP 200 during diagnosis of a stale local
registry override; that request did not import application data.

The maintainer explicitly approved committing the completed implementation and
preserved documentation work. Push, PR creation, merge, further issue publication,
model changes, and inference/acceptance execution remain unapproved. The reported
loss of list position when returning from details is a proposed follow-up; no
scroll-restoration change is included in this implementation commit. The published
issue remains the canonical behavior specification; this checkpoint is local.
