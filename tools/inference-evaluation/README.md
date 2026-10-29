# OncoMatch inference evaluation

A separate C# console tool for evaluating OncoMatch's focused assessment task
through local Ollama. No application project references, added packages, model
downloads, provider framework, or automatic model search. It is not part of API
startup or a product feature. .NET 10 SDK is required.

## Current authorization and scope

The human approved local preparation on `chore/inference-evaluation`: runner,
six serialized development fixtures, prompt/schema/configuration, documentation,
offline checks, and dry-run previews. The human then reviewed the prepared
prompt/schema and six-case walkthrough, selected the 12,288-token context and
2,048-token output baseline, and approved the local baseline commit. Push,
PR/tracker publication, and inference still require separate approval.
The separately approved first native trial then ran all six cases and stopped:
all responses failed strict duplicate-field validation. Its local approval record
is consumed, and its original outputs/input snapshots are retained. The human
approved two subsequent local fixes and offline checks: flatten the cancer-type
schema definition and exclude results source snapshots from compilation, then
approved their local commit after offline verification. That approval does not
authorize another run or publication.
The human subsequently approved local preparation of a D1-only mode and its
offline checks, then approved its local commit after all 48 checks passed.
The separately approved D1-only diagnostic then ran once and stopped. Mechanical
checks passed, but stage was wrongly excluded despite the satisfied OR alternative.
The human approved local preparation of a general prompt revision for compound
requirements and supplied staging labels, then approved its local commit and one
native six-case development batch. Freeze this revised prompt across D1, D3,
D5, D6, D9, D13, once each, with unchanged settings and no retries; stop for
review after the batch or earlier on timeout, cancellation, or internal fault.
That batch completed and stopped: five responses passed mechanical checks, while
D5 failed evidence validation; D3 and D9 matched all expected labels, but the
candidate still had substantive development-case failures. Review was presented
in chat; the human does not need to inspect the saved files again. Detailed raw
outputs and AI review remain local, and no independent human file review is claimed.

The human then chose the shortlisted Phi-4 Mini alternative, downloaded it, and
approved local configuration preparation and offline checks. After reviewing the
comparison scope and clarifying the prior results, the human approved proceeding
with the local Phi commit and one native six-case batch. Use the frozen revised
prompt and unchanged explicit settings, no retries, then stop for review. Further
changes, reruns, and publication remain unauthorized. At this commit checkpoint
the Phi evaluation batch has not started. Previous run approvals are consumed
records; this batch requires its own exact fingerprint/model/scope record.

The current proposed comparison candidate is `phi4-mini:3.8b-q4_K_M`.
The earlier candidate was `qwen3:4b-instruct-2507-q4_K_M`; its exact inputs and
answers remain in Git history and local result snapshots. Partial CPU offload
is allowed: measure it rather than treating it as an automatic rejection.

Initial trial scope: D1, D3, D5, D6, D9, D13, in that order, once each through
native `/api/chat`. Fresh two-message conversation per case, sequential requests,
120-second deadline including loading, no warm-up inference, automatic retry,
prompt revision, alternate model, or next batch. At most six inference requests
and 720 seconds of inference-request time; metadata observations and file I/O
are additional. Stop for review after the batch, interruption, timeout, or an
unexpected internal fault. A timeout stops scheduling so server activity can
be checked before any later request. Other pair-level HTTP/validation failures
are retained and the remaining agreed cases continue.

## Authoritative design

