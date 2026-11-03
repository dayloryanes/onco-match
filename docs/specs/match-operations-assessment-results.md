# Match operations and assessment results

Status: approved specification and four-slice implementation plan, 2026-11-02.
The human approved this specification for GitHub issue publication. Individual
implementation-ticket publication, starting implementation, experiments, live
inference, commits, and merges remain separately gated. No application
functionality has been implemented by this planning work.

Canonical specification: [Match operations and assessment results](https://github.com/dayloryanes/onco-match/issues/23).

Development entry point: [OncoMatch POC 1 — Task development overview](https://github.com/dayloryanes/onco-match/issues/1).
Remaining decision index: [Resolve remaining OncoMatch POC 1 decisions](https://github.com/dayloryanes/onco-match/issues/2).

## Problem Statement

Operators can import studies, and both demonstration roles can browse patients
and studies. They cannot yet run the agreed focused assessments or inspect
stored pair results in the application. The evaluation runner is separate tooling.

## Solution

Specify the complete application match workflow before agreeing smaller
implementation slices: explicit batch execution, progress, cancellation,
continuation, failed-attempt retries, and stored-result inspection from Dashboard,
Patient, and Study views. A complete specification does not authorize building
the whole workflow in one slice.

Existing assessment semantics and scope remain defined by the
[POC baseline](../planning/poc-1-baseline.md),
[assessment contract](../planning/condition-label-assessment-design.md), and
[glossary](../../GLOSSARY.md). The
[development baseline report](../evaluation/condition-label-baseline.md)
records the selected model configuration and quality limitations; route support
does not establish acceptance or route equivalence.

## User Stories

1. As an Operator, I want each completed study import to identify its fixed patient–study pairs, so that later imports do not change the work I started.
2. As an Operator, I want to explicitly start a match operation, so that importing studies never starts inference without my action.
3. As a user, I want zero-import batches to show No work, so that duplicate-only pages do not offer an empty match action.
4. As an Operator, I want the API to reject stale or concurrent actions, so that multiple sessions cannot process conflicting work.
5. As an Operator, I want both structured age and sex checks retained, so that a reliable exclusion can skip inference without hiding the other check.
6. As a user, I want age uncertainty preserved when whole-year patient data cannot establish a finer comparison, so that missing precision is not invented.
7. As a user, I want invalid and unsupported study age representations explained, so that they do not silently become passes or exclusions.
8. As a user, I want nonexcluded pairs assessed against complete study condition labels, so that explicit relationships and unsupported qualifiers remain visible.
9. As a user, I want absent condition text distinguished from an execution failure, so that no unnecessary inference or retry is offered.
10. As a user, I want authentic evidence references and exact input snapshots retained, so that findings can be traced to the supplied facts.
11. As a user, I want the application to calculate pair summaries from validated findings, so that the model does not choose an unsupported overall score.
12. As an Operator, I want sequential inference with a configurable request timeout, so that a slow pair has a bounded attempt without starting parallel work.
13. As an Operator, I want completed/total, failed, unfinished, and active counts, so that I can see progress and outstanding recovery needs.
14. As an Operator, I want completed results streamed to Dashboard, so that I can inspect progress before the whole operation finishes.
15. As an Operator, I want assessment details to open inside Dashboard, so that inspecting a completed result does not cancel the active operation.
16. As the initiating Operator, I want Cancel to stop further work and request cancellation of active inference, so that I control the current operation.
17. As a user, I want navigation, logout, disconnection, and session expiry to stop initiating work while preserving completed assessments, so that the operation has a clear lifetime.
18. As an Operator, I want Continue unfinished to process only never-completed or interrupted pairs, so that I do not repeat completed assessments.
19. As an Operator, I want Retry failed to make one new attempt per failed pair, so that recovery is explicit and never loops automatically.
20. As an Operator, I want repeated failures to remain visible and manually retryable, so that I can investigate them without losing successful results.
21. As a user, I want a compact attempt history, so that I can understand earlier failures and subsequent recovery.
22. As a user, I want a lost progress connection reconciled against stored backend state, so that a missing browser message does not cause completed work to repeat.
23. As a user, I want stored assessments to be shared across demonstration users, so that different views show the same results.
24. As a Viewer, I want to read results but not start, continue, retry, or cancel match operations, so that API permissions remain meaningful.
25. As an independently signed-in user, I want another session's logout or cancellation not to stop my work, so that session boundaries are respected.
26. As an Operator, I want accepted Start/Continue/Retry actions to grant a fresh 30-minute session, so that I do not begin with only a few minutes remaining.
27. As a user, I want a five-minute expiry warning and Stay signed in action, so that I can deliberately extend a long operation without unattended keepalive.
28. As a user, I want tabs sharing one login to share renewals and logout, so that operation deadlines follow the same browser session.
29. As a user, I want valid focus checks to preserve active work, so that switching focus does not itself cancel an operation.
30. As a user, I want results in Patient details grouped around that patient's study assessments, so that I can review the patient perspective.
31. As a user, I want results in Study details grouped around that study's patient assessments, so that I can review the study perspective.
32. As a user, I want a consistent assessment detail view with findings, evidence, uncertainty, and unassessed qualifiers, so that each entry is interpretable.
33. As a user, I want the assessment limits visible beside results, so that alignment within scope is not mistaken for clinical eligibility.
34. As a noninitiating user, I want manual Refresh to update shared progress, so that I can inspect current state without controlling another session's operation.
35. As someone configuring the application, I want to select native or OpenAI-compatible local Ollama through backend settings, so that I can use either supported route without editing client code.
36. As someone configuring the application, I want both route-specific model names recorded in advance, so that changing the route selects the intended model.
37. As a user, I want invalid inference configuration to disable only match actions with an actionable message, so that the delivered browsing/import features remain usable.
38. As a user, I want unavailable inference and invalid model output reported as execution problems, so that they do not produce fabricated assessment conclusions.
39. As a maintainer, I want application code organized inside the Match feature and independent of the runner, so that experiments do not silently change application behavior.
40. As a maintainer, I want meaningful automated tests with fake external services, so that lifecycle and contract behavior can be checked without live inference or acceptance fixtures.
41. As someone setting up the application, I want documented prerequisites, configuration, both route setups, in-memory lifetime, and known limits, so that I can run and assess the POC accurately.

## Implementation Decisions

### Application ownership

- Application functionality belongs inside the existing API project. The Match
  feature owns its application behavior and assessment contract; do not place
  application-owned code in a repository-level shared folder.
- Code genuinely shared by API features may live in an appropriate shared or
  infrastructure area inside that API project. Do not add a production project
  merely to share files with the evaluation runner.
- Keep the evaluation runner independent, treating it as a parallel evaluation
  project. Do not source-link application files into the runner, reference its
  executable from the API, or introduce live shared application/runner code.
- Use the runner as a reference for reviewed request formats and assessment
  behavior, not as the application's architecture or a template to copy wholesale.
  Design the API's feature boundaries, types, configuration, lifecycle, and tests
  for application needs. Reuse or adapt individual proven pieces only where they
  fit that design, through an explicit approved application change.
- Later experiments remain in the independent runner until adoption is separately
  reviewed. Runner fixtures, expected answers, execution guards, and reporting
  remain separate from application inputs and runtime behavior. This deliberately
  trades some duplicated logic for isolation; application contract/parity tests
  should make unintended differences visible.
- Keep the Ollama integration within the Match feature for its current sole
  application consumer. Do not introduce an API-wide infrastructure location
  based only on hypothetical future reuse.
- Experimental prompt, schema, validator, or summary changes must not silently
  alter the application's approved behavior or overwrite retained evaluation
  baselines. Preserve frozen runner assets when adapting application logic.

### Application condition-label contract names

- Use `conditionLabels` for the array of condition-label results and
  `conditionLabelId` for each entry's supplied condition-label source identifier
  in the new application's model-response JSON. These replace `labels` and
  `labelId` from the earlier assessment serialization design for this application
  contract; the assessment meanings and source-identity rules remain unchanged.
- Use corresponding descriptive application names where appropriate, including
  `conditionLabel`, `conditionLabels`, and `conditionLabelId`. The application's
  schema, model instructions, validation, serialized examples, and tests must
  agree on the new contract names.
- This is an explicitly approved application serialization change relative to
  the linked assessment contract. Preserve historical evaluation assets and
  results; they do not by themselves validate the newly named model contract.

For example, a supplied patient cancer type of Breast cancer and a single study
condition Breast cancer can produce the following model content. This illustrates
the new field names; it does not add a patient or evaluation fixture. Structured
age/sex checks remain application-owned and outside this model response.

```json
{
  "conditionLabels": [
    {
      "conditionLabelId": "study.conditions.0",
      "result": "alignmentSupported",
      "requirementSummary": "Breast cancer; no explicit stage or disease-state qualifier is stated.",
      "cancerType": {
        "result": "alignmentSupported",
        "patientEvidence": [{ "sourceId": "patient.cancerType", "value": "Breast cancer" }],
        "studyEvidence": [{ "sourceId": "study.conditions.0", "quote": "Breast cancer" }],
        "examinedSources": ["study.conditions.0"],
        "explanation": "The supplied cancer type corresponds to the condition label."
      },
      "stage": {
        "result": "notSpecified",
        "patientEvidence": [],
        "studyEvidence": [],
        "examinedSources": ["study.conditions.0"],
        "explanation": "Stage is not specified in this condition label."
      },
      "diseaseState": {
        "result": "notSpecified",
        "patientEvidence": [],
        "studyEvidence": [],
        "examinedSources": ["study.conditions.0"],
        "explanation": "Disease state is not specified in this condition label."
      },
      "unassessedQualifiers": [],
      "explanation": "Cancer-type alignment is supported within the assessed scope."
    }
  ]
}
```

### Configuration

- Support both local Ollama routes: `native` and `openai-compatible`.
- Select route, endpoint, and model through backend configuration; changes take
  effect after restarting the backend. Native is the default route.
- Use one shared match workflow and assessment behavior, with route-specific
  request/response handling. Do not introduce a general provider framework.
- Document setup for both supported routes, including the compatible model
  preset requirements. OpenAI-compatible describes the API format, not approved
  OpenAI cloud use or support for arbitrary compatible servers.
- Missing or invalid required inference configuration leaves login, patient
  browsing, study importing, and study browsing usable. Disable match actions
  with an actionable configuration message; do not silently choose another
  route/model. Valid configuration with an unavailable server is a separate
  outset inference failure under the existing assessment lifecycle rules.

#### Configuration keys

- Bind the API configuration section `Match:Inference` to a feature-owned
  `MatchInferenceOptions` class in Match/Inference. Program registers the options
  and client; the Match feature owns route selection and request construction.
- Fields: `Route` (native by default), `BaseUrl`, `NativeModel`,
  `OpenAiCompatibleModel`, and `RequestTimeoutSeconds` (600 by default).
  Both model names can be configured in advance, so changing Route selects its
  corresponding model without editing a request template or client source.
- The native route selects /api/chat; the compatible route selects
  /v1/chat/completions on the configured local Ollama base URL. Do not infer
  the route from the model name or silently fall back to the other route.
- Read and validate the selected configuration once at API startup. Hold that
  configuration for the process lifetime; file reload must not change it mid-run.
  A validation failure disables the Match feature without aborting API startup.
- Normal API configuration mechanisms remain available, including appsettings
  and environment-variable overrides such as `Match__Inference__Route`.
  The selected configuration is retained with attempt/assessment identity.

Initial configuration example:

```json
{
  "Match": {
    "Inference": {
      "Route": "native",
      "BaseUrl": "http://localhost:11434",
      "NativeModel": "gpt-oss:20b",
      "OpenAiCompatibleModel": "oncomatch-gpt-oss-20b-openai-compatible:latest",
      "RequestTimeoutSeconds": 600
    }
  }
}
```

Retained generation and execution controls:

- Initial generation controls retain the reviewed development configuration:
  low reasoning, context 24576, combined generation allowance 8192, framing
  reserve 4096, temperature 0.7, top-p 0.8, top-k 20, min-p 0, seed 42,
  repeat penalty 1, presence penalty 0, and keep-alive five minutes. These are
  inherited working controls, not an application quality or latency acceptance pass.
- Native requests send the reviewed options and response schema through the
  native chat API. Compatible requests use structured response_format,
  reasoning_effort low, max_tokens 8192, temperature/top-p/seed/presence penalty,
  and Ollama's keep_alive extension. The compatible preset supplies context,
  top-k, min-p, and repeat penalty; do not invent compatible API fields for them.
- Both routes use nonstreaming model answers and the same new application
  conditionLabels/conditionLabelId contract. The Dashboard stream is independent
  of model token streaming. Adapt instruction/schema identifiers consistently;
  do not alter the agreed interpretation policy as incidental implementation work.
- At the first need for inference in an operation, verify local service/model
  availability and the required selected-profile controls without generating an
  answer. Use the configured request timeout for that HTTP check. If unavailable,
  stop clearly without fabricating pair results or starting subsequent requests.
  An operation whose selected pairs all complete through structured/no-text
  routing needs no inference call. Retain completed work if a later first need
  for inference cannot proceed.
- Reject truncated/nonfinal provider envelopes, tool/refusal output, malformed
  JSON, duplicate keys, invalid coverage, or mechanically invalid evidence as
  specified by the assessment contract. Use a conservative context-input guard
  with the retained framing/output allowances; oversized inputs fail without
  truncation. Endpoint/model configurability does not certify other combinations.
- Restarting the backend to change configuration also clears application memory,
  as already agreed. Document that users must reimport studies after restart.

### Structured age input formats

- Recognize nonnegative whole-number study age bounds in the six agreed units:
  years, months, weeks, days, hours, and minutes. Accept ordinary capitalization
  differences and surrounding whitespace; preserve the original source text.
- Unsupported representations, including fractional values such as 18.5 Years,
  missing text, and unknown units remain Undetermined. Do not infer a bound or
  coerce an unsupported representation into an automatic pass or exclusion.
- Exact calendar arithmetic, numeric limits, mixed-unit interval validation,
  and boundary expectations are specified below under Structured-age calculation.

### Control and recovery

- A match batch is the fixed pair collection for one completed study import;
  use the existing import identifier as its batch identifier. Each accepted
  Start/Continue/Retry creates a distinct match operation. An evaluation
  attempt identifies one try at processing one pair, not an individual-pair
  execution control. See the glossary for canonical definitions.
- Only the initiating session can explicitly cancel an active operation.
  Other sessions may view progress and stored results.
- Once the operation stops, any Operator may continue unfinished pairs or retry
  failed evaluations. The existing single-active-operation rule remains.
- Provide separate batch-wide actions: Continue unfinished and Retry failed.
  Continue handles never-completed/interrupted pairs; Retry failed processes
  each currently failed pair once. Neither action reprocesses completed
  assessments. Individual failed-pair selection is not included in this draft.
- A retry operation makes one new attempt for each selected failed pair and
  then ends. Repeated failures remain failed and eligible for another explicit
  batch-wide retry; do not add an automatic retry loop or arbitrary manual retry
  limit. Retain successful assessments without repeating their inference.
- Retain a compact history of evaluation attempts for each pair, including when
  each ran, whether it completed/failed/was interrupted, and any failure reason.
  The pair's current status reflects the latest attempt, while earlier attempts
  remain inspectable. A completed assessment remains immutable and is never
  selected for another attempt by Continue unfinished or Retry failed.
- When no unfinished pairs remain but failures do, show Finished with failures,
  the completed/failed counts, failure reasons, and Retry failed. The Operator
  may leave the batch in this state and continue using the application.
- Leaving the initiating Dashboard stops its operation. Returning shows retained
  state and requires explicit continuation; it does not automatically restart.
- After a lost connection, reconcile saved backend state before offering recovery.
  Wait until the prior operation has stopped before admitting new work. A lost
  connection interrupts active work; it does not itself turn an interrupted pair
  into a failed model evaluation.
- Use a configurable per-inference-request timeout, initially 600 seconds.
  Timeout fails that pair's attempt and processing continues to remaining pairs.
  This is an operational limit, not an agreed latency acceptance criterion.
- Do not add a separate whole-operation time limit. A finite batch may finish
  while the initiating session remains valid, subject to request timeouts,
  cancellation, and existing stop conditions.

### Session lifetime

- An accepted Start match, Continue unfinished, or Retry failed action renews
  the initiating session for a fresh 30 minutes, even before the normal sliding
  renewal threshold. This retains the initiating session identity.
- Ordinary authenticated use retains the existing sliding-renewal rules.
- Five minutes before expiry, show a nonblocking banner with the remaining time
  and Stay signed in. A successful explicit extension grants another 30 minutes
  without interrupting the operation and dismisses the warning. An already expired
  session requires login rather than extension.
- Progress delivery alone must not renew the session; do not add background
  requests that keep unattended sessions alive.
- Actual session expiry cancels the active match operation, honoring successful renewals.
  Logout, leaving the initiating Dashboard, and disconnection retain their
  agreed cancellation behavior.
- Preserve completed assessments; request cancellation of active inference,
  start no further pairs, and leave interrupted work unfinished. After login,
  continuation starts unfinished pairs anew.
- This extends the delivered authentication design for long-running work.
  Backend expiry tracking, renewal coordination, and race handling follow the
  session contract below. Do not add a universal mouse/keyboard inactivity timer.

### Results and progress

- Dashboard, Patient, and Study views show compact pair summaries and open a
  shared assessment detail view containing structured checks, every condition
  label finding, evidence, explanations, and unassessed qualifiers.
- Keep the assessment-limits notice visible in the results context.
- Initial notice placement: immediately above the assessments in the Dashboard
  batch results section, the Patient details study-assessment section, and the
  Study details patient-assessment section. These are sections of existing pages,
  not a new Results page. No notice is required on the patient/study directories.
- Start with the application's existing subdued explanatory-text treatment;
  review appearance in the working UI. Do not add a popup or acknowledgement.
- Use the agreed working notice, with visual refinements reviewed in the UI:

> This assessment checks structured age and sex requirements and compares cancer type, stage, and disease state with the study's listed conditions. Biomarkers, treatment history, other requirements, and free-text eligibility criteria are not assessed. Results do not establish clinical eligibility or enrollment approval.

- Open assessment details within the Dashboard so inspecting completed results
  does not navigate away or cancel its active operation. Navigating to Patient
  or Study pages still leaves the Dashboard and cancels its operation.
- Show completed assessments / total pairs, with failed, unfinished, and active
  pair counts separately. A valid undetermined assessment counts as completed;
  an execution failure does not.
- Illustrative counts: 70/100 completed, 5 failed, 24 unfinished, 1 active.
  These are an explanation of the agreed presentation, not evaluation fixtures.

### Progress transport

- One initiating browser-to-backend request stays open for the match operation
  and streams small JSON progress/result updates as they become available.
- The backend makes separate sequential inference requests for pairs requiring
  AI; it does not send the entire batch as one model request. Physical HTTP
  connection reuse is separate from the inference-request boundary.
- Disconnection of the initiating request signals cancellation. Explicit
  continuation/retry uses a new request and operation; no automatic restart.
- Event framing, proxy delivery, and read-only state follow the detailed
  contracts below and must be demonstrated by automated checks during implementation.
- Use one JSON message per line for the initiating response. Messages identify
  operation start, pair start, pair completion/failure, and operation finish/stop,
  with current progress counts. Send a small connection heartbeat every 15 seconds
  while waiting; it must not renew authentication or restart an operation.
- Completed-pair messages carry the stored summary; opening assessment details
  retrieves the full retained assessment through its read endpoint.
- Non-initiating sessions use manual Refresh to update progress initially;
  only the initiating Dashboard receives live operation updates. Do not add
  another live subscription mechanism or automatic polling for observers.

### API routes

The human approved the route structure below. Browser requests use relative URLs
through the existing Vite proxy. Retain existing API authentication, role checks,
CSRF protection for mutations, and Problem Details conventions.

| Method and route | Responsibility |
| --- | --- |
| GET /api/match/batches | List match batches, counts, and any active operation. |
| GET /api/match/batches/{batchId} | Read a batch's current state and patient–study pair summaries. |
| POST /api/match/batches/{batchId}/operations | Start, continue, or retry failed work; stream progress/results on this response. |
| POST /api/match/operations/{operationId}/cancel | Request cancellation of that exact operation from its initiating session. |
| GET /api/match/pairs?patientId=… or nctId=… | List pair states/results for the Patient or Study perspective. |
| GET /api/match/pairs/{patientId}/{nctId} | Read one pair's state and detailed stored assessment, if completed. |

The operation request carries an explicit action (`start`, `continue`,
or `retryFailed`) and the batch state version last reviewed by the browser.
Admission atomically rejects stale state or another active match operation;
do not queue or silently reinterpret the requested action. Batch reads return
their version, counts, current operation, and role/session-appropriate actions.
Use patient ID plus NCT ID to identify a pair without conflating it with an
operation or evaluation-attempt identifier.

The detailed states/messages below complete this route outline. Other sessions
use manual Refresh. The human approved the complete specification for publication.

### Application state and read contracts

- Identify batches by the completed import identifier, operations by a newly
  generated opaque identifier, and pairs by patient ID plus NCT ID. Preserve
  import order for studies and the existing patient-ID order within each study.
- A completed import exposes one batch with its original newly imported NCT IDs
  and all ten application patients. Later imports do not add pairs to that batch.
  A zero-import page remains visible as No work, with zero pairs and no actions.
  Failed/cancelled imports do not create a selectable match batch.
- Batch states: `noWork`, `notStarted`, `running`, `stopping`,
  `unfinished`, `finishedWithFailures`, and `completed`. Running/stopping take
  precedence; when idle, unfinished pairs take precedence over failures.
  Completed requires every pair to have a valid completed assessment.
  NotStarted means no operation has yet been admitted for a nonempty batch;
  after an admitted operation stops, remaining unfinished pairs use Continue,
  including when inference was unavailable before the first model request.
- Pair states: `unfinished`, `running`, `failed`, `completed`. An
  interrupted attempt returns its pair to unfinished, including when it was a
  retry; retain the earlier failure in attempt history. Do not turn cancellation
  into a failed model interpretation or a completed undetermined assessment.
- Attempt history carries `id`, `operationId`, `startedAt`, `finishedAt`,
  `status` (`running`, `completed`, `failed`, `interrupted`), `failureCode`, and
  a safe failure explanation. Retain available route/model/configuration identity
  and elapsed time without publishing raw reasoning, endpoint credentials, or
  provider error bodies. Result summaries never substitute for execution states.
- A batch response carries `batchId`, `version`, `status`, `totalPairCount`,
  `completedPairCount`, `failedPairCount`, `unfinishedPairCount`,
  `activePairCount`, `activeOperation`, `availableActions`, and pair summaries.
  The four pair counts sum to total; active is zero or one. List responses omit
  detailed evidence/snapshots but retain the batch identity and counts.
- `activeOperation` carries `operationId`, `action`, `startedAt`,
  `selectedPairCount`, and caller-specific `canCancel`. Do not expose session IDs.
  Reads return actions appropriate to both the caller's role and current state;
  the API independently checks authorization on every mutation.
- Pair summaries carry `batchId`, `patientId`, `nctId`, `state`, a nullable
  `pairSummary`, structured-check summaries where available, and the latest
  attempt metadata. Only completed pairs have a pair summary. A detailed pair
  response additionally contains attempt history and any retained input snapshot
  and structured findings, including for failed/interrupted work. Completed
  responses add condition-label results/evidence where assessed, explanations,
  unassessed qualifiers, and assessment-limits text. For reliably excluded or
  no-condition-text pairs, disclose why condition-label assessment was skipped.
- Pair-list reads require exactly one supported patientId or nctId filter and
  return corresponding pairs in stable source order. Unknown patients/studies
  return 404; invalid/combined unsupported filters return 400. Batch reads are
  the Dashboard's perspective; all views read the same stored assessments.
- Preserve original study condition positions, including blank entries; assign
  source identifiers from those original positions. Send only supported patient
  facts and nonblank condition labels to inference. Blank/missing condition text
  and structured exclusions follow the existing no-inference routing rules.

### Mutation and streaming contracts

- The operation POST body has exactly `action` (`start`, `continue`, or
  `retryFailed`) and nonempty `expectedVersion`. Reject unknown fields/values.
  Start selects a not-started batch, Continue selects unfinished pairs, and Retry
  selects currently failed pairs. Capture that operation's pair selection once.
- Admission atomically checks configuration, authorization, current batch version,
  legal action/nonempty selection, and the single-active-operation gate. No queue,
  automatic retry, automatic route fallback, or silently substituted action.
- Before streaming begins, use existing Problem Details conventions: 400 for an
  invalid request/CSRF, 401 for missing/expired authentication, 403 for the wrong
  role or a different session cancelling an operation, 404 for unknown resources,
  409 for stale state/active operation/no work for the action, and 503 with
  `inference_configuration_invalid` when Match configuration is unusable.
- Successful admission renews the current session, preserving its identity, and
  returns HTTP 200 with `application/x-ndjson`. Start response headers promptly,
  then emit UTF-8 JSON objects terminated by newlines. Do not buffer an entire
  operation in the server, Vite proxy, or browser. Keep no-store behavior and
  avoid response compression/buffering that delays progress delivery.
- Each message carries `type`, `operationId`, `batchId`, `sequence`, `serverTime`,
  the current batch `version`, and the four progress counts. Types are
  `operationStarted`, `pairStarted`, `pairCompleted`, `pairFailed`,
  `operationFinished`, `operationStopped`, and `heartbeat`.
- Pair messages also identify `patientId` and `nctId`. Completion includes the
  stored pair summary and structured-check summaries; failure includes a safe
  failure code/explanation. OperationStopped includes its stop reason. Terminal
  messages distinguish a run finishing its selected pairs from a whole batch
  becoming completed. Failed pairs do not prevent a run from finishing.
- Persist each state transition before emitting its message. Sequence is local
  to the operation, not a replay/resumption protocol. HTTP chunks may split a
  UTF-8 character or JSON line; the browser buffers incomplete data until a full
  message arrives. A broken stream, invalid message, or missing terminal message
  requires state reconciliation and explicit recovery, never resubmission.
- Once headers have started, report execution failures through stream/state
  records rather than trying to change the HTTP status. If the connection is
  already gone, authoritative state remains available through normal reads.
- Send a heartbeat at least every 15 seconds during otherwise quiet inference.
  A heartbeat is transport traffic, not an authentication request or proof of
  user activity. Other sessions use their existing manual Refresh behavior.
- The cancel POST has no action payload. A valid initiating Operator receives
  202 after requesting cancellation of an active operation, or 204 if that known
  operation already stopped. Keep the global gate until active work has unwound;
  do not start a second operation merely because cancellation was requested.
- Check operation identity, cancellation, and session validity before beginning
  each pair and again at the atomic result-publication boundary. A result committed
  before cancellation is preserved; an uncommitted result after cancellation is
  not completed. Request cancellation of active inference; do not claim that a
  remote server necessarily stops computation immediately when its caller aborts.
- Preserve the existing failure distinctions: unavailable inference at outset
  stops clearly; request timeout/invalid response/invalid evidence/input-too-large
  fail an attempt and continue; unexpected internal errors stop the operation.
  Do not salvage partial findings or create clinical outcomes from execution errors.

### Session renewal, expiry, and browser coordination

- Keep standard secure-cookie authentication and the existing per-login
  `session_id`. Use a small Auth-owned in-memory lifetime coordinator for active
  operation cancellation, not a replacement authentication scheme or ticket store.
  Framework cookie validation remains the authority for accepting requests.
- Expose `expiresAt` and `serverTime` with the existing current-user response and
  renewal responses. New endpoint: authenticated, CSRF-protected
  `POST /api/auth/extend-session`, available to both demonstration roles. It
  explicitly issues a fresh 30-minute ticket for the same identity/session.
- An accepted Start/Continue/Retry renews before starting the progress response.
  Rejected or expired actions do not explicitly renew. Ordinary authenticated
  requests retain standard sliding renewal; capture the expiry of a ticket actually
  issued by the cookie handler, rather than assuming every request adds 30 minutes.
  Include the resulting expiry/server time on authenticated responses so the UI
  can reflect renewal without making periodic keepalive requests.
- The backend coordinator tracks the initiating session's latest issued expiry
  and requests cancellation when it is reached. Successful renewal before that
  deadline moves it forward. Logout cancels its active work immediately. A late
  renewal cannot resurrect an operation already stopped; explicit continuation
  uses a new operation. Expiry/cancellation/publication races must be serialized.
- Tabs sharing the browser's authentication cookie share the same session:
  renewal in any such tab extends the operation's deadline; logout in any such
  tab cancels its work. Independently authenticated browsers do not extend or
  cancel one another's work. Closing/navigating the initiating Dashboard still
  cancels that operation even if another tab remains signed in.
- Display the five-minute warning using server-provided expiry/time, allowing
  for browser clock differences. Dismiss it only after a successful renewal.
  An expired extension receives 401 and requires login. On a network failure,
  retain the last confirmed deadline and show Retry; do not pretend it extended.
- Preserve existing focus checks: temporarily hiding Dashboard during a successful
  check must not tear down the active stream. Confirmed authentication loss or
  protected-page teardown cancels it. Do not introduce unattended polling or a
  mouse/keyboard idle timer. Cookie/lifetime metadata never enters model inputs.
- Configure and test actual cookie emission together with the lifetime coordinator.
  Do not approximate automatic renewal solely in frontend code or switch to a
  server-side revocation scheme as an incidental implementation shortcut.

### Structured-age calculation

- Parse invariant ASCII whole numbers from 0 through Int64.MaxValue, followed by
  one or more whitespace characters and a singular/plural recognized unit. Ignore
  ordinary case and outer whitespace. Signs, decimals, combined expressions,
  N/A, missing/blank strings, and larger/unrecognized values are unsupported.
  Preserve raw values and explain why a comparison is unresolved.
- Treat patient AgeYears=Y as completed years, with no birth date, birth time,
  annual ageing, or precise age inferred. Unknown/invalid patient age is unresolved.
  Completed ages in years are exactly Y; completed months range from 12Y to
  12Y+11. Year/month bounds compare those completed counts inclusively.
- For smaller units, calculate a safe calendar envelope over possible birth
  dates in one representative Gregorian 400-year cycle. These are mathematical
  comparison anchors, not invented patient dates or additional patient fields.
  Use direct anniversary arithmetic with .NET's Gregorian AddYears/AddMonths
  end-of-month behavior, including Feb 29 becoming Feb 28 in a non-leap year.
- For each anchor B, let D(n,B) be whole days from B to its nth anniversary.
  The global lower completed-day bound is min D(Y,B), and the upper is
  max D(Y+1,B)-1. Convert those envelopes to completed weeks by floor division
  by 7, and to hours/minutes using the exclusive upper anniversary boundary:
  lower=min D(Y,B)*unitFactor; upper=max D(Y+1,B)*unitFactor-1, with factors
  24 and 1440 respectively. Do not round a sub-year uncertainty into an exact age.
- Gregorian patterns repeat every 400 years, containing 146097 days. Reduce
  large year/month spans into whole cycles plus a bounded remainder before
  calendar arithmetic; use overflow-safe integer arithmetic for cycle totals and
  comparisons. No universal 365-day year, 30-day month, current-date anchoring,
  timezone/DST assumption, or silent overflow repair is permitted.
- For a minimum, all possible completed ages reaching it establishes Satisfied;
  all below it establishes Excluded; overlap remains Undetermined. For a maximum,
  all within it establishes Satisfied; all above it establishes Excluded; overlap
  remains Undetermined. A reliable bound exclusion can establish Excluded despite
  an unrecognized other bound, except when the interval is established invalid.
- Validate recognized minimum/maximum intervals before using either for exclusion.
  Compare the inclusive minimum threshold against the exclusive boundary after
  the maximum completed count, preserving each unit's calendar meaning. If the
  interval is empty for every possible calendar anchor, it is invalid as a whole.
  When mixed-unit validity itself varies
  with unknown calendar context, report age Undetermined rather than excluding
  from a potentially invalid interval. Missing/unrecognized bounds do not become
  unbounded, and do not by themselves invalidate an independently reliable bound.
- Compute sex separately: structured ALL is explicitly unrestricted; matching
  known restricted values satisfy; differing known values exclude; missing or
  unsupported values/unknown patient sex under a restriction remain Undetermined.
  Retain both checks. A reliable sex exclusion still excludes when age is unresolved.

Deterministic age cases, distinct from reserved model-quality fixtures:

| Patient AgeYears | Study bounds / relevant bound | Expected age finding |
| --- | --- | --- |
| 18 or 65 | Minimum 18 Years, maximum 65 Years | Satisfied |
| 17 or 66 | Minimum 18 Years, maximum 65 Years | Excluded |
| 1 | Minimum 18 Months; maximum 100 Years | Undetermined |
| 2 | Minimum 18 Months; maximum 100 Years | Satisfied |
| 1 | Minimum 365 Days; maximum 100 Years | Satisfied |
| 1 | Minimum 366 Days; maximum 100 Years | Undetermined |
| 1 | Minimum 0 Days; maximum 730 Days | Satisfied |
| 1 | Minimum 0 Days; maximum 729 Days | Undetermined |
| 0 | Minimum 0 Hours; maximum 8783 Hours | Satisfied |
| 0 | Minimum 8784 Hours; maximum 100 Years | Excluded |
| 12 | Minimum 18 Years; unrecognized maximum | Excluded |
| 40 | Minimum 70 Years; maximum 18 Years | Undetermined: invalid interval |
| Any | Minimum 1 Year; maximum 11 Months | Undetermined: empty interval |
| Unknown | Valid supported bounds | Undetermined |

Additional tests must cover 1900/2000/2100 leap behavior, month ends, all six units,
Int64 parsing boundaries, safe large-span arithmetic, mixed-unit validity,
missing/unsupported inputs, and independent sex exclusions. These cases are
specification expectations for final review; no calculations/tests have been
executed during this design stage.

Calendar references: [.NET AddYears behavior](https://learn.microsoft.com/en-us/dotnet/api/system.datetime.addyears?view=net-10.0),
[DateOnly implementation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/DateOnly.cs),
and [Gregorian leap rules](https://aa.usno.navy.mil/faq/leap_years).
Cookie integration reference: [ASP.NET Core cookie handler](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Security/Authentication/Cookies/src/CookieAuthenticationHandler.cs).

## Testing Decisions

- Test observable behavior, not private method structure or implementation order.
  The primary backend seam is the real API through WebApplicationFactory with
  named fake HTTP handlers and the existing controlled clock. Unit-test the
  calendar/validation/aggregation rules where they need a focused deterministic
  matrix. Prefer existing seams; do not introduce an interface for every class.
- React tests cover role-aware controls, counts, assessment details, notices,
  history, stream parsing/reconciliation, and session warnings/extension.
- Browser workflows with mocked external services cover complete user paths;
  include both route selection and representative start/cancel/recovery/result
  viewing. Existing login/import/navigation behavior must remain intact.

- Extend existing WebApplicationFactory/named HTTP-handler/controlled-clock seams;
  add no live service dependency to automated tests. Test both inference routes
  and the new conditionLabels/conditionLabelId contract independently of the
  experimental runner's fixture/report wrappers.
- Required behavioral coverage includes admission conflicts, role/session control,
  partial success, unavailable inference, timeout/evidence/shape failures, source
  preservation, cancellation before/after commit, lost responses, framing split
  across HTTP chunks, heartbeat delivery through Vite, focus preservation,
  explicit/automatic renewal, actual expiry, multi-tab cookies, and recovery.
- Browser/clock tests must demonstrate initial fresh-30-minute admission, a
  same-session renewal postponing cancellation, expiry stopping unfinished work,
  logout in a shared tab cancelling, and another login remaining independent.
  Do not use repeated model calls to test any of these mechanics.
- These are implementation validation requirements, not evidence already
  obtained. No model-quality acceptance, clinical validity, independent source
  review, or maintainer manual validation is claimed by writing this document.
  All 19 acceptance fixtures remain reserved. Live inference and experiments
  need separate approval even after deterministic application tests pass.

### Acceptance criteria

- [ ] Both configured local Ollama routes work through one application workflow with the approved application JSON names and no implicit fallback.
- [ ] Missing/invalid inference configuration leaves delivered non-Match functionality usable and explains disabled match actions.
- [ ] Each nonempty completed import defines a fixed batch; zero-import batches show No work and cannot run.
- [ ] Both structured checks run, preserve source/uncertainty, and route reliable exclusions/no-text cases without inference.
- [ ] Valid condition-label responses are fully validated, traced to exact snapshots, and aggregated into the agreed pair summaries.
- [ ] Only one operation is active; stale/concurrent/unauthorized requests are rejected without duplicate work.
- [ ] Progress reaches the initiating Dashboard through Vite while work runs, with consistent counts and heartbeat messages that do not renew login.
- [ ] Accepted starts/recovery grant a fresh session, explicit/automatic renewals postpone expiry correctly, and actual expiry/logout/navigation/disconnection cancel unfinished work.
- [ ] Cancellation/publication races preserve committed assessments and never publish interrupted work as completed.
- [ ] Continue and Retry select only their eligible pairs; completed assessments are immutable; repeated failures remain inspectable and manually retryable.
- [ ] Connection loss reconciles against backend state without automatic resubmission or duplicate assessment creation.
- [ ] Both roles can read stored results/history/details in the agreed views; only authorized initiating sessions control active operations.
- [ ] Assessment limits use the existing subdued treatment beside results; Dashboard details remain in place during active work.
- [ ] All agreed deterministic rule, API, React, and browser checks pass; live inference/quality evidence is recorded separately only if explicitly authorized.
- [ ] Setup documentation covers both routes/preset requirements, restart/data loss, configuration, failure/recovery, and known assessment limits without private machine specifications.

## Out of Scope

Retain the baseline's exclusions, including background execution, persistence,
rematching completed snapshots, and full clinical eligibility assessment.
No new model optimization, cloud inference, acceptance run, download, or live
comparison is authorized. Licensing remains a release-preparation decision.
Publishing this specification does not authorize an implementation slice.

## Further Notes

The source assessment design remains authoritative for interpretation meanings;
this specification explicitly changes application JSON identifiers to
conditionLabels/conditionLabelId and extends session control for active work.
Historical evaluation assets and evidence are not relabelled as application
validation.

### Approved implementation slices — tickets not yet published

Each slice delivers observable end-to-end behavior with its tests, rather
than only a data layer or a set of helper classes. The approved breakdown is:

| Slice | Depends on | End-to-end result |
| --- | --- | --- |
| Extend and observe demonstration sessions | None | Users can see the confirmed expiry, receive the warning, explicitly extend, and have shared-tab renewal/logout handled correctly; existing browsing/import behavior remains covered. |
| Run batch assessments and inspect Dashboard results | Session slice | Operators run fixed batches through structured checks and either inference route, see streamed progress and stored details, and stop safely on cancellation/expiry/disconnection. Viewers can inspect shared state with Refresh. Recovery actions arrive in the next slice; interrupted/failed work remains accurately represented. |
| Continue unfinished work and retry failed evaluations | Batch-run slice | Operators recover eligible pairs, inspect attempt history, and retain immutable completed results, including repeated-failure and lost-response cases. |
| Browse assessments from Patient and Study details | Batch-run slice | Both perspectives expose the same stored summaries/details and assessment limits without starting match operations. This slice does not depend on the recovery UI. |

Use one feature branch and PR per approved slice, preferably one fresh session
per slice. The agreed default is one functional Conventional Commit containing
the slice's implementation, tests, and relevant documentation after development,
validation, and review. A distinct later UI refinement or other justified change
may have its own commit; do not create extra commits merely to satisfy a process.
If independent functional changes make a slice too large, revisit the slice
boundary rather than force one oversized commit. Agree the commit plan before
committing. Review the actual commits before choosing a merge method. Do not
publish implementation tickets, begin a slice, commit, or merge without the
corresponding human approval. The next skill for a reviewed breakdown is
to-tickets; this approved plan is not implementation-ticket publication or
authorization to begin implementation.
