# Condition-label evaluation runner

One isolated development console with shared preparation, validation, recording,
and offline checks for two local Ollama routes. The app does not reference this
project. Ordinary live execution remains disabled; the completed two-request
smoke exception is described below and is now disabled too. Prompt optimization is deferred; all
19 acceptance cases remain reserved.

The selected historical development baseline remains original v5 plus both
original result-field descriptions, GPT-OSS 20B low through native Ollama.
Both routes passed the one-case live smoke below. This is limited integration
evidence; compatible-route quality and acceptance remain unestablished.

## Assets and verification

- `baseline/`: 21 unchanged assets: prompt, full/subset settings, pinned model
  metadata, development inputs and all 16 frozen requests.
- `baseline.sha256.json` and `audit-baseline.cjs`: portable byte-integrity check.
- `Labels.cs` and `SchemaGuidance.cs`: retained schema, validator, pair aggregation
  and original descriptions. Unused narrative/Qwen code has been removed.
- `results/`, `bin/`, `obj/`: ignored, disposable outputs.

From the repository root, with the .NET 10 SDK:

```powershell
dotnet run --project tools/condition-label-evaluation -- prepare
dotnet run --project tools/condition-label-evaluation -- prepare openai-compatible
dotnet run --project tools/condition-label-evaluation -- self-check
node tools/condition-label-evaluation/audit-baseline.cjs
```

Node is required only for the byte-integrity audit. Preparation and self-check
need no Ollama service, network, frontend dependencies or `.local/` directory.
`prepare` defaults to `native`; `prepare native` is equivalent. Unknown routes
are rejected. Each invocation prepares one fixed development candidate, with no
automatic switch or fallback. Both use all 16 development cases, unchanged
instructions, schema guidance, and source catalogs. Native request content is
compared with all 16 frozen requests. Compatible requests preserve the same
messages/schema inside the different transport envelope.

`self-check` prepares its own current native and compatible previews and exercises
the shared recorder with fake HTTP. Its progress messages describe synthetic
responses, including intentional failures, and do not represent live inference
or semantic validation. It checks metadata failures, response/evidence failures,
cancellation, request timeouts, controlled-clock operation-deadline expiry,
guards, recording privacy, and preparation tampering.

## Routes and configuration

| Route | Inference endpoint | Candidate model | Reasoning control |
| --- | --- | --- | --- |
| `native` | `/api/chat` | Existing `gpt-oss:20b` baseline | `think: "low"` |
| `openai-compatible` | `/v1/chat/completions` | Declared `oncomatch-gpt-oss-20b-openai-compatible:latest` | `reasoning_effort: "low"` |

Both request one non-streaming answer. The compatible route uses
`response_format` with the same JSON schema, `reasoning_effort: low`, temperature
0.7, top-p 0.8, seed 42, presence penalty 0, and `max_tokens: 8192`.
`keep_alive: "5m"` is an Ollama-specific extension. This runner targets local
Ollama 0.40.13; Responses, other servers, streaming, and cloud inference are
outside this implementation.
Low reasoning is sent on each request; it is not a parameter in the model
definition. The preset controls the four model-side settings below.

The existing native model and its frozen assets are untouched. The compatible
candidate declares a separate model derived from the same source weights with:

| Model parameter | Required value |
| --- | --- |
| `num_ctx` | 24576 |
| `top_k` | 20 |
| `min_p` | 0 |
| `repeat_penalty` | 1 |

These controls are not compatible request fields in the pinned server. They
must be explicit model parameters, not undocumented extra request fields or
assumed defaults. The runner checks a closed declaration and rejects unresolved
or changed required controls. It never maps repeat penalty to frequency penalty.

For a later, separately approved model-creation step, review a model definition
using the existing pinned `gpt-oss:20b` source and the four parameters above.
Do not run model creation or download commands as part of offline setup.
Preparation needs no model installed and generates no model in Ollama.
Keep the named preset for future live use of this compatible-runner configuration.
Native calls use the original model. Offline checks and the current application
need neither preset setup nor a running inference service. Application inference
instructions belong to the later matching slice; if it adopts this approach,
document how to create/select the preset from existing weights rather than
presenting it as a separately downloaded OncoMatch model.

The offline configuration records the source digest separately and leaves the
derived model digest null with `modelVerification: not-verified`. Its declaration
does not attest to the current server state. The separate smoke approval froze
and verified the created preset's actual digest. Before a future live evaluation, verify the source
weights provenance, freeze the actual derived digest, verify the model's four
parameters/version/template, refresh preparation, and obtain a new execution
approval. Offline fake metadata exercises verification without establishing
anything about the installed server.

## Outputs and limits

