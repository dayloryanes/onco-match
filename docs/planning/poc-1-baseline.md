# OncoMatch POC 1 planning baseline

Approved planning baseline recorded on 2026-10-27. This is the current scope and
decision record, not a detailed implementation specification. Read the development
overview for current authorization. Remaining decisions live in the Wayfinder map;
do not duplicate their eventual resolutions across competing planning records.

## Purpose

OncoMatch is an independent, noncommercial public portfolio proof of concept
matching fictional oncology patients to real clinical research studies. It
demonstrates full-stack engineering and locally hosted LLM assessment. Results
are preliminary research-screening suggestions, not clinical eligibility
determinations or enrollment approval. No real patient data, hospital credentials,
or proprietary information is used. Licensing remains undecided; public GitHub
visibility is not an open-source license.

## Architecture and tools

- One feature-oriented ASP.NET Core controller-based API project, organized
  around Auth, Patients, Studies, and Match. Shared functionality may live outside
  features when justified; avoid extra production projects, speculative layers,
  premature abstractions, and microservices.
- C#/.NET 10 LTS, Visual Studio 2026, xUnit, and WebApplicationFactory.
- Independent React/TypeScript/Vite frontend in the same repository, with npm,
  ESLint, Vitest, React Testing Library, and Playwright.
- ClinicalTrials.gov API v2 for studies; Ollama as the documented inference
  starting point. React accesses inference only through the ASP.NET Core API.
- GitHub Issues, feature branches, PRs, and human review for implementation work.

The repository began with backend/frontend templates and placeholder tests.
Scaffolding is not implemented application functionality. Boilerplate cleanup is
incremental within approved work, not a requirement to remove everything first.

## Authentication and shared state

Use secure browser cookies with appropriate CSRF protection. React and the API
run on their own localhost ports; Vite proxies relative `/api/...` requests.
Accommodate secure cookies in local HTTPS configuration. Production hosting is
deferred; the browser-facing same-origin arrangement is agreed.

Use standard ASP.NET Core authentication schemes, claims, roles, and authorization
policies. Keep business logic independent of cookies or bearer tokens. Future
clients, including React Native, may use bearer authentication later; JWT,
additional schemes, registration, and account administration are outside POC 1.
Predefined demonstration users are acceptable.

| Role | Permissions |
| --- | --- |
| Viewer | Read patients, studies, import history, and stored assessments |
| Operator | All viewing permissions, plus importing and matching/recovery operations |

Enforce permissions at the API. Do not assume a role hierarchy from names.
Patients, studies, history, and results are shared across demo users for the
backend process lifetime. Logout preserves completed data. Restart clears imported
studies, batches, progress, and assessments; the synthetic patients load again.

## Synthetic patients

Load exactly 10 fictional patient records from JSON into memory. Include pediatric
and adult examples with varied diagnoses and cases demonstrating supported
alignment, exclusions, and uncertainty. Exact records need review alongside study
fixtures; live pages cannot guarantee a particular outcome distribution.

Assessment attributes are age, sex, cancer type, stage with its classification
system where applicable, and optional disease state. Represent unknown separately
from not applicable. Disease state can describe newly diagnosed, recurrent, or
refractory disease; do not infer it from facts outside the assessment scope.
Do not force every cancer into one universal numbered staging system.

## Study search, ordering, and import

Use `https://clinicaltrials.gov/api/v2/studies` with a fixed condition search
`query.cond=cancer`. Include interventional and observational studies, with
`RECRUITING` or `NOT_YET_RECRUITING` status. Expanded access is excluded. Apply no
pediatric/adult or geography restriction. Search/filter controls are not editable
by application users.

Use explicit API relevance ordering, `sort=@relevance`, consistently across pages.
This is relevance to the oncology search, not to a patient. It does not establish
cancer-type alignment. Relevance was preferred over update-date ordering because
the purpose is to retrieve oncology-related research, not recently edited records.
Do not sort imports by update date, NCT identifier, or an invented local score.
Do not manipulate page selection to force age balance or favorable matches.

