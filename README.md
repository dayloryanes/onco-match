# OncoMatch

An independent portfolio proof of concept for oncology research study screening.
All patients are fictional. This application does not determine clinical
eligibility or approve enrollment.

## Current behavior

Demo login opens Dashboard, with navigation to the 10 fictional patients and
imported studies. Operators can explicitly import registry pages and retry failed
attempts. Both roles can browse the same studies, details, and import history.
API permissions are enforced independently of the UI.

Application matching and inference are not implemented. Ollama is not required
to run the application. Licensing is undecided.

The separate [evaluation runner](tools/condition-label-evaluation/README.md)
supports native and OpenAI-compatible local Ollama requests for development
evaluation. Its README covers offline checks, configuration, and the limited live
evidence. Live execution is currently disabled.

See the [documentation index](docs/README.md) for specifications, evaluation
findings, and project status.

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
| Viewer | `viewer` | `ViewerDemo1!` |
| Operator | `operator` | `OperatorDemo1!` |

These credentials are intentionally public and also displayed on Login. Do not
reuse them elsewhere. Normal login opens Dashboard; a protected patient or study
link returns to that record after login. Use the header to browse Patients and
Studies. Patient details show supplied age, sex, diagnosis, stage, and disease
state. Unknown means a missing fact; Not applicable means the concept does not
apply. Last viewed marks navigation history, not a clinical review.

## Session and data lifetime

- Login uses secure cookies with a 30-minute lifetime that can renew during use.
  Logout ends the current browser session; shared tabs recheck when focused.
  Independently signed-in browsers remain signed in.
- Both roles share the same in-memory data. Logout preserves completed imports;
  restarting the backend clears imported studies and history and reloads the ten
  fictional patient fixtures. There is no database or patient editor.

Detailed authentication and fixture behavior is documented in the
[login and patient browsing specification](docs/specs/login-patient-browsing.md).

## Study importing

Dashboard offers Import first 10, Load Next 10, or Retry import to Operators.
Each action requests one ClinicalTrials.gov page using the fixed cancer search,
Recruiting/Not Yet Recruiting statuses, interventional/observational study types,
and registry relevance ordering. No geography or age filter is applied. Relevance
is to the search, not a patient; no particular patient alignment or age mix is
guaranteed. Importing does not perform patient screening or start inference.

Usable records are retained, duplicates skipped, and malformed records reported
in import history. Only one import can run at a time. Requests time out after
30 seconds. After a failure, review history and choose Retry import explicitly.
Leaving Dashboard or logging out requests cancellation; completed imports remain.

Both roles can inspect study lists/details and history. Refresh reads current
application memory; it does not fetch updates from the registry. Other users'
completed imports appear after Refresh. Study fields retain supplied source text;
Not available means display information is missing.

See the [study importing and browsing specification](docs/specs/study-import-browsing.md)
for pagination, validation, recovery, cancellation, and navigation details.

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

Automated tests use controlled responses rather than live registry or inference
services. API tests exercise authentication and CSRF protection; React tests use
MSW. Browser tests start both applications over HTTPS and a controlled registry
service on port 7133. The test backend selects that service with
`Registry__BaseUrl`; normal startup uses ClinicalTrials.gov.

Stop manually running servers on ports 7132, 7133, and 5173 before running
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
- Port already in use: stop the process you started on that port; avoid running
  the test-managed servers and manually started servers together.
- Registry unavailable/timeout/invalid response: inspect history and choose Retry
  import explicitly. A failed history retrieval must succeed before importing
  again.
- Backend startup reports an invalid patient fixture: check the identified record
  and field. Preserve missing values rather than inventing clinical facts.

## Project Structure

- `backend/` — ASP.NET Core API, fictional JSON fixtures, and xUnit tests.
- `frontend/` — React/TypeScript/Vite UI, React behavior tests, and Playwright.
- `docs/` — specifications, planning, evaluation findings, and project workflow.
- `tools/condition-label-evaluation/` — independent inference evaluation runner.

Implementation and documentation are AI-assisted. Automated checks do not replace
human review. See the [development overview](https://github.com/dayloryanes/onco-match/issues/1)
for current work and review status.
