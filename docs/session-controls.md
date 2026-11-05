# Demonstration session controls

Both demo roles see their confirmed session expiry and can explicitly extend the
session without leaving the current page. Original delivery and validation are
recorded in [issue #25](https://github.com/dayloryanes/onco-match/issues/25) and
[PR #29](https://github.com/dayloryanes/onco-match/pull/29). Current work status and
authorization belong in the [development overview](https://github.com/dayloryanes/onco-match/issues/1).

## Behavior

Normal startup uses a 30-minute secure, HttpOnly session cookie and a nonblocking
warning five minutes before confirmed expiry. **Stay signed in** grants a fresh
30 minutes for the same login identity. Success dismisses the warning; failure
keeps the last confirmed deadline and offers **Retry session extension**. An
expired extension returns 401 and requires login.

Ordinary authenticated browsing and focus checks retain standard sliding renewal
after more than half the ticket lifetime has passed. Preparing a CSRF security
token does not renew the login. The explicit extension endpoint renews only when
its action succeeds. Blocking or rejecting that extension cannot independently
add time; another successful browsing/focus request can still renew it.

Returning focus rechecks the session in the background while keeping the current
page and controls in place, so the first click works and scrolling stays stable.
A confirmed expiry clears protected content; a failed check withholds it and
offers Retry. Initial session discovery and cross-tab login/logout checks still
wait for confirmation. API authentication, role checks and CSRF protection remain
the authority for each request, including actions during a pending focus check.

Confirmed renewal metadata is shared across tabs. Login/logout changes cause
other tabs to check their shared cookie. Independently signed-in browser contexts
remain separate. A delayed response cannot replace a newer confirmed deadline.
The warning uses server time and a monotonic countdown to accommodate browser
clock differences. There is no periodic keepalive or mouse/keyboard idle timer.

Stopping an app server does not remove JavaScript already loaded in the browser.
Retry can remain clickable while its network requests fail. Reload needs the
frontend server. Restarting the backend clears imported application data but does
not invalidate an otherwise valid browser login cookie.

## Optional short-session manual testing

Use both matching development profiles, with the apps' normal prerequisites and
HTTPS setup from the [README](../README.md). Stop existing app servers first.

Backend, from the repository root:

```powershell
dotnet run --project backend/src/OncoMatch.Api --launch-profile https-session-test
```

Frontend, in a second terminal:

```powershell
cd frontend
npm.cmd run dev:session-test
```

The backend prints a **two-minute** lifetime. The frontend prints a warning at
**30 seconds remaining**. The backend profile sets `SessionTesting__Enabled=true`
in Development; the frontend command selects Vite's `session-test` development
mode. The short option does not change Production behavior or production builds.
Use the matching commands together; neither app assumes the other selected its
profile. No source edits or local configuration files are required.

After switching profiles, refresh the page and **log out and log in again**.
An existing cookie keeps its previous deadline, so restarting alone does not
establish a fresh test session.

To test blocked extension:

1. Keep the backend running and the browser online. Before logging in, open
   DevTools request blocking and block only
   `https://localhost:5173/api/auth/extend-session`.
2. Log in fresh and leave the app focused without browsing. After roughly
   90 seconds, the warning should show about 30 seconds remaining.
3. Note the confirmed expiry, click **Stay signed in**, and confirm that the
   blocked action leaves it unchanged and offers Retry.
4. Unblock and retry before expiry. Success grants two minutes and dismisses the
   warning. Returning from DevTools can itself trigger a normal focus renewal;
   if that already renewed the session, the warning may close before Retry.

To test actual expiry, use a separate fresh login and leave it untouched for over
2 minutes, without refocusing or browsing. An extension after expiry requires
login again. To test shared tabs, log in, open another tab, and verify logout is
observed in both. An independently signed-in browser context stays separate.

Return to the normal README startup commands after manual testing, then refresh
and log out/in again. Normal timings are 30 minutes and five minutes.

## API and validation boundaries

- Login, `GET /api/auth/me` and successful `POST /api/auth/extend-session` responses
  include `username`, `role`, `expiresAt` and `serverTime`. Extension requires
  authentication and the existing `X-CSRF-TOKEN` protection for both demo roles.
- Authenticated response headers expose `X-Session-Expires-At` and `X-Server-Time`.
  `X-Session-Renewed: true` means a ticket was actually issued. No session ID is
  exposed in these fields. The framework cookie handler remains the authority
  for authenticating requests; countdown state does not authenticate or revoke.
- Real-cookie API tests use the existing controlled clock. Startup-configuration
  tests use the application defaults without TestApp's cookie-lifetime override,
  checking normal startup, the development short option and Production protection.
- React tests cover the normal and short warning thresholds, extension outcomes,
  concurrent renewal, clock differences and preservation through focus checks.
  Chromium exercises full browser workflows with fake external services. Advancing
  browser time alone does not establish that a backend cookie expired or renewed.
- Automated test execution, automated source review and maintainer manual
  validation are separate records. Use the issue/PR evidence for actual results
  and settings. No live inference, clinical acceptance or complete manual pass is
  implied by an automated check.

The [batch assessment workflow](batch-assessments.md) uses an Auth-owned lifetime
coordinator to observe actual cookie renewals and cancel active work at expiry
or logout. The existing per-login `session_id` identifies that work without
replacing cookie authentication or introducing a revocation store. Assessment
baseline and acceptance assets remain separate from application validation.
