# Batch assessments

Dashboard lists one fixed batch for each completed
import: its newly imported studies paired with all ten fictional patients.
Later imports leave those pairs unchanged. Duplicate-only imports show No work;
failed and cancelled imports do not become match batches. Importing never starts
inference.

The Dashboard subtitle reads Import studies · Run assessments · Review results.
The search-scope notice is followed by the import action and Refresh. Beneath those
buttons, the study count (for example, 60 studies) precedes a middle dot and one
sentence explaining shared session visibility and clearing on server restart.
The cards follow directly below.

An Operator chooses Start match. Only one match operation can run across the
application. Stale actions and concurrent starts are rejected without queuing.
Both structured age and sex checks are retained; reliable exclusions skip AI.
Missing condition text also skips AI and yields an undetermined assessment.
Other pairs receive one sequential request containing all nonblank condition
labels and only the supported cancer, stage, and disease-state patient facts.
Source positions remain those of the original registry conditions.

Each history card shows import details on the left and its matching batch's
status, counts and controls on the right; the columns stack on narrow screens.
View batch expands results directly beneath those columns within the same card.
Close batch or Collapse results closes that section without cancelling active
work. Live summary counts and Cancel remain available while results are collapsed.

The initiating Dashboard receives progress and completed results as they are
saved. Inspect expands assessment details immediately beneath that patient-study
row. The row's Close details toggle or the inline Close assessment button
collapses the inspection and returns focus to Inspect, preserving active work.
Close assessment sits beneath the white inspection panel with separate spacing;
the row's Close details toggle remains available above it.
The assessment-limits notice appears once above the batch results, without
repeating inside each inspection. Exact input snapshot groups facts into a
Patient section followed by a separate Study section.
Collapse results has separate spacing beneath the table. Details include
structured checks, label findings, evidence, explanations,
unassessed qualifiers, scope limits, and retained input facts. Viewers and other
sessions use the single Refresh button beside the import action for shared state;
they do not subscribe or poll. Refresh updates import history, batch summaries and
the open batch's saved rows without closing results, inspection or its snapshot.
At the top of Dashboard, Refresh keeps the controls in view. Farther down, it
keeps the visible content in place when new cards appear above it, including when
Refresh is triggered programmatically. Each import or assessment update preserves
the current view, including deliberate scrolling while requests are pending.
Leaving Dashboard does not restore an old reading position on the destination.
Refresh does not interrupt an initiating operation's live progress, import studies
or start assessments.

Cancel is available only to the initiating Operator session. Leaving Dashboard,
logout, actual session expiry, and a lost initiating connection also stop work
once detected and request cancellation of active inference. Already saved
assessments remain. Successful admission grants a fresh session; actual cookie
renewal in a shared tab extends active work. Focus checks preserve the Dashboard
stream. Heartbeats every 15 quiet seconds do not renew the login.

Timeouts and invalid answers/evidence fail individual attempts while remaining
pairs continue. Unavailable inference at first need and unexpected internal errors
stop the operation. No failure becomes a fabricated clinical finding. A broken
stream is reconciled against saved state without automatic resubmission.
Continue/retry controls and Patient/Study assessment sections belong to later
slices. Interrupted/failed work remains visible but cannot be restarted in this
slice; a backend restart clears it together with all imported/assessed state.

## Local Ollama setup

Use the normal application startup in the project README. Inference is backend
only. Install and run a local Ollama service separately, with the reviewed
`gpt-oss:20b` weights available. The retained development evidence used Ollama
0.40.13; this application has automated fake-transport validation, not a new live
model/route compatibility or quality pass. No automatic model download occurs.

The API's `appsettings.json` contains the `Match:Inference` section:

```json
{
  "Route": "native",
  "BaseUrl": "http://localhost:11434",
  "NativeModel": "gpt-oss:20b",
  "OpenAiCompatibleModel": "oncomatch-gpt-oss-20b-openai-compatible:latest",
  "RequestTimeoutSeconds": 600
}
```

Choose `native` (`/api/chat`) or `openai-compatible`
(`/v1/chat/completions`). Both model names can be configured in advance.
Environment variables use names such as `Match__Inference__Route`.
Settings are captured at startup; restart and reimport studies after changing
them. An invalid configuration disables match actions with a message while other
features remain usable. BaseUrl must be a local HTTP(S) origin, without path,
query, fragment, or credentials. Redirects and route/model fallback are disabled.
The timeout covers each entire provider HTTP request, including the response body;
it is not a model-quality latency criterion or a whole-operation time limit.

For native requests, use the original model. For the compatible route, use the
separately named preset derived from the same existing weights. If it is already
installed, no creation is necessary. To create it manually from existing weights,
save the following as a local `Modelfile`:

```text
FROM gpt-oss:20b
PARAMETER num_ctx 24576
PARAMETER top_k 20
PARAMETER min_p 0
PARAMETER repeat_penalty 1
```

Then run:

```powershell
ollama create oncomatch-gpt-oss-20b-openai-compatible -f Modelfile
```

This derives a preset, rather than downloading a separate OncoMatch model.
It retains source weights/template; low reasoning is sent on each request.
Do not substitute cloud inference. Before the first generated answer, the API
checks `/api/show` for the reviewed GPT-OSS tokenizer/Harmony/low-reasoning
profile and rejects reported remote models. Compatible setup must explicitly
provide the four preset controls above. If this check fails, verify the model
name, local service, and preset rather than switching routes silently.
Other models/servers are not certified by editable settings.

Both routes request nonstreaming model answers. Dashboard uses a separate
newline-delimited progress stream through Vite. Retained controls are low
reasoning, context 24576, output allowance 8192, framing reserve 4096,
temperature 0.7, top-p 0.8, top-k 20, min-p 0, seed 42, repeat penalty 1,
presence penalty 0, and keep-alive five minutes. The conservative context guard
rejects oversized inputs without truncating them. The application uses
`conditionLabels` and `conditionLabelId`; experimental runner assets retain their
historical schema and are not application dependencies.

## Assessment limits and validation

These are limited research-screening assessments, not full clinical eligibility
or enrollment approval. Structured uncertainty remains visible alongside pair
summaries. Mechanical response/evidence validation cannot establish semantic
correctness. Raw model reasoning is neither displayed nor retained.

The [selected development baseline](evaluation/condition-label-baseline.md)
remains a working choice with known interpretation/evidence/explanation defects.
No application quality acceptance, numerical latency acceptance, or clinical
validity is implied. All 19 acceptance fixtures remain reserved. Live inference,
model changes, downloads and evaluations require their own approval in the
development workflow.

Automated checks use deterministic fixtures, fake registry/Ollama HTTP handlers,
and controlled API time. React checks cover controls, inspection and progress
parsing/reconciliation, including split UTF-8. Chromium exercises the real API,
secure cookies, Vite streaming/heartbeats, Dashboard inspection and cancellation
against a fake external service. Test execution, automated source review, and
maintainer manual validation are separate evidence. Current review/delivery
status belongs in the [development overview](https://github.com/dayloryanes/onco-match/issues/1).
