# Condition-label assessment design

Date: 2026-10-30. Decision: [Revise POC 1 assessment scope to condition-label alignment (#12)](https://github.com/dayloryanes/onco-match/issues/12).

Status: the human approved this written design package on 2026-10-30, including
the exact fixture records and serialized examples, after the focused discussion
and local preparation on `docs/condition-label-design`. This approved local
record is not an implementation ticket or a published GitHub resolution.
No application implementation, model run/download, or publication is authorized.
After design approval, the human separately authorized one local documentation
commit; Git history records its completion. GitHub #1/#12 have not been updated
with this conversation, and publication remains separately authorized.

Read the [baseline](poc-1-baseline.md), [glossary](../../GLOSSARY.md), and
[condition-label evaluation design](condition-label-evaluation-design.md). The preceding
[narrative contract](assessment-contract-design.md) is historical. Its structured
age/sex and unaffected execution/lifecycle rules are retained as described below;
its narrative inputs, three narrative results, and aggregation are not reused.

## Scope and responsibilities

| Owner | Responsibility |
| --- | --- |
| Application | Retain the exact patient/study input snapshot, check structured age/sex, assign source IDs, validate the model response, calculate the pair summary, and present limits. |
| AI | Interpret every complete supplied condition label against supported patient cancer type, stage/classification, and disease state. Return separate direct findings and a complete-label result with evidence. |
| Human evaluation | Review expected answers before output, and judge interpretation correctness, uncertainty, explanation quality, consistency, and practical latency. |

One AI request contains one patient and all complete condition labels for one
study. No per-label network calls or assumptions that separate labels form an
eligibility AND/OR rule. Repeated text at different source positions keeps its
source identity; it must not silently disappear from response coverage.

The model receives cancer type, stage status/value/classification, disease-state
status/value, and the condition-label source catalog. It does not choose pair IDs,
compute age/sex, or return the overall pair summary. The application retains the
structured age/sex inputs but does not delegate those checks to the model.

Exclude titles, eligibility criteria, summaries, detailed descriptions,
study-population narrative, and gender descriptions from AI input. Do not use them
to resolve an unclear label. Trial phase is not patient stage; locations remain
descriptive. No new patient biomarker, treatment, laboratory, consent, or willingness
fields are introduced. No source-text truncation or response salvage is approved.

## Structured routing

Compute both age and sex checks under the previously reviewed structured rules:
conservative whole-year age uncertainty, six bound units, inclusive completed-age
bounds, no invented age precision, invalid/inverted bounds remaining unresolved,
and structured sex ALL explicitly unrestricted. Missing or unsupported values
are not automatic exclusions or passes. Calendar arithmetic and exact imported
data contracts still need implementation-slice specification and tests.

1. If either check reliably excludes, complete the pair as Excluded by age/sex.
   Retain both structured findings; label assessment is Not assessed, with its
   reason, and no AI request is made.
2. Otherwise continue to label assessment, including when one or both structured
   checks are Undetermined. AI does not repair that uncertainty.
3. If condition text is missing, an empty list, or entirely blank, skip AI and
   complete as Condition-label alignment undetermined with "No condition text
   supplied." A reliable structured exclusion still takes priority.

A nonblank unrelated, broad, unfamiliar, or unsupported-qualified label is still
text to assess. Blank entries are retained in the input snapshot and identified
as having no condition text; only nonblank text entries require model findings.
Original source positions are preserved. A malformed non-text registry field is
not silently converted into missing text; exact import-error handling belongs to
the later import contract, not a newly invented assessment result.

## Direct findings and complete-label results

Direct cancer-type findings are Alignment supported, Conflict identified, or
Undetermined. Cancer silence does not establish alignment. Stage and disease
state also allow Not specified in condition labels when no explicit in-scope
stage value/classification or supported disease-state qualifier is stated.
This does not establish absence of other extent information or restrictions in
the label/full study; preserve unsupported modifiers separately.

Unknown patient information and Not applicable remain distinct supplied facts.
An absent qualifier does not require a known patient value. An explicit qualifier
requiring a comparison that cannot be made yields Undetermined. Do not equate
staging systems/editions, infer current extent from stage at diagnosis, or infer
disease state from unrelated facts. A patient Not applicable status does not
automatically satisfy or contradict an explicit stage qualifier.

Preserve unsupported explicit qualifiers (for example biomarkers and previous
treatment) in `unassessedQualifiers`, with authentic evidence and a reason. They
do not automatically erase otherwise supported in-scope alignment. If one selects
which in-scope rule applies and that selection is unknown, the complete label is
Undetermined unless another independent, applicable conflict establishes a result.

Unsupported standalone labels retain their own assessment entries rather than
being put in this qualifier array. For `SSTR2` alone, cancer type is Undetermined,
stage and disease state are Not specified, and the complete label is Undetermined.
Keep `unassessedQualifiers` empty: no explicit modifier within that label has been
identified. Preserve the original label through its source ID, evidence, and
explanation; do not infer positivity, a mandatory patient requirement, or a
relationship to another label. Every supplied nonblank label still has an entry;
none is discarded or silently marked aligned. Not evaluating biomarker status is
distinct from omitting the label assessment that explains this limit.

The AI assigns one complete-label result:

| Result | Meaning |
| --- | --- |
| `alignmentSupported` | Cancer correspondence is supported and the complete label's applicable supported conditions are satisfied; absent and unassessed qualifiers are disclosed separately. |
| `conflictIdentified` | At least one reliable applicable condition conflicts, after respecting alternatives and applicability. Other uncertainties remain visible. |
| `undetermined` | No reliable applicable conflict establishes a result, and missing facts, unsupported applicability, or ambiguity prevents supported alignment. |

Direct findings need not have the same result as the complete label:

| Patient / label | Cancer | Stage | State | Complete label |
| --- | --- | --- | --- | --- |
| Breast cancer, II, recurrent / stage III OR recurrent breast cancer | Aligns | Conflicts, assuming comparable staging | Aligns | Alignment supported through recurrence |
| Same patient / stage III AND recurrent breast cancer | Aligns | Conflicts | Aligns | Conflict identified: mandatory stage condition |
| Breast cancer, unknown stage / stage III breast cancer | Aligns | Undetermined | Not specified | Undetermined |
| Breast cancer, unknown stage / stage III lung cancer | Conflicts | Undetermined if comparison lacks necessary context | Not specified | Conflict identified: independent cancer-type conflict |

These are interpretation examples, not new clinical staging rules. Do not
mechanically aggregate direct findings with an "any conflict wins" rule: AND/OR
and applicability are interpreted at the complete-label level. An authentic
quotation and internally plausible result still may be semantically wrong.

Current metastatic status is outside the supported POC 1 comparisons. Preserve
Metastatic in `unassessedQualifiers`; do not invent a current-metastasis patient
field, infer it from recurrence, or automatically convert it to a numbered stage.
For Metastatic Neuroendocrine Tumour against supplied Neuroendocrine tumour,
cancer type aligns, stage/state are Not specified within their supported scope,
and the complete label supports alignment within scope, with Metastatic visibly
unassessed. This is not a missing fact for an otherwise supported stage check.
An explicit stage value whose necessary patient facts/classification are missing
still yields an Undetermined stage finding; those are different situations.

## Application-owned pair summary

Code calculates the summary from structured checks and valid complete-label
results. Display and camel-case serialized names below are approved design;
they are not implemented public API identifiers.

| Value | Display | Rule |
| --- | --- | --- |
| `excludedByAgeSex` | Excluded by age/sex | A reliable age/sex exclusion, before inference. |
| `conditionLabelAlignmentSupported` | Condition-label alignment supported | At least one label and all complete-label results are `alignmentSupported`. |
| `mixedConditionLabelFindings` | Mixed condition-label findings | At least one aligns and another conflicts or is undetermined. |
| `conditionLabelConflictsIdentified` | Condition-label conflicts identified | At least one label and every complete-label result is `conflictIdentified`. |
| `conditionLabelAlignmentUndetermined` | Condition-label alignment undetermined | No alignment, at least one undetermined; or no condition text without structured exclusion. |

Keep all label findings, age/sex uncertainty, and unassessed qualifiers visible.
Do not select the favorable label as full-study eligibility, or splice cancer
support from one label with stage support from another. A completed mixed or
undetermined assessment is not a failed execution or eligible for retry.

## Model response shape

The top-level object has exactly one field, `labels`, an array. Each label entry
has exactly these required fields:

| Field | Shape |
| --- | --- |
| `labelId` | Nonempty application-supplied condition source identifier. |
| `result` | One of the three complete-label values above. |
| `requirementSummary` | Nonempty concise interpretation, retaining alternatives and unresolved applicability. |
| `cancerType`, `stage`, `diseaseState` | Each a finding object defined below. |
| `unassessedQualifiers` | Array of explicit unsupported qualifiers within this label, empty when none; not a collection of unsupported whole labels. |
| `explanation` | Nonempty concise account of the complete-label result, not raw reasoning. |

Each finding object has exactly `result`, `patientEvidence`, `studyEvidence`,
`examinedSources`, and `explanation`. The finding result values are
`alignmentSupported`, `conflictIdentified`, `undetermined`, and (only for stage
or disease state) `notSpecified`. All evidence/source lists are required, even
when empty. `examinedSources` identifies the complete label considered and is
mandatory for `notSpecified`; it is a coverage declaration, not proof the model
correctly read it. Not specified normally has empty patient/study evidence lists.

Patient references are objects with exactly `sourceId` and `value`, matching the
supplied decoded JSON value/type. They may cite a supplied null or an unknown/not
applicable status, but not invent an absent fact. Study references have exactly
`sourceId` and `quote`, a nonempty exact substring of that label's decoded text.
An unassessed-qualifier item has exactly `qualifier`, `studyEvidence`, and `reason`;
all are nonempty, and its evidence comes from that complete label. The qualifier
description preserves the unsupported meaning and its authentic evidence;
the reason explains why that modifier is outside scope without inventing a
patient fact. Unsupported whole labels are explained in their own assessment
findings, not duplicated here. In-scope comparisons prevented by missing patient
facts remain Undetermined in their own finding, not merely put in this array.

These nested names/shapes are part of the approved serialization design.
They do not add a fourth label result, numerical score, provider abstraction, or
generic clinical-rule language.

## Source catalog and validation

Application-assigned identifiers map to exact original snapshot fields, such as
`patient.cancerType`, `patient.stage.status`, `patient.stage.value`,
`patient.stage.classificationSystem`, `patient.diseaseState.status`,
`patient.diseaseState.value`, and `study.conditions.0`. Study condition source IDs
map to `protocolSection.conditionsModule.conditions` array positions. Preserve
original decoded text and distinguish null patient values from absent study data.

1. Parse the response as the expected JSON object. Reject duplicate JSON keys,
   unexpected fields, missing required fields, wrong types, invalid result values,
   empty required strings, duplicate label IDs, and omitted/extra label entries.
   Provider-envelope handling is an integration concern, not this model shape.
2. Require every assessed label exactly once and all three direct findings.
   Array order may differ; source IDs bind entries to the supplied labels.
3. Check every source ID against the exact supplied snapshot. Finding study
   references and examined-source declarations must identify that entry's label,
   not another label or an excluded narrative source.
4. Compare quotations as exact decoded substrings, without case/punctuation
   repair. Compare patient references as exact JSON values/types without coercion.
5. Require label evidence for positive condition/qualifier claims and relevant
   patient evidence for comparisons. Missing facts are explained and reference
   their supplied unknown/null/status fields. Require coverage declarations for
   absent qualifiers. Exact semantic relevance still needs evaluation.
6. Validate mechanically checkable relationships (allowed finding states and
   required evidence shapes); do not pretend to prove AND/OR or cohort reasoning
   from source authenticity. A direct conflict plus a complete-label alignment
   is not automatically invalid: the OR example is intentionally valid.
7. Reject the entire model evaluation on any invalid required portion. Do not
   salvage good-looking labels, repair the answer, or fabricate an undetermined
   completed assessment from a broken response.

The model owns interpreted complete-label results; code owns pair aggregation.
Semantic contradictions can pass mechanical checks and fail the quality gate.

## Serialized example: a satisfied OR alternative

Illustrative fictional inputs, distinct from the ten application patients:

```json
{
  "patient.cancerType": "Breast cancer",
  "patient.stage.status": "known",
  "patient.stage.value": "II",
  "patient.stage.classificationSystem": "Demo staging system A",
  "patient.diseaseState.status": "known",
  "patient.diseaseState.value": "recurrent",
  "study.conditions.0": "Breast cancer, stage III at diagnosis under Demo staging system A OR recurrent"
}
```

```json
{
  "labels": [
    {
      "labelId": "study.conditions.0",
      "result": "alignmentSupported",
      "requirementSummary": "Breast cancer with either stage III at diagnosis under Demo staging system A or recurrent disease.",
      "cancerType": {
        "result": "alignmentSupported",
        "patientEvidence": [{ "sourceId": "patient.cancerType", "value": "Breast cancer" }],
        "studyEvidence": [{ "sourceId": "study.conditions.0", "quote": "Breast cancer" }],
        "examinedSources": ["study.conditions.0"],
        "explanation": "The supplied cancer type corresponds to the label."
      },
      "stage": {
        "result": "conflictIdentified",
        "patientEvidence": [
          { "sourceId": "patient.stage.status", "value": "known" },
          { "sourceId": "patient.stage.value", "value": "II" },
          { "sourceId": "patient.stage.classificationSystem", "value": "Demo staging system A" }
        ],
        "studyEvidence": [{ "sourceId": "study.conditions.0", "quote": "stage III at diagnosis under Demo staging system A" }],
        "examinedSources": ["study.conditions.0"],
        "explanation": "Stage II differs from III in the same supplied system and context; this is an OR alternative, not independently mandatory."
      },
      "diseaseState": {
        "result": "alignmentSupported",
        "patientEvidence": [
          { "sourceId": "patient.diseaseState.status", "value": "known" },
          { "sourceId": "patient.diseaseState.value", "value": "recurrent" }
        ],
        "studyEvidence": [{ "sourceId": "study.conditions.0", "quote": "recurrent" }],
        "examinedSources": ["study.conditions.0"],
        "explanation": "The patient satisfies the recurrence alternative."
      },
      "unassessedQualifiers": [],
      "explanation": "Cancer type aligns and recurrence satisfies the explicit OR; no stage III patient fact is invented."
    }
  ]
}
```

The code-owned pair summary is Condition-label alignment supported if the
structured checks do not exclude. Unknown age/sex findings, if any, remain visible.

## Serialized example: an unsupported qualifier

Inputs:

```json
{
  "patient.cancerType": "Breast cancer",
  "patient.stage.status": "unknown",
  "patient.stage.value": null,
  "patient.stage.classificationSystem": null,
  "patient.diseaseState.status": "unknown",
  "patient.diseaseState.value": null,
  "study.conditions.0": "HER2-positive breast cancer"
}
```

```json
{
  "labels": [
    {
      "labelId": "study.conditions.0",
      "result": "alignmentSupported",
      "requirementSummary": "Breast cancer with an explicit HER2-positive qualifier that is outside the assessed scope.",
      "cancerType": {
        "result": "alignmentSupported",
        "patientEvidence": [{ "sourceId": "patient.cancerType", "value": "Breast cancer" }],
        "studyEvidence": [{ "sourceId": "study.conditions.0", "quote": "breast cancer" }],
        "examinedSources": ["study.conditions.0"],
        "explanation": "The supplied cancer type aligns; no biomarker fact is inferred."
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
      "unassessedQualifiers": [
        {
          "qualifier": "HER2-positive",
          "studyEvidence": [{ "sourceId": "study.conditions.0", "quote": "HER2-positive" }],
          "reason": "Biomarkers are outside the assessed patient facts and scope."
        }
      ],
      "explanation": "Cancer-type alignment is supported within scope; the HER2-positive qualifier remains unassessed, not satisfied."
    }
  ]
}
```

## Failure, recovery, and retained lifecycle

Invalid shape is `invalidResponse`; mechanically invalid evidence is
`invalidEvidence`. Either fails the whole attempt, preserves completed age/sex
checks and prior work, and continues remaining pairs. A timeout is
`inferenceTimeout`, not an undetermined label. No automatic retries or repairs.
Operators may explicitly retry failed attempts. Missing source information with a
valid explained conclusion is different from a broken response.

Retain existing `inferenceUnavailable` outset-stop behavior, `inferenceError`
pair failures, `inputTooLarge` without silent truncation, and `internalError`
operation-stop behavior from the earlier contract. Cancellation leaves active
work unfinished and preserves completed assessments. Continue starts unfinished
work anew; it does not resume generated text. Pair identity, input snapshots,
execution states, and failure details remain application-owned.

Only one matching operation runs across the application; requests are sequential
and attached to the initiating dashboard request. Logout/navigation/disconnection
cancel the initiating work once detected. Shared in-memory state, immutable
completed assessments, no background jobs, and no rematching remain unchanged.
Exact streaming/API contracts and timeout/proxy implementation belong to later
approved slices. The experimental runner is not an implemented application feature.

## Working notice and UI boundary

> This assessment checks structured age and sex requirements and compares cancer type, stage, and disease state with the study's listed conditions. Biomarkers, treatment history, other requirements, and free-text eligibility criteria are not assessed. Results do not establish clinical eligibility or enrollment approval.

The wording is a working agreement, revisitable during UI design. Display the
scope in results context without requiring a popup, acknowledgement, or extra
click. Exact placement in Dashboard, Patient, and Study views, and any compact
list treatment, are deferred. Those views inspect the same stored pair results;
the existing patient directory has no assessment results yet. No UI is implemented
or redesigned by this document. Exact registry-field mappings belong in developer
documentation, not this notice.

## Review and next boundaries

The nested serialized fields, two JSON examples, and exact new fixtures are
approved as written design records. Numerical latency acceptance, candidate settings,
route selection, request budgets, and execution approvals remain open under
[#6](https://github.com/dayloryanes/onco-match/issues/6), which is still blocked by
open #12 on GitHub. No issue closure or publication is implied by local drafting.
No implementation specification/tickets are created here.