- [Project workflow](../../docs/agents/development-workflow.md)
- [Overview and current published checkpoint](https://github.com/dayloryanes/onco-match/issues/1)
- [Model/API decision](https://github.com/dayloryanes/onco-match/issues/6)
- [Assessment contract](../../docs/planning/assessment-contract-design.md)
- [Approved fixtures and scoring](../../docs/planning/inference-evaluation-design.md)
- [Glossary](../../GLOSSARY.md)

The six fixtures are direct serializations of selected approved development
cases. Study nulls in this fixture format represent absent fields; explicit
patient null values remain present in the source catalog. No biomarker or
treatment fields are added. The model sees the catalog and general instructions,
not expected answers, review guidance, case labels, or acceptance examples.
Existing frozen real-study snapshots remain in the planning documents; they
are neither copied nor submitted by this tool. No acceptance cases run here.

## Offline commands

From the repository root:

```powershell
dotnet run --project tools/inference-evaluation -- self-check
dotnet run --project tools/inference-evaluation -- dry-run native
dotnet run --project tools/inference-evaluation -- dry-run compatible
dotnet run --project tools/inference-evaluation -- dry-run-d1
```

The tool's offline commands make no HTTP requests. Self-check uses in-memory
fake HTTP handlers, not an Ollama or fake-network server. The ordinary .NET
build may perform SDK restore work; the project has no package references.
Dry-run writes six complete request previews and `manifest.json` under
`results/preparation/<fingerprint>/<route>/`, which Git ignores. Omitting a
command defaults to native dry-run. There is no background process.
`dry-run-d1` accepts no route or other arguments: it previews exactly D1 through
the native API, under `results/preparation/<fingerprint>/native-d1/`. Its manifest
records one request and at most 120 seconds of inference-request time. The usual
`dry-run` continues to preview the six-case batch.

## Files to review

| File | Purpose |
| --- | --- |
| `Program.cs` | CLI, dry-run, bounded execution, saved records |
| `Evaluation.cs` | Request/response adapters, source catalog, validation, fingerprints |
| `OfflineChecks.cs` | Deterministic checks with saved answers and fake transport |
| `fixtures.json` | Six approved fictional inputs, expected labels, review guidance |
| `prompt.txt` | Exact general screening instructions |
| `response-schema.json` | Required narrative response shape |
| `candidate.json` | Reviewed baseline model, settings, order, and limits |
| `execution-approval.example.json` | Non-executable example; all approvals remain false |
| `execution-approval-d1.example.json` | Non-executable D1-only approval example |

The deterministic age/sex handling is deliberately limited to these six fixtures:
all use age 52, minimum 21 Years, maximum 75 Years, and sex ALL. Those inputs are
checked before requests; age is Satisfied and sex No restriction identified.
This is not the production six-unit age parser or application failure/recovery
test harness. More fixtures and the full acceptance scheduler need separately
reviewed extensions. No new application implementation is included.

The cancer criterion is intentionally a standalone object rather than an allOf
refinement of the shared criterion. The installed Ollama 0.40.13 / llama.cpp
b11963 converter concatenates overlapping allOf property lists, which forced a
duplicate result key in the first trial. An offline probe against the installed
converter reproduces that failure with the saved schema and passes with the
revised schema. The response contract and strict duplicate/evidence validation
are unchanged. The self-check guards the standalone definition, its three-value
result enum, and agreement with all other shared criterion fields.

The project excludes `results/**/*.cs` from compilation. Saved source snapshots
remain evidence, not source inputs for the next build. The self-check creates a
temporary ignored source sentinel and inspects actual MSBuild Compile inputs to
verify the exclusion, then removes only that sentinel.

## Candidate settings and input sizing

The prompt used for the Qwen revised batch spells out how a satisfied OR alternative
governs every affected criterion summary while retaining independent mandatory
requirements. It also clarifies unresolved alternatives/cohort applicability,
separate assessment of unrelated criteria, and the prohibition on invented
staging-system equivalence comparisons. It introduces no fixture-specific facts,
answer examples, new assessed criteria, changed expected results, or acceptance
material. For the proposed Phi comparison, only candidate identity/model change:
the prompt, sampling, context/output settings, fixtures, schema, and validator
remain as in the revised Qwen batch. This compares recorded configurations, not
each model's best possible settings. The models have different native templates
and tokenizers; those necessary differences must be recorded. No alias or runner
code change is needed. The approved trial uses all six native development cases,
once each, with one frozen prompt; only that single batch is authorized.
D1-only mode remains available for targeted structural diagnostics.

Read-only metadata verification found Phi installed as Q4_K_M under Ollama
0.40.13, with byte-level gpt2 tokenizer metadata, gpt-4o pretokenization, and the
Phi chat template. The verified two-message/no-tools framing fits the existing
reserve. The Phi model declares a 131,072-token context capability; the experiment
still requests only 12,288. Its metadata has no model-specific sampling overrides.
Ollama 0.40.13's repetition penalty default is 1.0, matching Qwen's recorded
model override; explicit request sampling remains unchanged. GPU fit and speed
at the proposed context have not been measured for Phi. A manually observed
4,096-context residency sample is not a controlled comparison with the prior
12,288-context Qwen run.

Agreed generation settings: temperature 0.7, top-p 0.8, top-k 20, min-p 0;
recorded seed 42. No thinking controls are sent for these completion candidates.
A seed is not a guarantee of identical answers or repeatability evidence.
Native `keep_alive` is 5 minutes to avoid deliberately reloading between cases;
the runner does not change global Ollama settings or unload another model.

**Reviewed baseline limits:** 12,288 context tokens, 2,048 maximum output tokens,
and a 1,024-token chat-framing reserve. An initial
8,192-token proposal did not satisfy the deliberately conservative byte bound;
12,288 was selected to clear that bound, not through inference. Larger
context can increase memory use/offloading. A tokenizer-based count could justify
a smaller context later, but no tokenizer dependency/download is added here.

Sizing counts UTF-8 bytes in the entire serialized request, including schema
and API-only fields, then adds framing reserve and maximum output allowance.
This overcounts content tokens for the candidate's byte-level tokenizer; it is
not an exact token count, a memory estimate, or a universal tokenization method.
Before execution, inspect the installed tokenizer/chat template and approve its
fit within the framing reserve. The execution gate also verifies tokenizer type
and template hash. Input above the bound stops the run without truncation.
Output-limited answers fail; they are never completed assessments.

## API comparison limits

Both adapters are implemented and checked offline:

- Native `/api/chat`: JSON-schema `format`, sampling/context/output options.
- Compatible `/v1/chat/completions`: JSON-schema `response_format`, shared
  temperature/top-p/seed/output limit, compatible response-envelope handling.

**Compatible live execution is not enabled in this checkpoint.** Ollama's
compatible API does not directly set `num_ctx`, top-k, or min-p. Before a later
comparison, agree and verify equivalent server-side settings (for example a
reviewed Ollama model configuration), version support, and remaining differences.
No alias is created now. A compatible preview is not a claim of equivalence or
an execution authorization. Both routes are local Ollama; no cloud service or
OpenAI SDK/account is involved.

## Later execution, only after separate approval

First review the exact native previews, candidate limits, source catalog,
prompt/schema, and offline results. The intended history is an approved baseline
commit before inference, then separately approved execution, review, and later
commits of findings or deliberate revisions. The human approved this local
baseline commit; that approval does not authorize inference or publication.
The human also approved the local schema/build-fix commit after offline review;
execution and publication approvals remain separate.

Only after explicit execution approval, prepare a fresh ignored
`execution-approval.json` from the example. The retained first-run record is not
permission to rerun; changed inputs also invalidate its fingerprint. Record the
actual new approval, final fingerprint, unique run ID,
installed model's full digest, Ollama version, reviewed template hash, and both
review flags. That file is a record of permission, not a way to grant permission.

```powershell
# DO NOT RUN during preparation. Requires a separately approved local record.
dotnet run --project tools/inference-evaluation -- run tools/inference-evaluation/execution-approval.json
```

For a separately approved D1-only diagnostic, use the D1 approval example and
record exactly `caseIds: ["D1"]` and `maxRequests: 1` in a fresh local approval
file under ignored `results/`. The command is deliberately distinct:

```powershell
# DO NOT RUN without separate approval of this exact one-request diagnostic.
dotnet run --project tools/inference-evaluation -- run-d1 tools/inference-evaluation/results/approvals/d1.json
```

`run-d1` selects only D1 from the same reviewed fixtures, sends one native request,
and stops regardless of its outcome. It does not alter the model, prompt, schema,
sampling, context/output limits, or D1 payload. It applies the same 120-second
deadline, cancellation, evidence validation, snapshot/record retention, and
human semantic review. The original `run` selects all six cases. Both commands
require an exact matching approval list/count; neither approval can authorize
the other mode. No arbitrary case selection, automatic follow-on batch, or retry
is introduced. Both modes share the same candidate fingerprint but their execution
approvals additionally bind the exact scope through case IDs and request count.

The run validates permission and conservative fit before HTTP. It then claims
the run ID exactly once, snapshots the hashed inputs, and checks local metadata
without inference. It never downloads a model. Missing service, mismatched model
or version, or unverified template/tokenizer stops before inference and consumes
the run ID; it requires review rather than a silent restart. Loopback numeric
HTTP addresses only; redirects/proxies are disabled. Live execution currently
accepts only a separately approved candidate fingerprint and the exact native
scope (six-case `run` or one-case `run-d1`). Another local model can be proposed by
changing configuration; it still
needs fresh approval and verified tokenizer/template prerequisites. Other
tokenizers need a reviewed sizing implementation, not an assumed fit.

Ctrl+C requests cancellation and prevents further scheduling. Closing a client
request does not establish instantaneous termination of server/GPU work. After
timeout or interruption, check activity before authorizing any further inference.
The process lock prevents overlapping runner invocations; it cannot control
requests from another application. Avoid concurrent manual generation for useful
measurements. Presence of `execution.lock` alone does not mean a process is active;
the lock is an open file handle, and the empty file may remain after exit.

## Results, review, and retention

Everything under `results/` stays local and ignored:

- Preparation request previews/manifests.
- Per-run claimed approval, exact hashed input snapshots, and sanitized
  model/version/quantization metadata (no identifying host specifications).
- Append-only started/finished attempt records, structured answer content,
  mechanical validation, expected-label differences, and failure classification.
- Wall-clock inference-request duration; native load/prompt/generation metrics
  (Ollama durations are nanoseconds); separate residency observations when available.
- A final review checkpoint. No raw model reasoning is retained.

Interrupted/timeout attempts remain unfinished/failed; no partial content is
treated as a completed assessment. Failed and output-limited answers are retained
when content is available. The first request is not assumed cold: report actual
load duration. Residency observations are taken after each attempt and may be
unavailable; absence of observations does not establish GPU residency. Compatible
load timing is explicitly unavailable unless later instrumentation establishes it.

Mechanical validation rejects malformed/duplicate/unexpected fields, invalid
statuses, missing summaries, unknown sources, changed quotes/values, and missing
required evidence declarations. It cannot prove semantic correctness or that the
model considered every source. Expected-label comparison is only one review aid.
Every completed answer retains `semanticReview: pending` and `overallPass: null`.
Review each answer's compound rules, cohort uncertainty, evidence interpretation,
and scope with the human. These six one-off cases do not establish acceptance,
clinical validity, or repeated-run consistency. Numerical latency acceptance is
still undecided.

Retain outputs through evaluation/review; do not overwrite or automatically delete
them. A changed configuration/prompt/fixture is a new fingerprint and requires a
new reviewed run. Keep all scheduled attempts; no cherry-picking or silent retries.
Reviewed findings may later be committed with approval. Raw answers, timings, and
model downloads are not proposed for Git. Raw evidence can be separately reviewed
for publication/archive/deletion when no longer needed. First-trial findings and
the offline diagnosis remain local under results/ for review; no revised candidate
has been evaluated with inference.

## Official references

- [Phi-4 Mini Q4_K_M Ollama listing](https://ollama.com/library/phi4-mini:3.8b-q4_K_M)
- [Microsoft Phi-4 Mini model card](https://huggingface.co/microsoft/Phi-4-mini-instruct)
- [Qwen3 4B Instruct 2507 model card and recommended sampling](https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507)
- [Ollama native chat request/metrics](https://docs.ollama.com/api/chat)
- [Ollama structured outputs](https://docs.ollama.com/capabilities/structured-outputs)
- [Ollama OpenAI compatibility and context settings](https://docs.ollama.com/api/openai-compatibility)
- [Ollama context and offloading](https://docs.ollama.com/context-length)
- [Ollama 0.40.13 sampling defaults](https://github.com/ollama/ollama/blob/v0.40.13/api/types.go#L1130)

This preparation is AI-assisted. Offline tool checks are distinct from model
evaluation, independent automated code review, and maintainer manual validation.

Current local validation: 48 deterministic offline checks passed. This includes
the 36 checks after the schema/build fixes plus D1 selection, payload identity,
scope/count approval guards, and single-request fake transport. Earlier coverage
includes standalone schema regression and actual MSBuild result source exclusion,
along with:
fixture agreement with the approved design, exact evidence/type checks, malformed
and duplicate JSON, API request/response adapters, expected-label comparison,
oversized-input detection, execution approval guards, fake HTTP failures,
deadline, and cancellation. Both revised six-request dry-run previews and the
native D1-only preview were generated. The installed native converter was also
exercised offline: saved old
schema fails the duplicate-result assertion; revised schema passes. No inference
was performed for these fixes; no application test rerun, independent automated
code-review run, or maintainer manual validation is claimed. Previously reviewed
context/output settings are unchanged. Fresh explicit execution approval and
verification of installed metadata are required before further inference.