Initial import and Load Next 10 each request exactly one page with `pageSize=10`.
The API may return fewer records. Preserve search, filters, sorting, and page size;
pass a response's `nextPageToken` as the next request's `pageToken`. Deduplicate
using NCT identifiers. Skip duplicates without fetching replacement pages.
Maintain all imported studies in memory.

Import history includes fetched/imported/duplicate counts and relevant NCT IDs.
Retain duplicate-only batches with zero imported studies and no matching action.
A failed attempt preserves existing data and pagination position, supports an
explicit retry, and is recorded without fabricated fetched/imported counts.
When no next-page token remains, show exhaustion and disable Load Next 10.
Prevent concurrent imports from corrupting pagination state; the implementation
mechanism belongs in design, not a new product feature.

A broad search can return unexpected records. Neither relevance nor an age-
unrestricted search guarantees pediatric representation or meaningful alignment
on the first page. Consistent parameters do not freeze the live registry between
requests. No automatic refresh is implied.

## Dashboard and matching operations

The dashboard is the Operator's central place to import studies, inspect batch
summaries, explicitly start matching, and view progress/summaries. Import does not
automatically start inference. A batch evaluates its newly imported studies
against all 10 patients. A duplicate-only page produces no new pairs.

Multiple batches may await matching. Only one matching operation is active across
the application; other users may browse stored results. LLM requests are
sequential initially. New imports do not change an active batch's pair set.

There is one matching mechanism. Patients and Studies are read perspectives on
the same stored pair assessments, including all three outcomes and explanations.
Neither view starts matching or offers Match Again. Completed assessments are
snapshots; later imports leave them untouched.

## Assessment scope and outcomes

Recruitment status is a deterministic import filter. Both accepted statuses are
suitable for screening; Not Yet Recruiting does not count against the patient.

Use structured age/sex eligibility as the operational authority for deterministic
pair checks. A clear failure produces an explained Nonmatch and skips inference.
Missing, invalid, or unsupported information must not become an automatic
exclusion or satisfied criterion. Passing prefiltering only permits further
assessment. Reliable parsing rules and exact contracts remain design work.

For remaining pairs, the LLM interprets in-scope cancer-type, stage, and disease-
state requirements using supplied facts and study text. Evaluate a restriction
when the study specifies it. No restriction identified is not the same as an
unknown patient value or ambiguous study wording. Preserve that distinction.

| Overall outcome | Meaning within the limited scope |
| --- | --- |
| Potential match | Supported alignment across assessed criteria, without an established in-scope exclusion |
| Nonmatch | Reliable evidence establishes an in-scope exclusion |
| Insufficient information | Missing or ambiguous information prevents an in-scope conclusion |

No numerical score. Retain criterion-level requirements, applicable supplied
patient values, evaluation outcomes, explanations, and source references. Exact
schema and aggregation/validation contracts require further design review.

Biomarkers, laboratory results, treatment history, and unselected eligibility
requirements are outside the focused assessment. Consent, willingness, and final
enrollment/investigator approval are downstream human steps; POC 1 implements no
fields, checks, or workflows for them. Their absence does not automatically block
a Potential match. Every result must clearly disclose the assessment limits.

If an out-of-scope condition determines which in-scope cohort rule applies and
the applicable rule cannot be established, report Insufficient information rather
than ignoring the condition or choosing the favorable cohort. Another clear
in-scope exclusion can still establish Nonmatch. Do not add out-of-scope patient
attributes merely to force a conclusion.

## Inference, evidence validation, and recovery

The inference endpoint and model name are configurable. Qwen3 8B is an initial
candidate, not a validated choice. Prefer one simple integration, not a provider
framework. Native Ollama versus OpenAI-compatible requests remains open for
evaluation. A compatible API format does not require OpenAI cloud inference.
Other server/model combinations are not promised compatible merely because their
address and model can be configured. Cloud inference is not approved.

Validate structured responses and required evidence references/values mechanically
where possible. Reject malformed or mechanically invalid evidence as a failed
evaluation; valid JSON is not proof of correctness. Authentic evidence may still
be misinterpreted, and POC 1 cannot detect every hallucination or wrong outcome.
Completed incorrect assessments are not given a correction/rematching workflow.

