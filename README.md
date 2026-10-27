# OncoMatch

An independent portfolio proof of concept for oncology research study screening.
All patients are fictional. This application does not determine clinical
eligibility or approve enrollment.

## Current behavior

The first slice provides demo login, Dashboard, a directory of 10 fictional
patients, patient details, and logout. Viewer and Operator both browse the same
data. API permissions are enforced independently of the UI.

Studies, importing, matching, and Ollama integration are not implemented in this
slice. See the [approved specification](https://github.com/dayloryanes/onco-match/issues/8)
and [development overview](https://github.com/dayloryanes/onco-match/issues/1)
for scope and current authorization. Licensing is undecided.

## Run locally

Prerequisites: .NET 10 SDK and Node.js 24 with npm. Commands below use PowerShell
and `npm.cmd`, which works when PowerShell execution policy blocks `npm.ps1`.
Do not change execution policy just to run npm.

From the repository root, restore and build the backend:

```powershell
dotnet restore backend/OncoMatch.slnx
dotnet build backend/OncoMatch.slnx
```

In the frontend directory, install the locked dependencies and export a local
HTTPS certificate:

```powershell
cd frontend
npm.cmd ci
npm.cmd run setup:https
```

The export uses ASP.NET Core's development certificate and writes its certificate
and private key into the ignored `.certs` directory. Keep the private key local.
To trust the development certificate for interactive browsing, run the following
and accept the operating system's trust prompt:

```powershell
dotnet dev-certs https --trust
```

Start the backend in a terminal at the repository root:

```powershell
dotnet run --project backend/src/OncoMatch.Api --launch-profile https
```

Start the frontend in a second terminal:

```powershell
cd frontend
npm.cmd run dev
```

Open **https://localhost:5173**. Use `localhost` consistently. The frontend proxies
relative `/api/...` requests to **https://localhost:7132**; use the frontend address
for the demo workflow. The local development proxy accepts the backend's
self-signed certificate. This is development configuration, not a production
hosting arrangement.

| Role | Username | Password |
| --- | --- | --- |
| Viewer | `viewer` | `ViewerDemo!1` |
| Operator | `operator` | `OperatorDemo!1` |

These credentials are intentionally public and also displayed on Login. Do not
reuse them elsewhere. Normal login opens Dashboard; a protected patient link
returns to that patient after login. Patient views show supplied age, sex,
diagnosis, stage/classification system, and disease state. Unknown and Not
applicable have different meanings. Fixture contents can change through review.

## Session and data lifetime

- Secure, HttpOnly, nonpersistent cookies; CSRF tokens protect login/logout.
- A 30-minute authentication ticket uses standard sliding expiration. Eligible
  requests after the halfway point renew it; earlier requests do not reset an
  exact idle timer. There is no background session polling or Remember me option.
- Logout removes this browser's cookie and protected UI. Other tabs recheck when
  focused, and authenticated requests that receive 401 clear the UI. Independent
  browser sessions remain signed in. Server-side revocation of copied cookies
  is not part of this POC slice.
- The 10 JSON patient fixtures load into shared process memory. Logout preserves
  them; restarting the backend reloads them. There is no database or patient editor.
- Invalid fixture structure fails startup with record/field context. Do not
  substitute invented clinical facts for missing values.

## Checks

From the repository root:

```powershell
dotnet test backend/OncoMatch.slnx
```

From the frontend directory:

```powershell
npm.cmd test
npm.cmd run build
npm.cmd run lint
npm.cmd exec -- playwright install chromium
npm.cmd run test:e2e
```

The API tests use real authentication/antiforgery with an in-memory test server.
Cookie expiry tests control time. Fixture unit tests check meaningful validation
rules. React tests control HTTP responses with MSW. Browser tests start both
applications over HTTPS and exercise both roles, deep links, refresh, and logout
across tabs. Stop manually running servers on ports 7132 and 5173 before running
the browser suite; it deliberately does not reuse existing processes.

Playwright accepts the local self-signed certificate without installing a system
trust rule. Its configuration is limited to Chromium; other browsers have not
been verified. Automated tests use no live registry or inference services.

## Troubleshooting

- Missing `.certs` files or HTTPS startup failure: rerun `npm.cmd run setup:https`
  from the frontend directory. If certificates are regenerated, export again.
- Browser certificate warning: complete the development-certificate trust step,
  then restart the browser if needed. Check that you opened `https://localhost`.
- API unavailable or login service error: confirm the backend is running on 7132.
  Retrieval failures offer Retry and do not masquerade as an empty patient list.
- Port already in use: stop the process you started on that port; avoid running
  the test-managed servers and manually started servers together.
- A stale CSRF token after a session change rejects the mutation. Retry explicitly;
  the client obtains a current token before each mutation.

## Project Structure

- `backend/` — ASP.NET Core API, fictional JSON fixtures, and xUnit tests.
- `frontend/` — React/TypeScript/Vite UI, React behavior tests, and Playwright.
- `docs/` — planning baseline, project workflow, and the local specification copy.

Implementation and documentation are AI-assisted. Automated checks do not replace
human review; see the development overview for the current review/merge status.
