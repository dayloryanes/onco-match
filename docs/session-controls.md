# Demonstration session controls

Implementation scope: [Extend and observe demonstration sessions](https://github.com/dayloryanes/onco-match/issues/25),
the first slice of the [approved match specification](specs/match-operations-assessment-results.md).
Implementation has passed automated checks and source review. The maintainer
authorized the local implementation commit; publication and merge approval remain
separate. Maintainer manual validation has begun and exposed the follow-up
renewal interaction recorded below; no complete manual pass is claimed.

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
cookie renews after more than half of its lifetime has passed. Token acquisition
and the explicit extension endpoint are excluded from automatic renewal; only a
successful explicit extension issues its replacement ticket. A successful
ordinary request before the threshold does not extend the deadline. The UI uses server
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

## Manual-test diagnosis after the implementation commit

The maintainer requested temporary two-minute cookies with a 30-second warning,
then reported that blocking the explicit extension still advanced the deadline.
These temporary settings and the later UI correction are uncommitted. This
diagnosis does not authorize a new authentication policy or implementation slice.

Four controlled-clock API reproductions passed with the real cookie handler:

1. Fetching `/api/auth/csrf` after the sliding threshold issues a renewed cookie,
   even when the browser never sends the subsequent extension request.
2. A following `/api/auth/me` read reports that confirmed expiry without issuing
   another cookie before its next sliding threshold.
3. A focus-triggered `/api/auth/me` can itself renew after the sliding threshold.
4. An extension rejected for invalid CSRF can also issue a sliding renewal;
   authentication runs before the controller's CSRF rejection.

Command: `dotnet test backend/tests/OncoMatch.IntegrationTests --configuration Release --filter "Fetching_a_csrf_token|Refresh_after_token|A_focus_session_check|A_rejected_extension" --no-restore`.
These tests describe the current behavior; their passing does not mean a proposed
policy change has been implemented.

Separate Chromium checks used the real frontend with controlled API responses
and browser time, rather than treating fake responses as real-cookie evidence.
They reproduced the blocked-extension error after a renewed deadline, confirmed
that Refresh clears local error state without another extension, and confirmed
that focus renewal alone can hide the warning. In this controlled test the
warning appeared at 30 seconds remaining; a warning first appearing at zero was
not reproduced. An expired extension returned to Login.

Another isolated Chromium check stopped its own temporary Vite server. The
already-loaded application remained interactive, a focus check plus two Retry
clicks failed, and a full reload received connection refused. This is expected:
the browser retains loaded JavaScript, but API calls use the stopped frontend
proxy. Browser Back can restore a saved page; the exact back-forward-cache path
was not separately reproduced. This does not establish that a stopped server is
still serving pages. The diagnostic servers were stopped afterward.

The UI regression plus the existing App/Studies tests passed (37 tests). The
earlier UI correction removes imminent-expiry wording after a confirmed renewal
while retaining a separate extension-request error and Retry. It changes no
cookie renewal rules. Full default-duration suites were not rerun against the
temporary manual-test durations. No new independent source review is claimed
for these follow-up changes.

The maintainer subsequently approved the correction below. It is now implemented
locally, superseding the diagnostic behavior described above and refining the
ordinary-renewal rule in the parent specification:

- Automatic sliding renewal is suppressed on the CSRF-token and explicit
  extension endpoints, including case/trailing-slash route equivalents.
- Successful explicit extension still issues a fresh ticket. Token acquisition,
  browser-blocked extension requests, and rejected extension actions cannot
  independently renew the cookie.
- Ordinary browsing and focus/current-user checks retain normal sliding renewal.
  They can still renew independently while an extension request is blocked.
- The UI only shows imminent-expiry wording while the deadline is near. A failed
  extension after a separate confirmed browsing renewal gets its own error and
  Retry rather than a misleading expiry warning.

The replacement API regression tests first failed on the unwanted issuance,
then passed after the fix. Both two-minute and thirty-minute cookies are covered,
including rejected CSRF, expiry without renewal, explicit success, and retained
ordinary/focus renewal. All **69 API integration tests and 7 unit tests passed**.
The test host defaults to the approved thirty-minute lifetime for deterministic
historical fixtures; individual tests explicitly select two minutes too.

All **44 React tests passed** with the approved five-minute warning threshold.
That one constant was temporarily set to five minutes for the full suite and
restored to 30 seconds immediately afterward. The blocked/retry and concurrent
renewal tests also passed with the temporary manual-test threshold (38 tests
including existing App/Studies cases). Frontend build and ESLint passed.
Separate automated Standards and Spec source reviews cleared the code; both
requested that the earlier proposed/unapplied documentation status be corrected,
as done here. Neither reviewer ran tests. All four final isolated Chromium checks
passed with controlled API responses: blocked extension leaves expiry unchanged,
successful retry renews and dismisses the warning, Refresh/focus can independently
renew without submitting another extension, and the 30-second warning/expired-login
path behaves as specified. Actual-cookie evidence remains the API tests above.
The full **19-workflow Chromium suite also passed** with the temporary two-minute
cookie and 30-second warning. Its retry assertion initially read the expiry
before the retry completed and assumed distinct whole-second ticket timestamps;
the test now waits for server-confirmed success/error removal and a later real
server timestamp. No further application behavior change was needed for that
test correction. Both follow-up reviewers cleared the final documentation and
test updates. Frontend ESLint and whitespace checks passed afterward.
No live inference, new maintainer validation of
this correction, follow-up commit, publication, or merge was claimed at that
automated-validation checkpoint.

The maintainer subsequently reported that the corrected blocked-extension
scenario now works as expected. This records a manual pass for that scenario,
not a complete manual regression pass or source audit. The temporary two-minute
cookie and 30-second warning have now been restored to the approved 30-minute
cookie and five-minute warning. The maintainer approved the local follow-up
commit after that manual check. Publication and merge remain separately gated;
the next implementation slice has not started.