Both routes use `results/preparation/<source-fingerprint>/`, with distinct
candidate-profile subdirectories and a shared manifest. The compatible profile
is `development-full-low-gpt-oss-20b-compatible`; the original native profile is
`development-full-low-gpt-oss-20b`. Each contains its configuration and 16 request
previews. The manifest records route, endpoint, configuration hash, per-case
hashes and sizing, and unverified server status. Preparing the other route keeps
the first candidate's entries. Regenerate previews after source changes.

Offline fake executions use the existing ignored results area. Attempt and
summary records identify route/profile. Only final answer content and allowed
numeric metrics are recorded; raw envelopes and separated reasoning are omitted.
Missing compatible metrics remain unavailable. No automatic retry, repair, or
route fallback is performed.

The recorded controls remain 600 seconds per request, 9,660 seconds per candidate,
8,192 output tokens, a 4,096-token framing reserve, sequential case order and one
attempt. Those values describe candidates and sizing; they authorize no execution.

## Status and references

All previous live approvals are consumed. `run <profile> <approval>` rejects
production execution on both routes before any HTTP, including native metadata
requests, even with a syntactically complete approval. Fake transport is internal
to self-check and has no public bypass flag. A future live run requires a reviewed
execution design, model setup/identity verification, a new run identity and fresh
human approval before enabling it.

### Completed two-request smoke exception

The human separately approved creating the named preset from existing local
weights and one CL-D01 request per route before committing. Both requests retain
low reasoning, original prompt/schema, a maximum 8,192 output tokens, no retry or
fallback, and a 600-second request deadline. The smoke operation also has a
600-second overall deadline per route. No acceptance fixture or tuning is allowed.

The same runner's `smoke <profile> <approval>` command requires an exact approval
bound to source fingerprint, candidate configuration, generated request hash,
actual model digest, supported low reasoning, one case, and one request. The
only production identities for this approval are `smoke-20261102-native-1` and
`smoke-20261102-openai-compatible-1`. Claims are written before metadata HTTP;
replaying a claimed identity fails even if the prior attempt failed. Arbitrary
fresh run IDs, missing approvals, larger budgets and changed requests are rejected.
Both identities are consumed, and production smoke execution has also been
disabled in source. The normal `run` command remains disabled. Fake smoke guard
tests do not contact Ollama. Completion grants no further execution permission.

| Route / model | Live CL-D01 result | Request time |
| --- | --- | --- |
| Native / `gpt-oss:20b` | Valid response/evidence; all expected findings matched | 97.836 s |
| Compatible / preset with the same digest, tested before its rename | Valid response/evidence; all expected findings matched | 52.541 s |

Both explicitly requested low reasoning. AI-assisted final-answer inspection
found no material issue in this case; no independent live-output review or maintainer manual
validation is claimed. The two sequential timings are observations, not a
controlled performance comparison or a latency acceptance result. All 19
acceptance fixtures remain reserved. After the source-review fix, offline checks total 126, with
all 21 native asset hashes intact and a clean Release build.

Separate automated Standards and Spec source reviews against `d234666` are
complete. Standards had no actionable findings. Spec found missing operation-
deadline coverage; deterministic tests now drive the actual deadline on both
routes, preserving two completed results, interrupting the third and recording
the deadline flag without another request. Both follow-up reviews cleared the
fix. These source reviews are separate from test execution and live-output review.

The local preset is retained as `oncomatch-gpt-oss-20b-openai-compatible:latest`
and shares the existing weights/template; creating
it downloaded no weights and changed no native baseline assets. `ollama ps` can
display the original model when Ollama reuses its loaded runner for the preset.
It is not request history. The recorded compatible request and returned identity
were checked against the preset, not inferred from that resident-model display.
[Pinned scheduler behavior](https://github.com/ollama/ollama/blob/v0.40.13/server/sched.go#L130).
Historical request/approval artifacts retain the prior alias and their original
hashes. The rename preserves the preset digest and performs no new inference;
it is not a new live validation result.

The local summary is `results/smoke-20261102-summary.json`, with sanitized attempt
records and source snapshots under each one-use run identity. The historical
live source fingerprint is `1f6a7214e927c1b26272261b7198a55b3af36b0b1fab5848a18d5d27728d40b3`;
later disarming and documentation produce a different preparation fingerprint.

The offline extension implements [issue #18](https://github.com/dayloryanes/onco-match/issues/18).
Its automated checks establish runner behavior and native reproducibility only.
Neither offline checks nor the limited live smoke establish route equivalence
or pass the open model-quality gate.

See [baseline and findings](../../docs/evaluation/condition-label-baseline.md) for
the exact configuration, outcomes, limitations and model/code boundary.
Raw historical evidence and local audits live under the ignored workspace
directory `.local/condition-label-evaluation/`; they are supplementary and never
required to understand the report or run these offline checks.
