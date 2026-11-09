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
After an operation stops, any Operator can choose Continue unfinished for pairs
never completed or interrupted, or Retry failed for currently failed pairs. Each
action captures its eligible pairs once, starts a new operation, and grants a
fresh 30-minute session. It follows the same cancellation, timeout, streaming,
and single-operation rules as Start. A stale or concurrent action is rejected;
review the refreshed batch before choosing another action. A connection failure
does not itself establish that a pair failed: saved backend state is authoritative,
and recovery stays unavailable while the prior operation is still stopping. If
reconciliation cannot be confirmed, use Refresh before taking another action.

Retry failed makes one new attempt per selected pair and ends. Repeated failures
remain visible as Finished with failures when no unfinished work remains; another
manual retry is available without a fixed retry cap. There is no automatic retry,
resubmission, or recovery job. Cancelling a retry returns its interrupted pair to
unfinished while keeping its earlier failure in history. Continue starts a new
attempt for that pair. Completed assessments, including exclusions, undetermined
and mixed findings, are immutable and never selected by recovery. There is no
rematching or correction of completed results.

Inspect is available for pairs with an attempt, including failed or interrupted
work. Attempt history lists each attempt's identity, operation, start/finish,
status, safe failure explanation, and available route, model, timeout and duration
metadata. Earlier failures remain after success; only a completed pair has a pair
summary. Viewers may inspect shared history but cannot run recovery. A backend restart clears all imported,
assessed and attempt-history state.

## Patient and Study perspectives

Patient details list that patient's pairs across imported studies in source import
order. Study details list that study's pairs in the fictional patient catalog's
order. Both roles read the same saved states and assessments as Dashboard.
Only completed pairs have a pair summary; failed, unfinished, and running pairs
retain their execution states. Inspect is available once a pair has an attempt.

Each detail page places the assessment-limits notice beneath the Assessments
heading. In-page inspection uses the same findings, evidence, exact input snapshot,
and attempt-history display as Dashboard. Refresh assessments explicitly rereads
shared results, preserving an open inspection and snapshot. Failed reads leave
previously loaded results visible and offer retry. No polling or inference starts
from these pages, and they provide no execution or recovery controls.

Leaving the initiating Dashboard for either perspective stops its active work
once disconnection is detected; committed assessments remain. Browsing from an
independently signed-in session does not cancel another session's operation.
Patient and Study directories retain their existing navigation and source display;
there is no separate Results page.

## Local Ollama setup

Use the normal application startup in the project README. Inference is backend
only. Install and run a local Ollama service separately, with the reviewed
`gpt-oss:20b` weights available. The retained development evidence used Ollama
0.40.13; this application has automated fake-transport validation, not a new live
model/route compatibility or quality pass. No automatic model download occurs.

### Prepare the local service

Install Ollama using its [official setup instructions](https://docs.ollama.com/quickstart)
and open the local application. If the service is not already running, start it
in a separate terminal with `ollama serve`; keep it running while using assessments.
The default local service address is `http://localhost:11434`.

Check installed models:

```powershell
ollama ls
```

If `gpt-oss:20b` is missing, download it explicitly before starting assessments:

```powershell
ollama pull gpt-oss:20b
```

Both routes use these source weights. A fresh download does not by itself
reproduce or validate the recorded development baseline. These are manual setup
instructions; installing models or running inference is separate from the
automated application checks. See the [Ollama CLI reference](https://docs.ollama.com/cli).

### Application configuration

In Visual Studio or another editor, open
[`backend/src/OncoMatch.Api/appsettings.json`](../backend/src/OncoMatch.Api/appsettings.json).
Its `Match` → `Inference` section contains these settings:

```json
{
  "Route": "native",
  "BaseUrl": "http://localhost:11434",
  "NativeModel": "gpt-oss:20b",
  "OpenAiCompatibleModel": "oncomatch-gpt-oss-20b-openai-compatible:latest",
  "RequestTimeoutSeconds": 600
}
```

To select a route by editing this file, change `Route` to `native` or
`openai-compatible`, save, then stop and restart the backend. The application
selects `NativeModel` or `OpenAiCompatibleModel` accordingly. No frontend change
is needed. A `Match__Inference__Route` environment variable, if set in the backend
process, overrides this file; remove or update that variable when using the
editor-based setting.

Choose `native` (`/api/chat`) or `openai-compatible`
(`/v1/chat/completions`). Both model names can be configured in advance.
Environment variables use names such as `Match__Inference__Route`.
Settings are captured at startup; restart and reimport studies after changing
them. An invalid configuration disables match actions with a message while other
features remain usable. BaseUrl must be a local HTTP(S) origin, without path,
query, fragment, or credentials. Redirects and route/model fallback are disabled.
The timeout covers each entire provider HTTP request, including the response body;
it is not a model-quality latency criterion or a whole-operation time limit.

### Compatible model preset

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

### Select a route and start the application

From the repository root, choose the route in the terminal that starts the
backend. For native Ollama:

```powershell
$env:Match__Inference__Route = 'native'
dotnet run --project backend/src/OncoMatch.Api --launch-profile https
```

For the compatible route, first create the preset above, then use:

```powershell
$env:Match__Inference__Route = 'openai-compatible'
dotnet run --project backend/src/OncoMatch.Api --launch-profile https
```

Choose one backend command, not both. The environment setting overrides
`appsettings.json` for that terminal. To return to the configured default, remove
it with `Remove-Item Env:Match__Inference__Route` before starting the backend.
To switch routes, stop the backend and start it with the other value; restarting
clears imported studies and assessments. Use the same frontend startup commands
and demo credentials from the [README](../README.md#run-locally) for either route.
No OpenAI API key is needed: both options target the local Ollama service.

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
