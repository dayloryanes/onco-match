# Focused assessment contract: approved design

Decision work: [Define the focused assessment contract and evidence validation rules (#4)](https://github.com/dayloryanes/onco-match/issues/4).

Canonical published decision: [approved resolution](https://github.com/dayloryanes/onco-match/issues/4#issuecomment-6462538387).

Status: the human approved Q1-Q17, including the revision of Q14 to support all
six age units, and then approved the consolidated representation, validation
details, failure labels/policies, and the cross-criterion OR explanation. The
issue #4 design session is complete, and its separately authorized resolution
has been published and the issue closed. This local repository snapshot remains
uncommitted; it is not an implementation specification or authorization to implement.

The [baseline](poc-1-baseline.md) remains the established scope record. The
[glossary](../../GLOSSARY.md) defines vocabulary. This document records the approved
decision; it does not replace either record. The issue #4 resolution is the
canonical published record; this repository snapshot is not yet on GitHub.

## Agreed decisions

| Question | Agreed decision |
| --- | --- |
| Q1 | One completed pair assessment contains five summaries: age, sex, cancer type, stage, and disease state. Compound requirements retain their relationships. |
| Q2 | Retain the exact supplied patient facts and study content as the assessment input snapshot. |
| Q3 | The application owns deterministic checks, mechanical validation, and final outcome aggregation. The model interprets the three narrative criteria. |
| Q4 | Assessed criteria use Satisfied, Excluded, Undetermined, or No restriction identified. Patient Unknown/Not applicable remain distinct supplied facts. |
| Q5 | Narrative study evidence uses source locations and exact quotations. Structured study evidence and patient evidence use field/value references. Absence claims identify examined sources and explain the conclusion. |
| Q6 | Cancer-type alignment requires positive support from supplied study content. Search relevance or silence alone does not establish alignment. |
| Q7 | Respect clear AND/OR relationships and cohort applicability, including relationships spanning criteria. Material unresolved ambiguity yields Undetermined rather than a favorable guess. |
| Q8 | Any reliable criterion exclusion gives Nonmatch. Otherwise any Undetermined criterion gives Insufficient information. Otherwise supported cancer alignment and satisfied/unrestricted remaining criteria give Potential match. |
| Q9 | A deterministic exclusion skips inference; the narrative summaries say Not assessed and explain why. This is assessment status, not a fifth criterion result. |
| Q10 | The application assigns source identifiers. References use those identifiers plus exact text quotations or supplied values; the model does not calculate character offsets. |
| Q11 | No restriction identified lists the relevant supplied sources examined and explains the absence claim. Missing/unusable relevant information is not evidence of unrestricted eligibility. |
| Q12 | Whenever inference runs, a valid response contains cancer-type, stage, and disease-state summaries. An omitted summary is invalid output, not an implicit Undetermined result. |
| Q13 | Supply brief/official titles, conditions, brief summary, detailed description, full eligibility criteria, and study population when supplied. Keep sources distinct and missing fields explicit; do not silently truncate. |
| Q14, revised | Parse years, months, weeks, days, hours, and minutes. Compare only when the possible patient-age range establishes a result; do not round or invent precision. Unresolved necessary comparisons produce Undetermined. |
| Q15 | Explicit structured sex ALL means no restriction, even when patient sex is Unknown. Known same/different values satisfy/exclude. Missing/unsupported study sex or unknown patient sex under a restriction produces Undetermined. |
| Q16 | Age bounds include the stated completed age in their declared unit. A whole-year range 18-65 includes completed ages 18 and 65. |
| Q17 | An internally inconsistent age range is unusable: age is Undetermined, with the raw bounds and explanation retained. Do not swap limits or choose one to trust. |

## Existing boundaries preserved

- Age and sex use structured study eligibility as their operational authority.
  The model does not repair or override them from narrative text.
- Structured/narrative contradiction detection and reconciliation remain outside
  POC 1. Their possible future evaluation remains in the baseline. This decision
  adds no per-study conflict flag or results-screen warning placement.
- Patient stage remains the supplied stage-at-diagnosis fact from the delivered
  patient contract. Disease state is separate. Do not infer current disease extent,
  staging editions, TNM components, or other unsupplied patient facts.
- Biomarkers, labs, treatment history, consent, willingness, and final clinical
  eligibility remain outside the assessed criteria. If an unsupplied cohort
  selector prevents identifying an applicable in-scope rule, preserve uncertainty;
  another independent reliable exclusion can still establish Nonmatch.
- No numeric score, raw model reasoning display, automatic retries, or rematching
  of completed assessments. Completed Insufficient information is also final.
- Retention means shared process-memory lifetime, not database persistence.

## Approved contract representation

These approved field names and validation details make the agreed behavior
concrete. They have not been implemented as public API endpoints.

### Application-owned evaluation record

| Field | Meaning |
| --- | --- |
| `patientId`, `studyId` | The application binds the evaluation to the actual pair. The model does not choose or change these values. |
| `inputSnapshotId` | Reference to the retained inputs for this evaluation. Shared study content may be retained once and referenced without copying it per pair. |
| `state` | `pending`, `running`, `completed`, `failed`, or `interrupted`. |
| `assessment` | A completed `PairAssessment`, or null. Only `completed` has one. |
| `failure` | A code and concise diagnostic message for a failed attempt; otherwise null. |

An interrupted attempt leaves its pair unfinished; Continue starts a new attempt.
No partial model response becomes a completed assessment. Failed attempts are
recorded separately from any subsequent explicit recovery attempt. This does not
add an attempt-history screen or a new recovery operation.

### Completed pair assessment

`outcome` is one of `potentialMatch`, `nonmatch`, or `insufficientInformation`.
`criteria` contains exactly the five named summaries. Each summary contains:

| Field | Meaning |
| --- | --- |
| `assessmentStatus` | `assessed` or `notAssessed`. |
| `result` | `satisfied`, `excluded`, `undetermined`, or `noRestrictionIdentified`; null when not assessed. |
| `requirementSummary` | Concise account of the applicable rule, including compound relationships and unresolved applicability; null when not assessed. |
| `patientEvidence` | Field/value references to the supplied patient facts used. |
| `studyEvidence` | Exact quotations for narrative claims, or field/value references for structured study claims. |
| `examinedSources` | Source identifiers identifying the study content considered; required for No restriction identified. |
| `explanation` | Concise explanation of the conclusion or why assessment was skipped. This is not a chain-of-thought transcript. |

Compound interpretation must also work across criterion summaries. For a clearly
applicable rule "stage IV OR recurrent disease," known recurrence satisfies the
combined rule even when the supplied stage is II. The affected summaries describe
the complete combined rule and cite its supporting facts; neither labels the
failed stage alternative Excluded by itself. A Satisfied stage summary in this
case means the applicable combined requirement is satisfied, not that stage II
has been reclassified as stage IV. If there are additional mandatory stage rules,
they still need assessment. This simple representation retains compound meaning
in the summaries and evidence; it does not introduce a universal rule parser or
claim that mechanical validation proves the model's logic correct.

For a skipped criterion, evidence/source lists are empty, `result` and
`requirementSummary` are null, and `explanation` supplies the reason. An assessed
criterion always has a result and explanation, including when facts are missing.

Cancer type uses Satisfied, Excluded, or Undetermined: lack of positive disease
alignment cannot be converted into No restriction identified. Explicit broad
inclusion can support Satisfied when it covers the supplied patient diagnosis.

The model response contains exactly `cancerType`, `stage`, and `diseaseState`
summaries. It does not supply final outcome, pair identity, evaluation state, or
age/sex conclusions. The application adds its age/sex summaries and aggregates
only after validating the complete narrative response.

### Sources and references

The application supplies a source catalog tied to the retained snapshot. Each
source has an application-assigned identifier, its original field location,
presence information, and its original decoded JSON value or text. A present
patient field containing null is distinguishable from a missing study field.
Explicit patient status/value fields retain their existing semantics.

Aliases such as `study.minimumAge` map to the original API path, for example
`protocolSection.eligibilityModule.minimumAge`. List elements have distinct
identifiers, such as `study.conditions.0`. Missing declared sources can be named
when explaining unavailable information, but cannot supply invented quotations
or factual values.

Reference shapes:

```json
{
  "kind": "text",
  "sourceId": "study.eligibilityCriteria",
  "quote": "Newly diagnosed or recurrent disease."
}
```

```json
{
  "kind": "value",
  "sourceId": "patient.diseaseState.value",
  "value": "recurrent"
}
```

A No restriction identified conclusion may have no study quotation when no
restriction was stated, but must name the examined sources and explain the basis.
If an explicit unrestricted statement exists, quote/reference it as well.
The application can check declared coverage, not whether the model actually
interpreted every relevant passage correctly. A title alone cannot establish the
absence of stage or disease-state restrictions when relevant eligibility content
is missing. Not every missing optional field necessarily prevents a conclusion.

The model receives the Q13 narrative sources and the supplied patient facts needed
for the three narrative criteria, preserving stage/classification and disease-state
status. The application retains the structured age/sex sources used for its own
checks. Their inclusion in a snapshot does not delegate these checks to the model.
No retrieval, refresh, or extra patient attributes are implied.

## Approved mechanical validation

1. Parse the model assessment content as the expected JSON object. Reject malformed
   output, missing/duplicate criterion entries, unexpected fields, invalid types,
   invalid result/status values, and missing required explanations or evidence
   declarations. Provider response-envelope handling is a later integration detail.
2. Require all three narrative summaries and assessed status. The model cannot
   skip a narrative criterion after finding an exclusion elsewhere.
3. Check every referenced identifier against the application-supplied source
   catalog for this exact snapshot. Do not permit references to another pair or
   content not supplied for this evaluation.
4. For text evidence, require a nonempty exact substring of the referenced present
   text after normal JSON string decoding. Do not accept an altered quotation by
   silently changing punctuation, case, or words. Character offsets are not required.
5. For value evidence, require the referenced present field and an equal JSON
   value/type, including an explicitly supplied null where appropriate. Do not
   coerce a fabricated value into a match.
6. Require study evidence for positive claims about requirements, relevant patient
   evidence for patient-dependent conclusions, and examined-source declarations
   for absence claims. Undetermined explanations may instead identify genuinely
   missing data. Reject mechanically inconsistent shapes; do not claim mechanical
   validation can judge every semantic interpretation.
7. Reject the whole model evaluation if any required evidence or summary is
   mechanically invalid. Do not salvage one quoted exclusion into a completed
   Nonmatch while ignoring an invalid portion of that response.
8. On a valid response, combine it with the application's valid deterministic
   summaries. Calculate the overall outcome; do not trust a model-suggested overall
   label or confidence score. Authentic-but-misinterpreted evidence remains a risk.

Input information that is genuinely missing or contradictory is not itself a
malformed model response. A complete, evidence-valid explanation of that problem
can yield Undetermined and a completed Insufficient information assessment.

## Age and sex interpretation

- A patient age Y denotes completed years: the patient has reached birthday Y,
  but not birthday Y+1. No birth date or exact age is invented.
- Whole-year bounds are inclusive comparisons of completed years. For another
  unit, compare the full range of possible completed ages in that declared unit.
  Minimum passes only if every possible age reaches it, and excludes only if
  every possible age is below it. Maximum passes only if every possible age is
  within it, and excludes only if every possible age exceeds it. Otherwise the
  relevant comparison is unresolved.
- Calendar-dependent conversions must use safe bounds, not a fixed 30-day month
  or a universally 365-day year. Exact arithmetic implementation and deterministic
  test cases must demonstrate those guarantees during later specification work.
- All necessary bound checks must be established for Satisfied. One independently
  reliable bound failure can establish Excluded despite an unresolved other bound.
- A demonstrably inverted range is invalid as a whole and yields Undetermined;
  its bounds are not independently reliable exclusion evidence.
- Missing/unrecognized bound representations are unresolved. Registration's
  N/A option has not been verified as a particular API omission encoding, so this
  design does not equate omitted minimum/maximum fields with unbounded eligibility.
- Sex ALL yields No restriction identified, including for Unknown patient sex.
  Matching known restricted values satisfy; different known values exclude.
  Missing/unsupported study sex or Unknown patient sex under a restriction yields
  Undetermined. Do not infer sex from diagnosis or narrative wording.
- Compute both deterministic summaries before inference. If either reliably
  excludes, retain both summaries and mark all three narrative criteria Not
  assessed. Otherwise run the narrative assessment even if age/sex is Undetermined;
  it may establish an independent exclusion.

## Approved execution-failure separation

The overall outcome exists only for a completed assessment. Approved failure
labels make the distinction inspectable without adding another assessment outcome:

| Code/state | Meaning and behavior |
| --- | --- |
| `inferenceUnavailable` | The service is unavailable at operation outset: stop clearly and retain prior completed work. Do not label unattempted pairs Nonmatch. |
| `inferenceTimeout` | A pair times out: fail that attempt and continue remaining work. |
| `invalidResponse` | Malformed/incomplete response or invalid contract shape: fail that attempt and continue remaining work. |
| `invalidEvidence` | Mechanically invalid evidence: fail that attempt and continue remaining work. |
| `inputTooLarge` | The integration cannot submit the agreed full inputs within its supported context: fail the attempt without silent truncation and continue remaining pairs. Feasibility/limits require later evaluation. |
| `inferenceError` | Other reported inference-request failure after work starts: fail the attempt and continue remaining pairs. Exact adapter mappings remain part of integration design. |
| `internalError` | An unexpected application fault: stop the operation clearly, retain completed work, and leave unattempted pairs unfinished. Do not fabricate clinical outcomes. |
| `interrupted` state | Cancellation/disconnection stops further work and requests cancellation of active inference. The active pair stays unfinished, not completed or automatically retried. |

No timeout duration, HTTP streaming contract, preflight probing strategy,
automatic retry, or independent background execution is selected by this design.

## Worked cases

All cases are hypothetical design examples, not new patient fixtures or measured
model behavior. Abbreviations: Satisfied (S), Excluded (E), Undetermined (U),
No restriction identified (N), Not assessed (-).

| Case | Age | Sex | Cancer type | Stage | Disease state | Result |
| --- | --- | --- | --- | --- | --- | --- |
| All requirements supported; no stage restriction identified | S | N | S | N | S | Potential match |
| Supplied patient age is Unknown; other criteria supported | U | S | S | N | S | Insufficient information |
| Minimum 70 years, maximum 18 years; no other exclusion | U | N | S | N | S | Insufficient information, explaining invalid age range |
| Same invalid age range, but sex reliably excludes | U | E | - | - | - | Nonmatch; inference skipped |
| Patient is 12; valid minimum is 18 years | E | N | - | - | - | Nonmatch; inference skipped |
| Patient is recurrent; study accepts newly diagnosed OR recurrent | S | N | S | N | S | Potential match if all other requirements are supported |
| Stage II and recurrent; the only stage/disease-state rule is stage IV OR recurrent | S | N | S | S | S | Potential match; both affected summaries explain satisfaction through recurrence, not an invented stage IV |
| Applicable cohort cannot be established from supplied facts | S | N | S | U | S | Insufficient information |
| Model cites a quotation absent from the retained source | S | N | No accepted narrative summaries | No accepted narrative summaries | No accepted narrative summaries | Failed attempt: invalidEvidence; no completed outcome |
| Operation is cancelled during model generation | S | N | No completed narrative assessment | No completed narrative assessment | No completed narrative assessment | Interrupted/unfinished; Continue starts anew |

One approved serialized criterion shape, illustrating compound interpretation:

```json
{
  "assessmentStatus": "assessed",
  "result": "satisfied",
  "requirementSummary": "Newly diagnosed or recurrent disease is accepted.",
  "patientEvidence": [
    {
      "kind": "value",
      "sourceId": "patient.diseaseState.value",
      "value": "recurrent"
    }
  ],
  "studyEvidence": [
    {
      "kind": "text",
      "sourceId": "study.eligibilityCriteria",
      "quote": "Newly diagnosed or recurrent disease."
    }
  ],
  "examinedSources": ["study.eligibilityCriteria"],
  "explanation": "The supplied recurrent disease state satisfies one accepted alternative."
}
```

The result above is a disease-state result only, not an overall Potential match.
The referenced hypothetical input contains that exact quoted sentence and the
explicitly known recurrent patient value; the example does not alter any fixture.

## Verification and remaining approval

Prepared from the live Q1-Q17 decisions, the baseline, current patient contract,
and the current overview/decision issue. Read-only factual lookups checked source
fields and age/sex definitions against the official references below. No live
study sampling or model experiments were performed. No application code changed.

Document validation: all three JSON examples parsed successfully, both local
documents passed trailing-whitespace checks, and git diff --check passed for
tracked changes (there are no tracked edits). The only working-tree additions
are this approved design document and GLOSSARY.md. No application tests or independent automated
code-review run were performed for these local design documents.

The human approved the representation, mechanical validation details, and failure
labels/policies as one consolidated design after clarification of cross-criterion
alternatives. No design question remains open within this session. Precise calendar
arithmetic/tests and later integration details remain work for their separately
authorized specification/evaluation stages, as described above.

The human separately approved publishing the prepared resolution, closing #4,
and updating the existing map (#2) and overview (#1). Those four actions are
complete. Read-back verification confirmed the exact published comment/body
contents against the reviewed local files, #4 CLOSED, and #1/#2 still OPEN.

At the original #4 checkpoint, committing/publishing these repository files,
starting issue #5, new specifications, experiments, and implementation required
their separate approvals. The overview then recorded #5 as awaiting approval;
no next stage had started. Both local files were uncommitted on main, with no
application changes or commits introduced by that session.

Subsequently, the human approved the #5 evaluation-design session, its bounded
registry retrieval, local documents/snapshots, and separate tracker publication.
That decision is now closed; see the
[approved #5 resolution](https://github.com/dayloryanes/onco-match/issues/5#issuecomment-6463753448)
and [local evaluation design](inference-evaluation-design.md). The #4 design is
unchanged. Repository commits/publication and #6 experiments still require
separate approval; use the current overview for authorization.

## Official factual references

- [ClinicalTrials.gov API response schema](https://clinicaltrials.gov/api/oas/v2):
  source field locations, age-unit string pattern, and optional response fields.
- [ClinicalTrials.gov registration definitions](https://clinicaltrials.gov/policy/protocol-definitions):
  age unit choices including the registration N/A option, and explicit sex All.
- [NCI cancer staging](https://www.cancer.gov/about-cancer/diagnosis-staging/staging):
  background for the existing stage-at-diagnosis patient contract, not a new
  restaging feature or additional patient attribute.
