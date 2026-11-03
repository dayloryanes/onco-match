# Demonstration session controls

Implementation scope: [Extend and observe demonstration sessions](https://github.com/dayloryanes/onco-match/issues/25),
the first slice of the [approved match specification](specs/match-operations-assessment-results.md).
Implementation has passed automated checks and source review. The maintainer
authorized the local implementation commit; publication and merge approval remain
separate. Maintainer manual testing is deferred until tomorrow, not claimed passed.

## Behavior

Both Viewer and Operator see their confirmed session expiry. Five minutes before
that deadline, a nonblocking banner offers **Stay signed in**. It issues a fresh
30-minute secure, HttpOnly session cookie without leaving the current page or
changing the login's identity. A successful extension dismisses the warning.
A failed extension retains the last confirmed deadline and offers **Retry session
extension**; a 401 requires login. Cookie authentication remains the authority
for whether requests are accepted; the local countdown does not authenticate or
revoke a session.

Ordinary authenticated requests retain ASP.NET Core sliding expiration: the
cookie renews after more than half of its lifetime has passed. A successful
request before that threshold does not extend the deadline. The UI uses server
time and a monotonic countdown to accommodate a different browser wall clock.
The local countdown makes no network requests. There is no authentication
keepalive or mouse/keyboard inactivity timer.

Tabs sharing a login receive actual renewal metadata through BroadcastChannel.
Login/logout changes cause the other tabs to check their shared cookie with the
API. Focus checks remain in place and preserve mounted protected content during
a successful check. A delayed response cannot move a newer confirmed expiry
backward. Independently signed-in browser contexts have separate cookies and
channels. Browsers without BroadcastChannel still recheck on focus.

No Match execution, operation deadline, placeholder Match UI, inference call, or
server-side revocation store is added in this slice. The following slice can use
the existing per-login `session_id` claim and actual issued-ticket metadata for
its Auth-owned operation-lifetime coordinator. Login, browsing, imports, and
their existing cancellation behavior remain covered by regression tests.

## API and implementation notes

- `GET /api/auth/me`, login, and extension responses include `username`, `role`,
  `expiresAt`, and `serverTime`. Times use ISO 8601 UTC offsets.
- `POST /api/auth/extend-session` requires authentication and the existing
  `X-CSRF-TOKEN` mechanism. Both demo roles are allowed. Missing/invalid CSRF on
  an authenticated request returns 400; an expired/anonymous request returns 401.
- Authenticated responses expose `X-Session-Expires-At` and `X-Server-Time`.
  `X-Session-Renewed: true` identifies an actually issued ticket, including normal
  sliding renewal. No session identifier is exposed in these headers or JSON.
- The Auth cookie manager delegates standard cookie operations to the framework
  chunking manager and observes the protected ticket it actually appends. It does
  not infer renewal from request success or alter framework validation.
- The cookie handler emits sliding renewals at response start. Metadata headers
  run after that callback; session JSON starts headers before serializing its
  small response, so JSON expiry agrees with the cookie actually emitted.
- Demonstration accounts hold account identity separately from session response
  metadata. Cookies remain nonpersistent and keep existing Secure, HttpOnly,
  and SameSite settings. No dependency or production project is added.

## Validation

Automated validation uses the existing API, React, and Chromium boundaries:

- Controlled-clock WebApplicationFactory tests check actual Set-Cookie output,
  matching expiry metadata, early explicit extension for both roles, retained
  session identity, standard sliding renewal, expiry rejection, CSRF, protected
  responses, and independent-login deadlines.
- React/MSW tests check the warning threshold, browser/server clock differences,
  extension success and failure, expired extension, no keepalive, ordinary-request
  renewal, and Dashboard preservation through focus checks and stale responses.
- Chromium workflows run both applications over HTTPS with the fake registry.
  They check warning/retry/extension in place, shared-tab renewal/logout, and an
  independently signed-in context. Browser time advances only the UI countdown;
  real ticket expiry and sliding thresholds are checked with the API test clock.

Final local checks on 2026-11-03:

- All 59 API integration tests and 7 unit tests passed (`dotnet test backend/OncoMatch.slnx --no-restore`).
- All 42 React tests passed (`npm.cmd test`).
- All 19 Chromium workflows passed (`npm.cmd run test:e2e`).
- Frontend build and ESLint passed. Backend Release build passed with zero
  warnings/errors. `git diff --check` passed.
- Separate automated Standards and Spec source reviews assessed the uncommitted
  changes against `a40be83`, explicitly including new files. Both found the same
  delayed-extension deadline rollback. A new behavioral test reproduced the
  failure, the fix passed, and both follow-up reviewers cleared the finding.
  No remaining findings were reported on either axis.
- AI-assisted inspection of the Chromium warning/retry screenshot checked the
  rendered session controls. This is distinct from maintainer manual validation.

Automated execution, automated source review, and maintainer manual validation
are distinct. No new maintainer validation, live-model evidence, model-quality
acceptance, or clinical validation is claimed by this slice. Code and supporting
documentation remain local on `feat/session-controls`. The maintainer authorized
the implementation commit and proceeding to the next slice; push/publication and
merge still need their separate approvals. The next slice builds on this local
session-controls implementation; this approval does not claim that #25 is merged
or that its pending manual validation has happened.