Failure is an execution state, separate from the three assessment outcomes.
Retain completed work if a pair times out or returns invalid output; record that
attempt as failed and continue remaining work. If inference is unavailable at the
outset, stop clearly. Exact failure classification remains design work.

Operators may explicitly retry failed evaluations and continue unfinished pairs
after cancellation. Completed assessments, including Insufficient information,
are not eligible for retry. No automatic retries or recovery jobs.

Matching stays attached to the initiating dashboard request, which streams
progress and completed pair results. Show completed/total counts rather than
unvalidated time estimates or raw model reasoning. Cancel, logout, leaving the
dashboard, or closing/disconnecting stops further work once detected; request
cancellation of active inference. Logout must explicitly cancel the initiating
operation rather than assume cookie invalidation ends an active request.

Retain completed assessments; do not save an interrupted pair as completed.
Continue starts unfinished evaluations anew, not halfway through generated text.
The dashboard must remain open; execution independent of the request is outside
POC 1. Streaming transport details and timeout/proxy behavior need design review.

## Testing and evaluation

Introduce xUnit business-rule tests, WebApplicationFactory API tests, Vitest/React
Testing Library behavior tests, and Playwright workflows alongside relevant
features. Agree testing boundaries before implementation. Use deterministic
fixtures and mock ClinicalTrials.gov/Ollama for automated tests. Fixtures do not
replace or supplement live imports to force a desired mix.

The approved inference evaluation plan covers clear alignment/exclusions, missing
facts, equivalent stage wording, disease state, absent restrictions, ambiguous
cohorts, scope boundaries, invented evidence, malformed output, and service
failures. Evaluate outcome/evidence quality, uncertainty, repeated-run consistency,
and latency. Propose independently reviewed expected results and acceptance
thresholds before running experiments. Compare native/compatible endpoints on
the same examples; measure hardware use and context needs rather than assuming
performance. Running experiments is not yet authorized.

## First development slice

Approved direction: start both applications, log in with a predefined demo user,
browse the 10 synthetic patients, open details, and log out so protected access
is no longer available. Both roles may browse. Meaningful Operator-only operation
tests arrive with importing/matching. Detailed specification and testing boundaries
are pending approval. No implementation is authorized by this baseline.

## Documentation and future boundaries

Deliver practical setup instructions covering prerequisites, backend/frontend,
demo login, tests, Ollama/model setup, editable inference settings, troubleshooting,
tested combinations, and in-memory lifetime. Document the search/sort rationale,
assessment limits, known error risks, AI assistance, and human review accurately.
Private machine details are not project documentation. Revisit licensing before
POC 1 release and final README/setup documentation.

| Outside POC 1 | Possible future direction, not a commitment |
| --- | --- |
| Refresh, change detection, stale assessments, automatic/selective reassessment | Provisional POC 2 exploration |
| Persistence, background execution, scheduling, queues, durable recovery, scale | Provisional POC 3 exploration where justified |
| Structured/narrative consistency checking | Evaluate a study-level check once per study; measure overhead and correctness |
| Broader cohort and clinical eligibility assessment | Explicit cohort rules, additional patient facts, and professional review |
| Independent execution after dashboard disconnection | Reconnecting to progress with an explicitly expanded execution model |
| Additional inference servers, mobile clients, agent-based matching, interoperability | Separate future decisions |

Do not assign unapproved improvements to a release. Source contradictions may
cause incorrect exclusions in POC 1; sending everything to an LLM is not assumed
to solve them. Fixes required for approved POC 1 behavior remain in POC 1 scope.

## Primary references from discovery

- [ClinicalTrials.gov API](https://clinicaltrials.gov/data-api/api)
- [ClinicalTrials.gov study definitions](https://clinicaltrials.gov/policy/protocol-definitions)
- [NCI cancer staging](https://www.cancer.gov/about-cancer/diagnosis-staging/staging)
- [ASP.NET Core cookie authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0)
- [Ollama structured output](https://docs.ollama.com/capabilities/structured-outputs)
- [Ollama compatible API](https://docs.ollama.com/api/openai-compatibility)
- [LM Studio structured output](https://lmstudio.ai/docs/developer/openai-compat/structured-output)
- [GitHub licensing guidance](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository)
