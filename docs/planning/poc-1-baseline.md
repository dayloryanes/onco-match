# OncoMatch POC 1 planning baseline

Approved planning baseline recorded on 2026-10-27. This is the current scope and
decision record, not a detailed implementation specification. Read the development
overview for current authorization. Remaining decisions live in the Wayfinder map;
do not duplicate their eventual resolutions across competing planning records.

Local revision, 2026-10-30: incorporates the focused discussion for
[scope decision #12](https://github.com/dayloryanes/onco-match/issues/12).
The human approved local document preparation, not implementation or publication.
The [condition-label assessment](condition-label-assessment-design.md) and
[evaluation](condition-label-evaluation-design.md) records contain the contract
and approved exact fixtures. GitHub records await separately approved
publication; the old narrative designs and fixtures remain historical evidence.

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

Load exactly 10 fictional patient records from JSON into memory. The delivered
login/patient-browsing slice already supplies these pediatric and adult records.
They remain unchanged by this scope revision. Separate evaluation fixtures do not
augment application patients; live pages cannot guarantee an outcome distribution.

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
the same stored pair assessments, including pair summaries and explanations.
Neither view starts matching or offers Match Again. Completed assessments are
snapshots; later imports leave them untouched.

## Assessment scope and pair summaries

Recruitment status is a deterministic import filter. Both accepted statuses are
suitable for screening; Not Yet Recruiting does not count against the patient.

Use structured age/sex eligibility as the operational authority for deterministic
pair checks. Compute both checks. A reliable exclusion produces Excluded by
age/sex and skips inference. Otherwise continue, even if either or both checks
are Undetermined. AI does not resolve or override these structured checks.
Missing, invalid, or unsupported information must not become an automatic
exclusion or satisfied criterion. Passing prefiltering only permits further
assessment. Previously reviewed conservative age/sex rules are retained; exact
calendar-arithmetic implementation remains later specification work.

For remaining pairs with condition text, one AI request interprets all complete
condition labels against supplied cancer type, stage/classification, and disease
state. Each label has separate findings and a complete-label result: Alignment
supported, Conflict identified, or Undetermined. Explicit AND/OR relationships
within a label retain their meaning; a failed alternative does not defeat a
satisfied OR alternative. Separate labels do not establish an eligibility AND/OR
relationship. Stage-at-diagnosis facts do not establish current disease extent.

No eligibility narrative, titles, summaries, detailed descriptions, study-population
narrative, or gender descriptions are supplied to resolve label ambiguity.
Absent qualifiers are Not specified in condition labels, not evidence of no
restriction elsewhere. Missing necessary facts and ambiguous staging contexts
remain Undetermined. Preserve unsupported qualifiers as Not assessed.

Current metastatic status is not an assessed patient attribute. Preserve an
explicit Metastatic label qualifier as unassessed rather than inferring it from
recurrence or automatically mapping it to stage at diagnosis. Without an explicit
in-scope stage value/classification, the stage finding is Not specified; the
extent wording remains visible in the qualifier evidence and explanation.

Code computes the pair summary after valid complete-label results:

| Pair summary | Meaning within the limited scope |
| --- | --- |
| Excluded by age/sex | At least one reliable structured exclusion; AI skipped |
| Condition-label alignment supported | All supplied complete labels support alignment within scope |
| Mixed condition-label findings | At least one complete label aligns and another conflicts or is undetermined |
| Condition-label conflicts identified | Every complete label reliably conflicts; not full-study exclusion |
| Condition-label alignment undetermined | No label aligns and at least one is undetermined, or no condition text is supplied without a reliable age/sex exclusion |

Structured uncertainty and unassessed qualifiers remain visible beside every
applicable summary. No numerical score. Retain exact input snapshots, structured
checks, per-label/criterion findings, explicit relationships, explanations, and
source references. Missing/empty/all-blank condition text skips AI and is not an
execution failure. Exact malformed-import handling remains slice design work.

Biomarkers, laboratory results, treatment history, and unselected eligibility
requirements are outside the focused assessment. Consent, willingness, and final
enrollment/investigator approval are downstream human steps; POC 1 implements no
fields, checks, or workflows for them. Their absence does not automatically block
label alignment within scope. Every result must clearly disclose assessment limits.

If an out-of-scope condition determines which in-scope cohort rule applies and
the applicable rule cannot be established, report an Undetermined label result rather
than ignoring the condition or choosing the favorable cohort. Another clear
independent applicable conflict can still establish a label conflict. Do not add out-of-scope patient
attributes merely to force a conclusion.

The agreed working notice is:

> This assessment checks structured age and sex requirements and compares cancer type, stage, and disease state with the study's listed conditions. Biomarkers, treatment history, other requirements, and free-text eligibility criteria are not assessed. Results do not establish clinical eligibility or enrollment approval.

Exact UI placement and later wording refinements are deferred to results UI
design. No popup, acknowledgement, or list-row reminder is required by this decision.

## Inference, evidence validation, and recovery

The inference endpoint and model name are configurable. No model has been selected;
the completed narrative trials did not meet their quality gate and do not validate
the narrowed task. Prefer one simple integration, not a provider
framework. Native Ollama versus OpenAI-compatible requests remains open for
evaluation. A compatible API format does not require OpenAI cloud inference.
Other server/model combinations are not promised compatible merely because their
address and model can be configured. Cloud inference is not approved.

Validate structured responses and required evidence references/values mechanically
where possible. Reject malformed or mechanically invalid evidence as a failed
evaluation; valid JSON is not proof of correctness. Authentic evidence may still
be misinterpreted, and POC 1 cannot detect every hallucination or wrong outcome.
Completed incorrect assessments are not given a correction/rematching workflow.

Failure is an execution state, separate from completed pair summaries.
Retain completed work if a pair times out or returns invalid output; record that
attempt as failed and continue remaining work. If inference is unavailable at the
outset, stop clearly. Reject the whole invalid model response without salvaging
partial label findings; retain completed age/sex checks. No automatic repair.

Operators may explicitly retry failed evaluations and continue unfinished pairs
after cancellation. Completed assessments, including undetermined or mixed summaries,
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

The initial discussion agreed 16 development cases and 16 separate acceptance
cases (12 controlled and 4 frozen real-study cases), with three acceptance
repetitions: 48 responses per frozen candidate configuration. The human then
requested fresh analysis of real-study coverage rather than inheriting four from
the historical plan. An approved bounded lookup retrieved 120 distinct studies
across 12 pages. The human approved the revised allocation of seven varied
real-study cases, making 19 acceptance cases and 57 responses per configuration.
All scored responses must meet the structure, evidence,
interpretation, and uncertainty expectations. All 35 case expectations were
reviewed in discussion, and the final written fixture records and serialized
examples were approved. No execution is authorized by that approval.

Keep acceptance reserved; using its failures to guide changes retires those cases
from acceptance. Old narrative results do not become new-scope passes. Controlled
application checks are separate from model-quality requests. Measure a separately
approved small development run before agreeing a numerical latency limit; freeze
that limit, deadline, prompt, model, and settings before acceptance. Record request
durations, median, slowest request, and total time including failures.
Native/compatible-route selection remains open; equivalent inputs and controls
must be reviewed before a comparison. Settings, budgets, downloads, research, and
execution require separate approval. No new experiment is authorized here.

## First development slice

Delivered through [PR #9](https://github.com/dayloryanes/onco-match/pull/9): start
both applications, log in with a predefined demo user, browse the 10 synthetic
patients, open details, and log out so protected access is no longer available.
Both roles may browse. Meaningful Operator-only operation tests arrive with
importing/matching. This scope revision changes no delivered application behavior
and authorizes no further implementation.

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
| Narrative eligibility interpretation | Independent scope and quality evaluation; no promised release |
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
