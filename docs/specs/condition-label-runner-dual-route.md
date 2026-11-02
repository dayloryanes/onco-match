# Support both Ollama inference routes in the evaluation runner

Status: specification and final design published; the human explicitly approved
the bounded offline implementation and agreed deterministic tests on 2026-11-02.
Implementation is complete locally on `feat/runner-compatible-route`, with
96 original offline checks passing, native request parity, all 21 baseline asset
hashes, and a Release build passing with zero warnings/errors. The human then
separately approved the named preset creation and a bounded two-request live smoke
test, including its command and offline guard tests; both live requests completed
successfully with explicit low reasoning. The smoke scope is consumed and disabled;
126 offline checks pass after the reviewed operation-deadline coverage fix.
Separate automated Standards and Spec reviews are complete with no outstanding
findings. The human explicitly approved the local implementation commit with
subject `feat(evaluation): support both Ollama inference routes`. Code publication,
PRs and merges remain unapproved. See the overview for the resulting commit
checkpoint; this approval does not authorize further live execution.

Canonical specification: [Support both Ollama inference routes in the evaluation runner](https://github.com/dayloryanes/onco-match/issues/18).

Context: [development overview](https://github.com/dayloryanes/onco-match/issues/1),
[Wayfinder map](https://github.com/dayloryanes/onco-match/issues/2),
[open evaluation gate](https://github.com/dayloryanes/onco-match/issues/6),
[assessment contract](../planning/condition-label-assessment-design.md),
[evaluation design](../planning/condition-label-evaluation-design.md), and
[selected native baseline](../evaluation/condition-label-baseline.md).

## Problem Statement

The maintainer wants to evaluate condition-label assessment through either
Ollama's native API or its OpenAI-compatible API. The current isolated runner
prepares and executes only the selected native baseline. Creating a second
runner or copying its fixtures, validation, and recording would make evaluation
harder to understand and maintain.

The native baseline's recorded results do not establish compatible-route
behavior. Preparation must expose settings that cannot transfer directly rather
than silently accepting different defaults or presenting an unverified candidate
as equivalent to the historical baseline.

## Solution

Extend the existing runner with an explicitly selected inference route, shared
evaluation behavior, and route-specific request/response handling. Initially
support only local Ollama: native non-streaming chat and OpenAI-compatible
non-streaming Chat Completions. Retain Ollama-specific metadata verification.

Deliver offline preparation, fake-transport execution checks, and documentation
for both routes. Preserve the native baseline and leave all real-server
execution disabled. This work precedes a separately specified application
matching/results slice; the application remains independent of the runner.

## User Stories

1. As the maintainer, I want one runner for both routes, so that I maintain one evaluation workflow.
2. As the maintainer, I want existing native preparation commands to retain their behavior, so that adding a route does not invalidate the baseline workflow.
3. As the maintainer, I want each configuration to identify its route explicitly, so that the intended request format is unambiguous.
4. As the maintainer, I want unknown routes rejected, so that a typo cannot silently select another API.
5. As the maintainer, I want compatible requests prepared offline, so that I can inspect them without contacting Ollama.
6. As the maintainer, I want both routes to use the same reviewed development inputs, so that transport differences do not silently change the assessment task.
7. As the maintainer, I want the original prompt and schema guidance preserved, so that this task does not become prompt optimization.
8. As the maintainer, I want every baseline control accounted for, so that a missing compatible field cannot silently change a candidate.
9. As the maintainer, I want unresolved required settings to block preparation, so that incomplete configurations do not produce apparently ready requests.
10. As the maintainer, I want any necessary model definition reviewed before creation, so that runner work cannot silently modify my local models.
11. As the maintainer, I want Ollama version and model metadata checked through native metadata endpoints, so that compatible requests still have a concrete server/model identity.
12. As the maintainer, I want route and configuration identity retained in generated artifacts, so that I can trace every request and result.
13. As the maintainer, I want native and compatible outputs kept distinguishable within the existing output organization, so that one cannot overwrite or masquerade as the other.
14. As the maintainer, I want the same assessment validator and pair aggregation on both routes, so that changing transport does not change assessment semantics.
15. As the maintainer, I want malformed or incomplete compatible responses rejected, so that transport completion is not mistaken for a completed assessment.
16. As the maintainer, I want failures reported without retries or route fallback, so that the configured route's behavior remains visible.
17. As the maintainer, I want cancellation and deadlines checked with fake transport, so that offline checks exercise lifecycle behavior.
18. As the maintainer, I want raw reasoning excluded from artifacts on both routes, so that compatible handling preserves existing recording protections.
19. As the maintainer, I want the native assets and frozen requests unchanged, so that historical evidence remains reproducible.
20. As the maintainer, I want offline checks that cannot reach a real server, so that tests need neither Ollama nor model execution approval.
21. As the maintainer, I want old and newly constructed execution approvals rejected by the production guard, so that adding a route cannot enable live execution.
22. As the maintainer, I want instructions for selecting and preparing either route in the existing runner documentation, so that setup is understandable without another documentation tree.
23. As the maintainer, I want documentation to distinguish implementation tests from live quality evidence, so that compatible support is not described as accepted model behavior.
24. As the maintainer, I want the application unchanged, so that runner work stays separate from the later matching slice.

## Implementation Decisions

### Organization and existing behavior

- Keep one console project, one entry point, and the existing offline-check
  mechanism. Extend the existing HTTP-handler seam rather than creating a second
  test project or an external-server test harness.
- Share fixture loading, source catalog construction, assessment schema,
  original guidance, input-size checks, assessment validation, pair aggregation,
  recording, and execution guards. Separate only the concrete route differences.
- The existing response utility already contains compatible-envelope parsing;
  reuse and complete it instead of introducing a competing parser. Request
  preparation, the execution path, and fake transport currently assume native
  chat and must become explicitly route-aware.
- Keep the existing command family: `prepare`, `self-check`, and guarded `run`.
  Existing native invocations remain valid. Additional profile selection is a
  small extension of this interface, not a separate route-specific executable.
- Do not introduce a provider framework, plugin registry, dependency package,
  parallel source tree, or broad cleanup. Ordinary internal file organization
  remains an implementation detail within this bounded scope.

### Route and candidate contracts

- Preserve the existing `route: native` value. The compatible serialized value
  is `route: openai-compatible`.
- Validate an explicit route on every candidate configuration. Reject missing
  or unknown routes rather than inferring a route from the model or endpoint.
  Existing frozen native configurations already identify their route.
- Native inference uses `POST /api/chat`; compatible inference uses
  `POST /v1/chat/completions`. Both request a single, complete, non-streaming
  response from local Ollama. Neither uses a cloud endpoint.
- Use the existing local Ollama connection boundary. Native metadata requests
  may verify version, model digest, template, supported thinking values, relevant
  model parameters, and available residency information for either route.
- A compatible candidate has its own profile identity and configuration. Do not
  mutate the frozen native configuration to represent it, copy baseline assets
  into a new baseline tree, or claim the historical native digest applies to a
  separately defined model without verification.
- Bind preparation and recording identity to route, exact configuration,
  messages, schema, and relevant source/model information. Changed route or
  settings invalidate stale preparation. Retain the existing output hierarchy;
  distinct profiles prevent collisions without creating a second results system.

### Requests and setting accountability

- Use the same original v5 instructions, original two result-field descriptions,
  decoded patient facts, condition-label catalog, and development-case order.
  Preserve message text and the schema embedded in the system message.
- Native requests retain their frozen shape. Compatible requests place the same
  assessment schema in the compatible `response_format` JSON-schema envelope.
  The transport wrapper must not rewrite assessment fields or their meaning.
- The initial candidate remains GPT-OSS 20B with low reasoning. This task does
  not select a new model, tune settings, or relax the assessment contract.
- Account for temperature, top-p, top-k, min-p, seed, repeat/presence penalties,
  context size, output allowance, reasoning level, and keep-alive behavior.
  Keep framing reserves, request/operation deadlines, sequential case order,
  repetition count, and zero retries explicit as runner controls.
- For each control, document its intended value, whether it belongs in the
  request, model configuration, or runner, how it is checked, and any unresolved
  difference. Do not equate different penalty controls merely because their
  names are similar. An undocumented ignored field is not a mapping.
- Verify mapping against the pinned Ollama 0.40.13 behavior before treating a
  compatible configuration as preparable. Current online documentation alone
  does not prove the pinned implementation's behavior. Unsupported required
  controls or unverified mappings fail preparation with a specific explanation.
- Offline preparation validates the declared configuration and documented
  mapping; it cannot attest to the actual state of a running server. Reports
  explicitly distinguish offline validation from server verification. Fake
  metadata exercises future verification behavior without contacting a server.
- Define a separately named local model configuration derived from the existing
  GPT-OSS 20B weights, explicitly setting context 24,576, top-k 20, min-p 0,
  and repeat penalty 1. Retain the native baseline model/configuration unchanged.
  Preparing the declaration and documenting setup do not authorize model
  creation, replacement, weight downloads, or a call to the local service.
- Send `keep_alive: "5m"` on compatible requests. Document this as an Ollama
  extension to the compatible API, not a portable OpenAI field.
- The offline compatible declaration identifies the intended model name,
  pinned source-model identity, explicit model parameters, route, prompt/schema,
  request controls, and their fingerprints. It may represent an uncreated model;
  mark actual derived-model identity and server verification as not yet verified.
  Do not invent a derived-model digest or reuse the source digest as that digest.
  Known mappings and a valid declaration permit offline request generation;
  unresolved required settings still block it.
- Before any future separately approved live evaluation, establish and freeze
  the actual derived-model identity and verify model parameters, version,
  template, and weights provenance against the declaration. A changed identity
  invalidates stale preparation/approval. This task implements that boundary
  with fake metadata while leaving real execution disabled. Exact model-creation
  commands and declaration contents must be reviewed before their execution.
- Keep full-input sizing and explicit rejection of oversized inputs. Do not
  truncate prompts, labels, or schema to fit a compatible request.

### Responses, failures, and recording

- Parse the selected route's envelope explicitly. Compatible handling requires
  exactly one usable assistant completion with string content and a successful
  stop reason. Reject missing/ambiguous choices, tool-only/refused answers,
  output-limited responses, malformed envelopes, and invalid JSON.
- Validate returned model identity against the candidate. Only the extracted
  final content enters the existing assessment validator and pair aggregation.
  Do not salvage partial labels, repair responses, or turn invalid output into
  a completed Undetermined assessment.
- Preserve existing failure/lifecycle policies: transport/service or identity
  failures stop the run; per-case invalid output/evidence and request timeouts
  follow existing continuation behavior; cancellation stops further work and
  records interruption. No automatic retry, answer repair, or alternate route.
- Retain per-request and aggregate elapsed measurements, failures, and completion
  counts. Record available route-specific usage without inventing native timing
  fields for compatible responses; missing metrics remain explicitly unavailable.
- Identify route/profile in preparation manifests, attempt records, and summary
  artifacts so individual records remain understandable outside their directory.
- Never persist raw provider envelopes or separated reasoning text. Preserve
  detection/rejection of inline reasoning markers and ensure diagnostic messages
  do not leak reasoning or malformed raw response bodies.
- Retain production guards for both routes. Ordinary CLI execution cannot reach
  metadata or inference HTTP even when supplied a syntactically valid approval.
  Fake-transport injection remains internal to offline tests, not a public bypass.

### Documentation

- Extend the existing runner README with route selection, configuration examples,
  offline commands, setting limitations, metadata dependencies, and testing status.
- Review affected references in the documentation index, POC baseline, development
  baseline report, and main README. Update current capability descriptions where
  needed; preserve historical native results and accurate historical statements.
- Keep runner instructions together. Application inference setup remains for the
  matching slice; the main README must continue to state its delivered behavior.
- Clearly separate offline support, actual server compatibility, live quality
  results, and acceptance. Neither route support nor fake-transport checks closes
  the evaluation gate or establishes clinical validity.

## Testing Decisions

The human agreed to extend the existing offline checks using fake HTTP and the
existing development fixtures. Prefer checks through preparation and the shared
runner/recorder with an injected HTTP handler: assert requests, persisted outputs,
failure states, and absence of unintended calls. Avoid tests of internal class
layout or duplicate tests that merely mirror field assignments.

Existing prior art is the runner's preparation parity checks, baseline hash
audit, and fake-handler execution checks. Extend those mechanisms rather than
duplicating evaluation fixtures or introducing live services.

| Boundary | Required evidence |
| --- | --- |
| Native preservation | All baseline asset hashes unchanged; all 16 native generated requests retain frozen content and guidance; existing offline behavior remains covered. |
| Route selection | Explicit known routes accepted; missing/unknown routes rejected; configuration and artifacts identify the chosen route; no alternate inference endpoint is contacted. |
| Compatible preparation | Correct Chat Completions endpoint/body, non-streaming mode, one completion, original messages and schema; assessment input excludes narrative fields and expected answers. |
| Setting checks | Required controls have explicit mappings; missing/unresolved mappings and mismatched declared model parameters block preparation; oversized inputs fail without truncation; compatible requests retain the approved five-minute keep-alive extension. |
| Offline model declaration | A valid declared model permits request generation without model creation or HTTP; artifacts explicitly mark actual model/server verification as outstanding, never copy the source digest into the derived identity, and reject unresolved settings. |
| Identity and freshness | Fake metadata mismatches are rejected; changed route/configuration/request invalidates preparation; distinct candidate outputs cannot overwrite or be confused with native outputs. |
| Successful response | Fake compatible and native envelopes carrying the same assessment content yield the same assessment validation and pair summary; missing provider metrics are not fabricated. |
| Invalid response/evidence | Malformed/duplicate-key envelopes, missing or multiple choices, null/non-text content, refusal/tool-only/length completion, wrong model, invalid assessment JSON and evidence follow the stated failure policy. |
| Lifecycle | Fake HTTP errors, cancellation, request timeout and operation deadline preserve completed records and stop/continue as specified, with no retry or fallback. |
| Recording privacy | Separated fake reasoning and inline reasoning markers cannot leak through persisted attempts, errors, or reports on either route. |
| Execution guards | Native and compatible production attempts reject before any real HTTP, including metadata; consumed/replayed approvals and stale preparation remain rejected. |
| Test isolation | Prepare and self-check require no running Ollama, registry, frontend, private evidence directory, downloaded model, or reserved acceptance fixture. |

Run the extended offline suite, preparation checks, and portable baseline audit
when implementation is separately approved. No new tests or model calls were
executed while drafting this specification. Offline reports must state their
scope and must not describe synthetic answers as model-quality results.

## Out of Scope

- Application/backend/frontend matching, inference integration, or setup changes.
- A second runner, provider framework, generalized server compatibility, broad
  runner rewrite, or new test project/dependency.
- Servers other than local Ollama; OpenAI cloud access; Responses, Completions,
  streaming, tools, multimodal inputs, or automatic fallback.
- Prompt/model optimization, new clinical attributes, changed assessment
  semantics, stricter semantic scoring, or altered fixture expectations.
- Live evaluation beyond the separately approved smoke exception below, timing
  comparisons, acceptance runs, numerical latency decisions, model downloads,
  or enabling ordinary production execution.
- Changing frozen baseline assets, rewriting historical evidence, or retiring
  any of the 19 reserved acceptance fixtures.
- Code publication, PRs, merges, or closing the open model-quality gate.

## Local implementation validation

- Original offline implementation checkpoint: native and compatible preparation,
  96 self-check assertions, all 21 baseline-asset hashes, and Release build
  passed. Native preparation still compares all 16 requests against the frozen
  assets. The suite exercises both routes through fake HTTP, including declared
  metadata checks, malformed response/evidence handling, cancellation, simulated
  timeouts, privacy, preparation tampering, and production guards.
- Initial source inspection was performed separately against this specification and the
  repository workflow. At that checkpoint no independent source review, maintainer manual validation,
  application test run, actual server compatibility, or model-quality result is
  claimed. Existing request/operation deadline logic is retained; timeout policy
  checks inject failures rather than waiting for the production deadlines.
- At that pre-smoke checkpoint, application source and baseline assets were unchanged. No new runner, test
  project, dependency, or parallel fixture tree was introduced. No local Ollama,
  registry, inference, acceptance, model creation, or download call was made.
- The human approved a local commit of the reviewed working changes and
  specification. Code publication requires separate approval. Live execution
  beyond the consumed smoke scope and the application matching slice remain
  separate stages requiring approval.

## Automated source review

The human approved separate Standards and Spec reviews of the uncommitted changes
against `d2346665b1ba6efb8753d0e1f118effe1938e850`, including this initially
untracked specification. The reviews ran in independent agent contexts, read-only,
without service calls, model requests, or test execution by the reviewers.

- Standards: no actionable documented-standard violations or material smells.
- Spec: one P2 coverage finding. The lifecycle requirement called for operation-
  deadline expiry coverage; injected request-timeout failures and external
  cancellation did not exercise it. This finding is resolved with controlled-clock
  tests through the existing runner/fake-HTTP boundary on both routes.
- The runner uses the built-in `TimeProvider`, defaulting to system time. Tests
  expire the actual operation deadline during the third request and verify two
  retained validated results, one interruption without a completed assessment,
  zero failures, the deadline flag and no fourth request. The flag also observes
  the deadline cancellation source so it does not depend solely on UTC timing.
- Both reviewers followed up on the fix and found no new actionable issue.
  Final verification separately passed 126 offline checks, all 21 baseline hashes,
  native frozen-request comparisons and Release build with zero warnings/errors.
  No production-duration wait, live request, new dependency or new project was
  introduced. No maintainer manual validation or clinical review is claimed.

There are no outstanding review findings. The local commit is approved;
code-publication approval remains the next boundary. These review notes
were added after source/test verification and do not change runner behavior.

## Further Notes

### Separately approved live smoke test

The human explicitly approved creation of the named compatible model from the
existing pinned local source weights, plus a bounded smoke command and offline
guard tests. Execute CL-D01 once through native and once through compatible chat,
sequentially, using the original prompt/schema/settings and explicit low reasoning
(`think: low` / `reasoning_effort: low`). The budget is two inference requests
total, at most 600 seconds per request and 8,192 output tokens per request, no
retry/fallback, no downloads, tuning or acceptance fixtures. Stop on failure.

The reviewed model definition derives from `gpt-oss:20b` with `num_ctx 24576`,
`top_k 20`, `min_p 0` and `repeat_penalty 1`. Verify source digest before creation;
freeze actual derived digest, parameters, template and source-weight provenance
before inference. Native model and frozen assets remain unchanged.

The existing runner gains `smoke <profile> <approval>`, sharing the actual
preflight/request/recording path. Exact approvals bind request/configuration/source
hashes, actual identity and one CL-D01 request to the two fixed one-use identities
`smoke-20261102-native-1` and `smoke-20261102-openai-compatible-1`. Claim each identity
before metadata HTTP and reject replay even after failure. Ordinary `run` remains
disabled. These bounded results establish only the tested path, not route
equivalence, acceptance, or clinical validity. Additional live requests require
fresh approval and a new reviewed execution scope.

Execution completed once per route: native 97.836 seconds and compatible 52.541
seconds, both with mechanically valid evidence and no expected finding differences.
AI-assisted final-answer review found no material issue for CL-D01; no independent
live-output review or maintainer manual validation is claimed. The preset shares the original
weight layer/template and is retained locally, with actual digest
`d1885bff47d2bfe492a97fb9a28968e8e29cb4fe4c0f81f902ab6a8ebd9e8709`.
The source native digest and all 21 baseline assets remain unchanged.
The human subsequently approved renaming the preset to
`oncomatch-gpt-oss-20b-openai-compatible:latest` across current configuration,
documentation and local Ollama. Keep its digest/parameters unchanged and preserve
completed request evidence under the historical alias. The rename authorizes no
new inference. Instructions for creating/selecting any application preset belong
to the later matching slice; the current application has no inference dependency.

Both run identities are consumed and production smoke has been disabled in
source. 124 offline checks and Release build pass afterward. No retries,
downloads, acceptance requests, commits or code publication occurred. The live
source fingerprint is `1f6a7214e927c1b26272261b7198a55b3af36b0b1fab5848a18d5d27728d40b3`;
current source has since changed to disable smoke and record the results.
Local evidence remains in the existing ignored runner results area.

### Pinned-source check and approved model contract

A read-only check of Ollama v0.40.13 confirmed these transport mappings:
`temperature`, `top_p`, `seed`, and `presence_penalty` map directly;
`max_tokens` becomes `num_predict`; `reasoning_effort` selects thinking;
`response_format.json_schema.schema` supplies the assessment schema. Its request
type has no context-size, top-k, min-p, or repeat-penalty fields. It accepts
`keep_alive` as an Ollama extension, not a portable OpenAI field.
[Pinned request type and conversion](https://github.com/ollama/ollama/blob/v0.40.13/openai/openai.go#L103).

Model options are applied before request overrides. The approved model-side
requirements are context 24,576, top-k 20, min-p 0, and repeat penalty 1;
these must be explicit and verifiable rather than assumed defaults.
[Pinned option precedence](https://github.com/ollama/ollama/blob/v0.40.13/server/routes.go#L126).
The retained model metadata records only temperature as an explicit parameter,
so it does not establish those requirements.

The human approved a separately named local model declaration with these four
explicit controls, the five-minute keep-alive extension, offline declaration
validation/request generation, and actual identity/parameter verification before
future live evaluation. The declaration can be prepared without creating the
model. Creation and live execution each remain separate approval boundaries.
No defaults may be silently substituted. Source inspection establishes conversion
behavior, not actual installed-server state or model-quality equivalence.

The design checkpoint is resolved. Replace `needs-info` with `ready-for-agent`:
the bounded offline implementation is specified, but the label does not grant
implementation approval or imply the declared model already exists.

The human chose this runner work as a separate predecessor to the application
matching/results discussion. Completing it does not automatically authorize a
live comparison or the matching specification stage.

The route value, bounded command extension, testing boundary, and rejection of
unresolved required settings are included in the approved design. An exact
model-creation declaration must be presented before creation is authorized;
it is not a prerequisite for implementing and testing offline declarations.
Behavior or scope changes beyond this contract require human review.

The proposed scope extends the baseline's single-integration direction for the
isolated runner only. It does not promise two application integrations or
universal compatible-server support. Specification publication and tracker
updates are approved; reference this issue rather than creating another handoff
document. Source/document publication remains separately controlled.

Primary references for implementation verification:
[Ollama OpenAI compatibility](https://docs.ollama.com/api/openai-compatibility)
and [Ollama structured outputs](https://docs.ollama.com/capabilities/structured-outputs).
These describe current documentation, not verified behavior of the pinned
server or evidence of a completed live run.
