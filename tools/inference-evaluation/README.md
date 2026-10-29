# OncoMatch inference evaluation

A separate C# console tool for evaluating OncoMatch's focused assessment task
through local Ollama. No application project references, added packages, model
downloads, provider framework, or automatic model search. It is not part of API
startup or a product feature. .NET 10 SDK is required.

## Current checkpoint and scope

The original narrative-assessment feasibility work has stopped. None of the
tested configurations met its development quality requirements. This reusable
console still contains the historical narrative fixtures and response contract;
condition-label assessment has not been implemented. [Scope decision #12](https://github.com/dayloryanes/onco-match/issues/12)
records the agreed narrowed direction and unresolved contract/evaluation questions.
[Model checkpoint #6](https://github.com/dayloryanes/onco-match/issues/6) records the
measurements. Further experiments wait for the revised design and exact approval.

| Latest completed native configuration | Mechanical completions | All expected criterion labels match | Total request time |
| --- | ---: | ---: | ---: |
| Phi Mini, corrected evidence request | 0/6 | 0/6 raw answers | 269.156s |
| Qwen Instruct-2507, same corrected request | 6/6 | 2/6 | 513.617s |
| Qwen3 4B hybrid, explicit thinking | 5/6 | 0/6 | 1090.016s |

Each batch used D1/D3/D5/D6/D9/D13 once, without retries. Phi/Instruct requests
differed only by model. The hybrid trial also changed reasoning mode, sampling
and deadline, so it does not isolate thinking's causal effect. Mechanical
completion and matching labels do not prove correct interpretation. Full repeated
acceptance and live compatible-route comparisons remain unrun.

The retained candidate.json is the last tested ordinary hybrid
qwen3:4b-q4_K_M profile: explicit think:true, temperature0.6, top-p0.95, top-k20,
min-p0, seed42, context12288, combined generation cap2048 and deadline300s. It is
not a selected POC model or the next condition-label candidate. The completed
hybrid batch observed separated reasoning in all six responses; only a boolean
is retained. D1 hit the generation cap without final JSON. Its five completed
answers contained interpretation errors. No raw reasoning is retained.

Earlier structural fixes flattened a schema composition that forced duplicate
result keys and excluded saved result sources from compilation. Later request
corrections defined evidence roles, specialized IDs from each supplied catalog,
and included the same schema in messages and the API format field. Expected
answers remain outside requests. Those corrections improved some mechanical
results but did not establish reliable narrative interpretation.

An earlier hybrid D1 remained unfinished across Windows Modern Standby. A local
postmortem found that UTC elapsed time alone did not demonstrate a timeout defect: the
.NET Windows relative timer excludes sleep time. Two local-stub HTTP deadline
probes passed. The fresh six-case batch finished without timeout. The user
controls power settings; no automatic sleep-prevention behavior was implemented.

Local exact inputs, consumed approvals, answers, measurements and diagnostics are
retained under ignored results/. All prior attempts remain intact. No model is
accepted, no narrowed-scope trial has run, and no run starts automatically.
Separate local commits preserve the experimental runner checkpoint and the scope
draft. No push, new inference or application change follows from those commits.

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
treatment fields are added. The model sees the catalog, instructions, and schema,
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
handlers. For the current explicit-thinking candidate, use native previews only;
the compatible command above applies to configurations without that control.
Self-check retains legacy compatible coverage separately. It uses
fake HTTP handlers, not an Ollama or fake-network server. The ordinary .NET
build may perform SDK restore work; the project has no package references.
Dry-run writes six complete request previews and `manifest.json` under
`results/preparation/<fingerprint>/<route>/`, which Git ignores. Omitting a
command defaults to native dry-run. There is no background process.
`dry-run-d1` accepts no route or other arguments: it previews exactly D1 through
the native API, under `results/preparation/<fingerprint>/native-d1/`. Its manifest
records one request and the configured request deadline. The usual
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

The current prompt and schema are retained evidence of the narrative experiment.
They preserve compound requirements, cohort uncertainty and supplied staging
labels, define evidence roles and missing/null sources, and derive allowed source
IDs from inputs without answer-key examples. They must be redesigned only after
#12 resolves the narrowed contract; old fixtures are not acceptance evidence for
that future task. D1-only mode remains available for separately approved
structural diagnostics.

The latest Phi/Instruct completion runs used temperature0.7, top-p0.8, top-k20,
min-p0, seed42, context12288, generation cap2048 and deadline120s, without a
separate thinking control. The retained hybrid profile uses the values in the
checkpoint above. Explicit thinking is native-only; compatible previews reject it
rather than silently dropping the setting. Omitting a think field does not
generally prove reasoning is disabled for every model; verify model controls.

Installed metadata and template hashes were inspected per candidate. Phi uses
gpt2/gpt-4o pretokenization; the Qwen candidates use gpt2/qwen2. Their reviewed
two-message/no-tools template paths fit the framing reserve. Metadata, generation
metrics, residency and exact hashes remain in local records. Download size is
not loaded memory, and a4096-context observation is not a12288-context comparison.
No generic GPU-fit or speed guarantee is made.

A seed is not a guarantee of identical answers. Native keep_alive is5 minutes to
avoid deliberately reloading between cases. The runner does not alter global
Ollama settings or explicitly unload another model. Request deadlines are the
reviewed configured120 or300 seconds; Windows relative-timer sleep behavior must
be considered when interpreting UTC durations.

**Reviewed baseline limits:** 12,288 context tokens, 2,048 maximum output tokens,
and a 1,024-token chat-framing reserve. An initial
8,192-token proposal did not satisfy the deliberately conservative byte bound;
12,288 was selected to clear that bound, not through inference. Larger
context can increase memory use/offloading. A tokenizer-based count could justify
a smaller context later, but no tokenizer dependency/download is added here.

Sizing sums UTF-8 bytes in decoded system/user message content, then adds framing
reserve and maximum output allowance. The response schema in the system message
counts once. API-only schema/format fields, options, and HTTP JSON escaping are
excluded from the token bound. `requestUtf8Bytes` still records whole-request
bytes for diagnostics; `messageContentUtf8Bytes` records the input to the bound.
This overcounts content tokens for the candidate's byte-level tokenizer; it is
not an exact token count, a memory estimate, or a universal tokenization method.
Sizing rejects message fields beyond role/content, conversations beyond system
then user, and supplied tools; those paths require their own framing review.
Before execution, inspect the installed tokenizer/chat template and approve its
fit within the framing reserve. The execution gate also verifies tokenizer type
and template hash. Input above the bound stops the run without truncation.
Output-limited answers fail; they are never completed assessments.

On 2026-10-29, read-only metadata verification confirmed Ollama 0.40.13 and Phi's
gpt2 byte-level tokenizer with gpt-4o pretokenization, `go_template` source, no
embedded messages/default system prompt, and the previously recorded template
SHA-256 `813f53fdc6e58d35bb1c3853c93266380e9ca918a993e8eab193e8ede5d3a603`.
For the selected two-message path without tools, the template inserts role/end
markers and an assistant prefix within the 1,024 reserve; it does not repeat the
contents. Ollama's [prompt renderer](https://github.com/ollama/ollama/blob/v0.40.13/server/prompt.go)
renders messages/tools/thinking values, while the [completion transport](https://github.com/ollama/ollama/blob/v0.40.13/llm/llama_server.go)
passes JSON schema separately from the rendered prompt. The API format copy
therefore does not add a second schema to this candidate's input. This review
does not establish equivalent live settings for the compatible endpoint or a
framing guarantee for another model/provider/template.

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
sampling, context/output limits, or D1 payload. It applies the configured request
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
for publication/archive/deletion when no longer needed. All trial findings and
the offline diagnosis remain local under results/ for review. The completed trials are summarized above.

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

Current local validation: 67 deterministic offline checks passed. Six added checks
cover explicit thinking on/off payloads, rejection of silently dropped compatible
thinking, reviewed 120/300-second configurations, rejection of unreviewed deadlines
and non-boolean controls, and rejection of reasoning without final JSON. The
existing response test also checks the reasoning-observed boolean and no raw text
retention. Six earlier sizing
checks cover decoded Unicode byte counts and reserves, API packaging versus added
message text, exact-limit acceptance/overflow rejection without truncation,
matching adapter bounds/all six inputs fitting, and rejection of unreviewed media
or tools. Seven earlier additions
exercise schema grounding in both adapters, per-input source-ID categories and
presence, reusable-schema isolation, answer-key independence, and continued
rejection of misattributed quotations, patient examined-source IDs, and positive
claims without patient evidence. These establish request construction and guard
behavior; they do not establish improved model interpretation. The installed
native schema converter also processed request-specific D5/D6 schemas offline
without forcing duplicate result fields; this does not establish model quality.
Earlier coverage includes
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
context/output settings are unchanged. The latest previews clear the corrected
conservative sizing guard as documented above. Fresh explicit execution approval
and verification of installed metadata are required before further inference.
