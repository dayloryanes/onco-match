# Condition-label evaluation design

Date: 2026-10-30. Decision: [scope revision #12](https://github.com/dayloryanes/onco-match/issues/12).

Status: approved local design record. The human approved all 35 case expectations
through case-by-case summaries and then approved the complete written package,
including exact fixture text and serialized examples, on 2026-10-30.
This design approval does not authorize execution or publication.
The human approved seven real-study cases, replacing the initial four, after
reviewing the coverage recommendation. No inference has been run for this task. No model,
prompt, API route, or inference settings are selected by this document.

Read [Condition-label assessment design](condition-label-assessment-design.md),
the [baseline](poc-1-baseline.md), and [glossary](../../GLOSSARY.md). Preserve the
[old narrative evaluation](inference-evaluation-design.md), its fixtures, and
retained outputs as history. These new cases are not relabelled old passes.
The ten application patients and live import behavior are unchanged.

Review progress: the human approved the CL-D01 through CL-D16 development-case
expectations, CL-A01 through CL-A12 controlled acceptance expectations, and
CL-R01 through CL-R07 real-study expectations after case-by-case summaries.
Final written-record/serialized-example approval is complete. No case has been run
through a model as part of this review.

## Evaluation method and authorization

- Development: 16 controlled cases, CL-D01 through CL-D16. Failures may guide
  prompt/settings changes within a separately approved development budget.
- Acceptance: 12 controlled cases, CL-A01 through CL-A12, plus seven frozen
  real-study cases, CL-R01 through CL-R07: 19 cases total. The allocation is
  approved, including the written fixture records and expected findings.
- Three repetitions per acceptance case under a frozen candidate configuration.
  The original 16-case plan gave 48 responses. The approved revised allocation
  gives 19 cases and 57 responses per frozen candidate configuration.
- Every scored response must pass structure/evidence checks and the reviewed
  interpretation, uncertainty, qualifier, and explanation expectations. A correct
  overall pair summary does not compensate for wrong supporting findings.
- A candidate configuration includes model/tag, prompt, schema, route, inference
  controls, software versions, input catalog, deadline, and latency limit.
  Acceptance repetitions do not change these or omit inconvenient cases.
- Mechanical completion, semantic quality, and clinical validation are distinct.
  This is a deliberately small POC evaluation, not clinical validation or a
  statistical guarantee of generalization.
- Development/acceptance are separate inputs with fixed expected answers before
  model output. Seeing expected answers during design is not seeing model output.
  Reserved acceptance must not become a prompt-tuning loop. If acceptance failures
  guide changes, retire those cases from acceptance and independently review
  replacement cases before another scored assessment. Do not silently rerun
  until a favorable answer appears or change expectations to excuse failures.
- A separately approved small development run measures practical feasibility.
  Record each duration, median, slowest request, and total including failures.
  Agree numerical latency acceptance after those measurements and before frozen
  acceptance. Timeouts are failures; fast incorrect answers still fail quality.
- Route comparisons use equivalent reviewed inputs/settings where possible.
  Each materially different route/configuration is separately identified and
  judged; no automatic doubling of the request budget is approved here.
- This document authorizes no model request, download, application/runner change,
  commit, publication, or issue closure. Exact candidate controls, pilot subset,
  deadlines, request/token/context limits, and budgets need separate review.

## Fixture conventions and exact patient inputs

These are fictional evaluation inputs, not edits to application patients.
The controlled base patient is:

```json
{
  "ageYears": 52,
  "sex": "female",
  "cancerType": "Breast cancer",
  "stage": {
    "status": "known",
    "value": "II",
    "classificationSystem": "Demo staging system A"
  },
  "diseaseState": { "status": "known", "value": "recurrent" }
}
```

`Demo staging system A` and `Demo staging system B` are fictional classification
names, not real clinical systems. Controlled labels explicitly use stage at
diagnosis to avoid assuming it equals current extent. Roman/Arabic stage
notation variations in these fictional fixtures denote the same specified group;
they do not license staging-system or edition conversion in real records.

Every controlled case uses structured study minimum `18 Years`, maximum
`65 Years`, and sex `ALL`. Thus age is Satisfied, sex is No restriction identified,
and inference is permitted. These fields are retained by code, not delegated to
the model. No other study text is supplied to AI. The Conditions column below
contains the complete exact JSON array, not snippets from a larger input.

Patient changes are JSON object-property replacements applied to the base.
Replacing `stage` or `diseaseState` replaces the whole object. No omitted nested
value is inherited. Source IDs map to each original label index and patient
field as in the assessment design. Labels and JSON strings are decoded once;
code does not normalize evidence to make fabricated quotations match.

Expectation abbreviations in the case tables:

| Symbol | Finding / label value |
| --- | --- |
| S | `alignmentSupported` |
| C | `conflictIdentified` |
| U | `undetermined` |
| N | `notSpecified`, only for a direct stage/state finding |

Each tuple is `(cancerType, stage, diseaseState; complete-label result)`, in
original label order. Pair summary S means Condition-label alignment supported;
C means Condition-label conflicts identified; U means Condition-label alignment
undetermined; M means Mixed condition-label findings. These symbols are document
notation only, not extra API result values.

All cases require exact source evidence for positive claims and patient-dependent
comparisons; no invented null replacements, biomarkers, current disease extent,
or narrative requirements. N identifies the complete examined label, not a full
study absence claim. No raw model reasoning is requested or retained as an
assessment explanation. Expected flags and rationale below are part of scoring,
not optional comments. Case-specific expectations and their exact written
representation were reviewed and approved by the human.

## Development cases: exact inputs and reviewed expectations

### CL-D01: name casing

Patient changes: `{}`.

Conditions: `["BREAST CANCER"]`.

Expected: `(S,N,N;S)`; pair S. Case variation does not create a cancer conflict.
Cite the actual uppercase label and exact supplied patient value.

### CL-D02: unrelated cancer

Patient changes: `{}`.

Conditions: `["Lung cancer"]`.

Expected: `(C,N,N;C)`; pair C. Cite lung versus supplied breast cancer. Do not
claim full-study exclusion or use patient sex to infer diagnosis.

### CL-D03: explicit broad disease scope

Patient changes: `{}`.

Conditions: `["All cancers"]`.

Expected: `(S,N,N;S)`; pair S. Broad cancer wording supports correspondence with
the supplied cancer type. No stage/state restriction is asserted or invented.

### CL-D04: unknown cancer type

Patient changes: `{"cancerType":null}`.

Conditions: `["Breast cancer"]`.

Expected: `(U,N,N;U)`; pair U. Cite the supplied null. Do not infer a cancer type
from sex, stage, disease state, or the study label.

### CL-D05: comparable stage alignment

Patient changes: `{}`.

Conditions: `["Breast cancer, stage II at diagnosis under Demo staging system A"]`.

Expected: `(S,S,N;S)`; pair S. Cite stage value, classification, known status, and
the complete relevant stage qualifier. No equivalence to another system is needed.

### CL-D06: mandatory stage conflict

Patient changes: `{}`.

Conditions: `["Breast cancer, stage III at diagnosis under Demo staging system A"]`.

Expected: `(S,C,N;C)`; pair C. Same comparable classification/context; II differs
from III. Preserve cancer alignment instead of calling the cancer type a conflict.

### CL-D07: missing stage fact

Patient changes: `{"stage":{"status":"unknown","value":null,"classificationSystem":null}}`.

Conditions: `["Breast cancer, stage II at diagnosis under Demo staging system A"]`.

Expected: `(S,U,N;U)`; pair U. Cite unknown/null facts, not the base stage.
Do not fabricate a classification because the label names one.

### CL-D08: unsupported staging equivalence

Patient changes: `{"stage":{"status":"known","value":"II","classificationSystem":"Demo staging system B"}}`.

Conditions: `["Breast cancer, stage II at diagnosis under Demo staging system A"]`.

Expected: `(S,U,N;U)`; pair U. Equal numerals do not establish equivalence between
two different supplied classification names.

### CL-D09: absent qualifiers with unknown patient facts

Patient changes: `{"stage":{"status":"unknown","value":null,"classificationSystem":null},"diseaseState":{"status":"unknown","value":null}}`.

Conditions: `["Breast cancer"]`.

Expected: `(S,N,N;S)`; pair S. Unknown facts do not create restrictions absent
from the label. Do not say the full study has no stage/state restrictions.

### CL-D10: disease-state conflict

Patient changes: `{"diseaseState":{"status":"known","value":"newlyDiagnosed"}}`.

Conditions: `["Recurrent breast cancer"]`.

Expected: `(S,N,C;C)`; pair C. Cite supplied newly diagnosed state versus recurrent;
do not infer recurrence from the cancer type.

### CL-D11: unsupported qualifier without a selector

Patient changes: `{}`.

Conditions: `["HER2-positive breast cancer"]`.

Expected: `(S,N,N;S)`; pair S. Required unassessed qualifier: HER2-positive, with
an authentic quotation and biomarker-scope reason. Do not assert HER2 status or
turn this simple unsupported qualifier into an automatic in-scope conflict.

### CL-D12: unsupported qualifier selects the applicable rule

Patient changes: `{}`.

Conditions: `["Breast cancer: if biomarker X is present, stage III at diagnosis under Demo staging system A; if biomarker X is absent, recurrent disease"]`.

Expected: `(S,C,S;U)`; pair U. Direct stage comparison conflicts; recurrence aligns.
Required unassessed qualifier: biomarker X present/absent selection. Complete-label
applicability is unknown. Do not choose the favorable branch or treat the
conditional stage conflict as mandatory. References must preserve both branches.

### CL-D13: multiple labels with conflicting findings

Patient changes: `{}`.

Conditions: `["Breast cancer","Lung cancer"]`.

Expected in order: `(S,N,N;S)`, `(C,N,N;C)`; pair M. Both complete labels are
required. Do not assume both cancers are mandatory or discard the conflicting one.

### CL-D14: accepted OR alternative

Patient changes: `{}`.

Conditions: `["Breast cancer, stage III at diagnosis under Demo staging system A OR recurrent"]`.

Expected: `(S,C,S;S)`; pair S. Recurrence satisfies OR; retain the stage difference.
This is the serialized contract example, available for development rather than
reserved as a novel acceptance case.

### CL-D15: mandatory AND conflict

Patient changes: `{}`.

Conditions: `["Breast cancer, stage III at diagnosis under Demo staging system A AND recurrent"]`.

Expected: `(S,C,S;C)`; pair C. Same direct comparisons as CL-D14 but AND makes
the stage requirement mandatory. An authentic quote with label result S is wrong.

### CL-D16: unknown relationship

Patient changes: `{}`.

Conditions: `["Breast cancer, stage III at diagnosis under Demo staging system A / recurrent (relationship unspecified)"]`.

Expected: `(S,C,S;U)`; pair U. Do not invent AND/OR. Keep the direct comparisons
and explain that their relationship prevents a complete-label conclusion.

## Controlled acceptance cases: exact inputs and reviewed expectations

These different patient/label combinations have reviewed expectations and are
reserved for a frozen candidate. They test the same agreed rules; they are
not a claim of independent clinical representativeness or broad generalization.

### CL-A01: named disease variation

Patient changes: `{"cancerType":"Colon cancer"}`.

Conditions: `["COLON CANCER"]`.

Expected: `(S,N,N;S)`; pair S. Preserve exact capitalization in quotations.

### CL-A02: named disease conflict

Patient changes: `{"sex":"male","cancerType":"Prostate cancer"}`.

Conditions: `["Ovarian cancer"]`.

Expected: `(C,N,N;C)`; pair C. Compare supplied cancer types, not sex-based guesses.

### CL-A03: broad cancer wording

Patient changes: `{"cancerType":"Ovarian cancer"}`.

Conditions: `["Cancer"]`.

Expected: `(S,N,N;S)`; pair S. Cite broad cancer wording; no invented qualifiers.

### CL-A04: missing diagnosis

Patient changes: `{"cancerType":null}`.

Conditions: `["Melanoma"]`.

Expected: `(U,N,N;U)`; pair U. The supplied null remains unknown.

### CL-A05: same-system notation variation

Patient changes: `{"cancerType":"Colon cancer","stage":{"status":"known","value":"III","classificationSystem":"Demo staging system B"}}`.

Conditions: `["Colon cancer, stage 3 at diagnosis under Demo staging system B"]`.

Expected: `(S,S,N;S)`; pair S. Same fictional system and context; notation-only
III/3 variation. No real clinical staging conversion is implied.

### CL-A06: explicit stage mismatch

Patient changes: `{"sex":"male","cancerType":"Prostate cancer","stage":{"status":"known","value":"II","classificationSystem":"Demo staging system B"}}`.

Conditions: `["Prostate cancer, stage IV at diagnosis under Demo staging system B"]`.

Expected: `(S,C,N;C)`; pair C. Retain the matching cancer type and conflicting stage.

### CL-A07: unknown stage under explicit requirement

Patient changes: `{"cancerType":"Ovarian cancer","stage":{"status":"unknown","value":null,"classificationSystem":null}}`.

Conditions: `["Ovarian cancer, stage I at diagnosis under Demo staging system B"]`.

Expected: `(S,U,N;U)`; pair U. Do not copy classification or value from the label.

### CL-A08: incompatible disease state

Patient changes: `{"cancerType":"Melanoma","diseaseState":{"status":"known","value":"refractory"}}`.

Conditions: `["Newly diagnosed melanoma"]`.

Expected: `(S,N,C;C)`; pair C. Cite the supplied refractory state and label wording.

### CL-A09: two unsupported qualifiers, absent supported qualifiers

Patient changes: `{"cancerType":"Ovarian cancer","stage":{"status":"unknown","value":null,"classificationSystem":null},"diseaseState":{"status":"unknown","value":null}}`.

Conditions: `["Ovarian cancer with biomarker Z present and previous systemic treatment"]`.

Expected: `(S,N,N;S)`; pair S. Required unassessed qualifiers: biomarker Z present
and previous systemic treatment, each with evidence/reason. Unknown stage/state
does not create requirements. Neither unsupported requirement is claimed satisfied.

### CL-A10: OR satisfied through disease state

Patient changes: `{"cancerType":"Colon cancer","stage":{"status":"known","value":"II","classificationSystem":"Demo staging system B"},"diseaseState":{"status":"known","value":"refractory"}}`.

Conditions: `["Colon cancer, stage IV at diagnosis under Demo staging system B OR refractory"]`.

Expected: `(S,C,S;S)`; pair S. Refractory satisfies OR without reclassifying stage II.

### CL-A11: AND fails through disease state

Patient changes: `{"cancerType":"Melanoma","stage":{"status":"known","value":"II","classificationSystem":"Demo staging system B"},"diseaseState":{"status":"known","value":"newlyDiagnosed"}}`.

Conditions: `["Melanoma, stage II at diagnosis under Demo staging system B AND recurrent"]`.

Expected: `(S,S,C;C)`; pair C. A satisfied stage does not erase the mandatory
recurrence conflict. Do not treat AND as OR.

### CL-A12: multiple labels, one unresolved selector

Patient changes: `{"sex":"male","cancerType":"Prostate cancer","stage":{"status":"known","value":"II","classificationSystem":"Demo staging system B"}}`.

Conditions: `["Prostate cancer","Prostate cancer: if biomarker Z is present, stage IV at diagnosis under Demo staging system B; if biomarker Z is absent, recurrent disease"]`.

Expected in order: `(S,N,N;S)`, `(S,C,S;U)`; pair M. Required unassessed qualifier
in the second entry: biomarker Z present/absent selector. Its branch cannot be
chosen, even though the direct recurrence comparison aligns. The first label
does not settle the second label's interpretation or establish clinical eligibility.

## Real-study source selection: reviewed allocation and expectations

The original four snapshots were selected on 2026-10-28 for narrative cases.
Three have identical Conditions text, `Malignant Tumors`. They remain untouched
under `fixtures/inference-evaluation/studies/`; three identical labels do not
provide three distinct checks of the new AI task.

The human approved a fresh two-page lookup, then requested continued pagination
to find coverage. That continuation was bounded to ten further pages. A total
of 12 pages returned 120 distinct records and 54 distinct condition-label arrays
on 2026-10-30 UTC. Fixed cancer search, recruitment/type filters, relevance sort,
page size 10, and response page tokens were preserved. No inference or application
import occurred. This is deliberate evaluation selection, not a representative
sample or a change to live import pagination.

Raw page responses and request provenance are temporarily retained outside Git
for decoded-content verification. Seven selected snapshots are saved
unchanged in decoded content under `fixtures/condition-label-evaluation/studies/`;
the seven-case allocation, exact fixture records, and expectations are approved.
Old snapshots/evidence are not overwritten.
No unbounded search, model call, or favorable-result selection is implied.

Approved seven-case coverage allocation and exact written expectations:

| Case | Record | Complete Conditions array | Distinct coverage |
| --- | --- | --- | --- |
| CL-R01 | NCT07277777 | `["Malignant Tumors"]` | Broad disease scope |
| CL-R02 | NCT04797039 | `["Primary Malignant Neoplasm of Prostate (Diagnosis)"]` | Specific disease wording |
| CL-R03 | NCT06867445 | `["Cervical Cancers","Vaginal Cancers"]` | Multiple named diseases |
| CL-R04 | NCT07301801 | `["Malignant Tumors","SSTR2","PET"]` | Disease plus standalone terms without an explicit disease/qualifier relationship |
| CL-R05 | NCT05855811 | `["Recurrent Cancer","Primary Cancer","Second Cancer"]` | Explicit disease state and other disease-context qualifiers |
| CL-R06 | NCT06043193 | `["Metastatic Neuroendocrine Tumour"]` | Unassessed extent qualifier outside supported patient comparisons |
| CL-R07 | NCT06270602 | `["Oncology"]` | Vague non-diagnostic wording |

Seven cases were recommended and approved because each exercises a different input pattern; the
number is not inherited from the historical selection. More repeated broad
labels would add little. This sample does not supply every controlled AND/OR,
biomarker-selector, or precise staging case; controlled examples retain those
checks. Keyword screening aided discovery but did not determine final meanings
or prove that all biomarker/qualifier patterns had been found.

The [source manifest](fixtures/condition-label-evaluation/provenance.json) records
fixed parameters, source page/time, selection limits, and snapshot hashes.
The hashes use the manifest's stated LF-normalized byte format, matching Git storage
while allowing Windows CRLF working copies; source JSON content is unchanged.
Each file contains the complete selected study object, verified equal in decoded JSON to
its response object. Saving the full object supports provenance; it does not
authorize supplying excluded fields to AI. Retrieval occurred between
2026-10-30T05:42:46Z and 2026-10-30T05:44:28Z (see exact per-record timestamps in
the manifest). Formatting/escaping differs from wire serialization.

### Real-study cases: exact inputs and reviewed expectations

These seven cases have an approved allocation, exact written inputs, and
reviewed expected findings.
Their IDs are separate from the old R1-R4 narrative cases. They use this real-case
base patient, rather than the controlled base's known fictional stage:

```json
{
  "ageYears": 52,
  "sex": "female",
  "cancerType": "Breast cancer",
  "stage": { "status": "unknown", "value": null, "classificationSystem": null },
  "diseaseState": { "status": "unknown", "value": null }
}
```

Changes below replace whole properties of this base. Conditions are the complete
unchanged arrays in the linked snapshots. No titles or narrative fields enter
the AI input. Missing maximum age remains Undetermined under the retained rules,
not an unlimited maximum and not an automatic exclusion.

### CL-R01: broad disease scope

Snapshot: [NCT07277777.json](fixtures/condition-label-evaluation/studies/NCT07277777.json),
fresh source page 2. The historical snapshot of this same record remains separate.

Patient changes: `{}`.

Conditions: `["Malignant Tumors"]`.

Structured: minimum `18 Years`, maximum `80 Years`, sex `ALL`.
Expected age Satisfied; sex No restriction identified.

Expected label: `(S,N,N;S)`; pair S. Cite broad malignant-tumor correspondence
with supplied breast cancer. Neither stage nor disease state is stated in this
label. Do not import primary/recurrent/metastatic requirements from its title or
eligibility narrative, even though those exist in the full retained source object.

### CL-R02: specific disease text

Snapshot: [NCT04797039.json](fixtures/condition-label-evaluation/studies/NCT04797039.json),
source page 3.

Patient changes: `{"sex":"male","cancerType":"Prostate cancer"}`.

Conditions: `["Primary Malignant Neoplasm of Prostate (Diagnosis)"]`.

Structured: minimum `30 Years`, maximum `100 Years`, sex `MALE`.
Expected age Satisfied; sex Satisfied.

Expected label: `(S,N,N;S)`; pair S. Prostate cancer corresponds to the named
prostate malignancy within scope. Required unassessed qualifier: Primary, with
source evidence and an origin-context scope reason, consistent with CL-R05.
Do not infer primary status, convert Primary to newly diagnosed, or turn the
parenthetical Diagnosis into an additional patient requirement.

### CL-R03: multiple named diseases

Snapshot: [NCT06867445.json](fixtures/condition-label-evaluation/studies/NCT06867445.json),
source page 12.

Patient changes: `{"cancerType":"Cervical cancer"}`.

Conditions: `["Cervical Cancers","Vaginal Cancers"]`.

Structured: minimum `18 Years`, maximum absent, sex `FEMALE`.
Expected age Undetermined; sex Satisfied.

Expected in order: `(S,N,N;S)`, `(C,N,N;C)`; pair M, with age uncertainty visible.
The array is not evidence that a patient must have both cancers. Preserve both
findings, without selecting the favorable one as a clinical eligibility decision.

### CL-R04: standalone terms alongside a broad disease

Snapshot: [NCT07301801.json](fixtures/condition-label-evaluation/studies/NCT07301801.json),
source page 4.

Patient changes: `{}`.

Conditions: `["Malignant Tumors","SSTR2","PET"]`.

Structured: minimum `18 Years`, maximum absent, sex `ALL`.
Expected age Undetermined; sex No restriction identified.

Expected in order: `(S,N,N;S)`, `(U,N,N;U)`, `(U,N,N;U)`; pair M.
The first label supports broad cancer correspondence. Neither bare token by
itself establishes a cancer diagnosis or its relationship to the disease label.
Preserve SSTR2 and PET in their own assessment entries, with exact source
references and a scope/context explanation. Do not infer SSTR2 positivity, an imaging requirement,
or an AND relationship using excluded narrative/title fields. Do not require the
patient to have an invented attribute to complete the response.

The reviewed response design keeps `unassessedQualifiers` for unsupported
modifiers within a label. That array is empty for each bare SSTR2/PET label;
the original term is not discarded, and its own findings/explanation record
why a supported comparison cannot be established. Its stage and disease-state
findings are N, not supported alignment. No relationship to another label or
mandatory requirement is invented. This behavior and its written record were
reviewed and approved by the human.

### CL-R05: explicit recurrence and other disease-context terms

Snapshot: [NCT05855811.json](fixtures/condition-label-evaluation/studies/NCT05855811.json),
source page 7.

Patient changes: `{"diseaseState":{"status":"known","value":"newlyDiagnosed"}}`.

Conditions: `["Recurrent Cancer","Primary Cancer","Second Cancer"]`.

Structured: minimum `18 Years`, maximum absent, sex `ALL`.
Expected age Undetermined; sex No restriction identified.

Expected in order: `(S,N,C;C)`, `(S,N,N;S)`, `(S,N,N;S)`; pair M.
The supplied newly diagnosed state conflicts with Recurrent in the first label.
The other two labels retain broad cancer correspondence but preserve Primary and
Second as unassessed context qualifiers. Do not equate newly diagnosed with a
first primary, infer prior cancers, or invent patient origin/history facts.
Do not infer that all three labels are mandatory or mutually exclusive cohorts.

These expectations were reviewed with the human: Primary/Second remain
unassessed qualifiers, not synonyms for the supplied newly diagnosed state.
No new prior-cancer or tumor-origin patient fields are introduced.
Meaning references: [NCI primary cancer](https://www.cancer.gov/publications/dictionaries/cancer-terms/def/primary-cancer)
and [NCI second primary cancer](https://www.cancer.gov/publications/dictionaries/cancer-terms/def/second-primary-cancer).
These support interpretation review, not additional study sources supplied to AI.

### CL-R06: unassessed extent qualifier

Snapshot: [NCT06043193.json](fixtures/condition-label-evaluation/studies/NCT06043193.json),
source page 10.

Patient changes: `{"sex":"male","cancerType":"Neuroendocrine tumour"}`.

Conditions: `["Metastatic Neuroendocrine Tumour"]`.

Structured: minimum `18 Years`, maximum absent, sex `ALL`.
Expected age Undetermined; sex No restriction identified.

Expected label: `(S,N,N;S)`; pair S, with age uncertainty and the unassessed
qualifier visible. Required `unassessedQualifiers` item: Metastatic, with exact
source quotation and the reason that current metastatic status is outside the
supported patient comparisons. The human reviewed this scope treatment; it does
not introduce a current-metastasis patient field or a stage conversion rule.

Cancer type corresponds. No explicit in-scope stage value/classification or
newly diagnosed/recurrent/refractory qualifier is stated, so stage/state are N.
This must not conceal the explicit extent wording: Metastatic remains preserved
in the qualifier array and explanation. Do not infer that it is satisfied from
recurrence or from an earlier stage-at-diagnosis value. Unknown patient stage does
not block alignment when the only extent modifier is outside this assessed scope.

Meaning reference: [NCI metastatic definition](https://www.cancer.gov/publications/dictionaries/cancer-terms/def/metastatic).
It describes spread from the original site, not a universal mapping to our
supplied stage-at-diagnosis value. This reference supports fixture interpretation;
it is not extra study content supplied to the model or clinical validation.

### CL-R07: vague non-diagnostic condition text

Snapshot: [NCT06270602.json](fixtures/condition-label-evaluation/studies/NCT06270602.json),
source page 9.

Patient changes: `{}`.

Conditions: `["Oncology"]`.

Structured: minimum `18 Years`, maximum absent, sex `ALL`.
Expected age Undetermined; sex No restriction identified.

Expected label: `(U,N,N;U)`; pair U. The word alone does not positively establish
the study's disease scope for this patient. Do not treat the oncology search or
a title/description as proof of alignment. The label is present and must be
assessed; this is not the no-condition-text routing case.

## Controlled application checks: separate from model request counts

These are proposed later test boundaries, not implemented tests or permission to
change the runner/application. Use deterministic/stubbed responses, not live AI.

| Group | Expected behavior |
| --- | --- |
| Structured routing | Compute age and sex; either reliable exclusion skips AI. Neither excludes: continue even with one/both Undetermined. |
| Age/sex rules | Inclusive boundaries, six age units, conservative ranges, missing/invalid/inverted bounds, sex ALL, unknown patient sex, and independent reliable exclusion. |
| No condition text | Missing/empty/all-blank text without structured exclusion yields explained pair U and no AI; reliable exclusion takes priority. |
| Source coverage | Preserve original label IDs, repeated label text, and blank-source positions; return exactly one entry for each assessed nonblank label. |
| Invalid response | Invalid JSON, duplicate keys, missing label/finding, duplicate/extra label ID, unexpected fields, wrong type/result: failed `invalidResponse`, no partial assessment. |
| Invalid evidence | Unknown source ID, another label's quotation, excluded narrative source, altered quote, fabricated patient value: failed `invalidEvidence`, no salvage. |
| Authentic but wrong interpretation | Prepared CL-D14 answer quotes the OR label exactly but concludes conflict from stage alone: evidence can pass, interpretation is wrong. Do not claim source checks detect all semantic errors. |
| Pair aggregation | Exhaust the agreed valid label-result combinations, empty-text path, structured-exclusion priority, and retained uncertainty/flags. Do not aggregate direct comparison conflicts as mandatory label conflicts. |
| Unsupported fields | Preserve qualifier flags and exact evidence without inventing patient fields or interpreting a bare token as positive/negative. |
| Pair failures | Preserve age/sex checks and prior work; invalid response/evidence/timeout fails attempt, continues remaining pairs, and is not pair U. |
| Outset unavailability | Stop clearly; preserve prior results and do not fabricate outcomes for unattempted pairs. |
| Cancellation/recovery | Cancel active work once detected; preserve completed assessments; interrupted pair stays unfinished. Explicit retry only for failed attempts; Continue starts unfinished work anew. |
| Context | Full agreed inputs or explicit `inputTooLarge`; no silent source truncation. |

Application test values, HTTP contracts, exact malformed-import handling, and
implementation slices require their own specification/approval. A deliberately
injected failure passes its application check when handled correctly; an unexpected
failed scored model request still fails the model-quality gate.

## Remaining review and next authorization

The written records, exact inputs/expectations, evidence requirements, and
serialized contract examples are approved. Next steps need their own authorization:

1. Separately approve committing and publishing the design records if wanted.
2. Resolve #12 through separately approved tracker updates. #6 remains blocked
   on GitHub until that happens; local document preparation does not close it.
3. Separately propose/review candidate controls and a bounded development pilot.
   Measure latency, agree the limit, then freeze a configuration before acceptance.

No numerical latency limit, model choice, download, pilot execution, publication,
commit, or implementation is authorized by this design approval.

After design approval, the human separately authorized one local documentation
commit containing the 15 approved files. That bounded approval does not authorize
push, PR creation, tracker updates, implementation, or inference. The local Git
log records completion; further commits/publication require their own approval.
