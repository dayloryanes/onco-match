# Inference evaluation: approved design

> Historical narrative-scope design. Preserve these original fixtures,
> expectations, and snapshots as evidence of the earlier task; do not relabel
> their failures as successes on the narrower task. The new plan is
> [Condition-label evaluation design](condition-label-evaluation-design.md),
> approved locally under [scope decision #12](https://github.com/dayloryanes/onco-match/issues/12), awaiting publication.
> Historical authorization statements below are not current permission to run
> experiments; resume through [overview #1](https://github.com/dayloryanes/onco-match/issues/1).

Decision work: [Agree the inference evaluation fixtures and acceptance criteria (#5)](https://github.com/dayloryanes/onco-match/issues/5).

Status: approved through live human discussion on 2026-10-28, using grilling
and domain-modeling. The human approved the consolidated framework, exact
controlled acceptance/development examples, four real-study candidates and
their expected results, then separately approved these local documentation,
glossary, and snapshot writes. This is a local design record, not an
implementation specification or authorization to execute experiments.

Read the [baseline](poc-1-baseline.md) for established POC scope,
[assessment contract](assessment-contract-design.md) and its
[canonical #4 resolution](https://github.com/dayloryanes/onco-match/issues/4#issuecomment-6462538387)
for assessment behavior, and [GLOSSARY.md](../../GLOSSARY.md) for vocabulary.
This design preserves those decisions.

## Authorization checkpoint

Authorized and completed: bounded design discussion, read-only retrieval of up
to two registry pages, review of expected results, and local creation of this
record, four study snapshots, and approved glossary additions.

The ten delivered application patients remain unchanged. Evaluation fixtures
are separate fictional inputs under the same patient contract; they do not
augment live imports or application data.

The human subsequently approved publishing the prepared resolution, closing #5,
and updating #1/#2. Those tracker actions are complete; read-back verification
confirmed the reviewed contents, #5 CLOSED, and #1/#2 OPEN. The published
[approved #5 resolution](https://github.com/dayloryanes/onco-match/issues/5#issuecomment-6463753448)
records the full evaluation design. The human also approved correcting A13/D13
locally and in that resolution to follow the existing Unknown-stage contract.

Committing or publishing repository files, creating new specifications/tickets,
changing branches, implementation, model downloads, and experiments remain outside
the current authorization. The next experimental work belongs to
[#6](https://github.com/dayloryanes/onco-match/issues/6) after separate approval.
Tracker publication and local corrections do not grant that approval.

## Evaluation structure and completion criteria

- Development: 14 controlled cases D1-D14, available for prompt/integration refinement.
- Acceptance: 14 controlled cases A1-A14 plus real-study cases R1-R4.
- Each acceptance case runs five times per endpoint under a frozen candidate:
  18 cases x 5 repetitions x 2 endpoints = **180 scored requests per candidate**.
- Development runs and controlled application checks are additional; their
  execution budget requires approval before experiments.
- Review expected criterion results, overall outcome, rationale, and acceptable
  evidence before seeing candidate output. Human review establishes agreement
  with the focused contract, not clinical validation.
- Unresolved expectations remain exploratory and unscored until resolved.
- Development counterparts deliberately mirror acceptance logic while changing
  facts and wording. This is a narrow controlled evaluation, not a claim of
  broad generalization. Real-study wording supplies a separate realism check.
- Acceptance inputs must not become prompt examples or guide refinement while
  retaining acceptance status. A case used to guide a change becomes a
  development case and requires a reviewed acceptance replacement.
- The current reviewed expected results are recorded below. Every narrative
  model response still requires all three summaries, including after an exclusion.

The plan is complete as a reviewed design. No candidate has been executed or
accepted. Operational configuration values, execution budget, experiment
procedure details, and measured latency acceptance remain later approved work.

## Candidate configuration and API comparison

Start with Qwen3 8B through local Ollama, sequential requests. Compare native
chat (`/api/chat`) and OpenAI-compatible chat (`/v1/chat/completions`) using the
same fixtures and equivalent settings. Both routes address the same local
server/model; API compatibility does not imply OpenAI cloud inference.

Record exact model/version or digest, quantization, Ollama version, prompt,
response schema, generation settings, context limit, and timeout before scored
runs. Record any setting that cannot be matched across endpoints. A material
change creates a new candidate requiring fresh acceptance results.

Compare semantic correctness, valid evidence, execution failures, consistency,
and latency. Identical prose or identical quotation choice is unnecessary when
alternatives are equally valid.

The intended POC 1 implementation uses one simple integration selected later
in #6. A user-selectable native/compatible switch is not approved. Address and
model-name configuration does not guarantee other servers/models work; document
tested combinations. Additional formats/servers remain possible future work,
not a release commitment. No provider framework or cloud inference is authorized.

## Scoring, repeatability, and latency

Every scored acceptance case must pass all five runs for the candidate
configuration to pass the quality gate. A wrong outcome, incorrect criterion
result, materially wrong compound/uncertainty interpretation, unsupported claim,
invented fact/evidence, invalid response, or unexpected execution failure makes
that attempt unsuccessful. A correct overall label alone is insufficient.

Check separately:
1. Overall outcome and each criterion's status/result.
2. Compound requirements, applicability, uncertainty, and scope interpretation.
3. Authentic and sufficient evidence and faithful explanations.
4. Response structure and execution success.

Report error counts by type rather than averaging away a material failure.
Passing this small reviewed suite establishes success on these examples; it is
not a statistical reliability guarantee or general clinical validation.

Retain every scheduled attempt. No silent retry, discarded failure, or
replacement of a bad answer. A demonstrably broken experiment setup needs a
documented correction and explicit rerun decision.

For each endpoint, record total pair duration, distinguish initial model-loading
time from subsequent requests, and report median, slowest observed duration,
and timeout counts. Quality and speed are separate. **Latency acceptance is
chosen with the human after measurements exist**; no duration is promised here.

Record per attempt: case and fixture version, candidate configuration, endpoint,
repetition, structured assessment content, validation result, observed duration,
pass/fail, and specific reason. Retain the structured answer, not raw model
reasoning. Summarize outcome consistency, evidence quality, failures, and latency
separately. Fixture versioning must identify the exact saved content used.

## Common controlled-fixture conventions

These fictional texts are logic examples, not medical guidance. The explicitly
fictional staging labels avoid asserting equivalence between real clinical
systems or inferring editions, TNM components, or current disease extent.

An Unknown patient fact retains the existing explicit patient-status semantics;
it is not an omitted study field or Not applicable. Patient stage remains
stage at diagnosis. The descriptions below are logical fixture definitions,
not new serialized API contracts.

For each case, the quoted passage is the complete fictional eligibility text.
Other narrative fields (official title, brief summary, detailed description,
study population) are explicitly absent. The common brief title and condition
are present. No additional study text or hidden patient facts are implied.

S = Satisfied; E = Excluded; U = Undetermined; N = No restriction identified.
Result triples list cancer type / stage / disease state. Controlled cases all
have age Satisfied and sex No restriction identified. All Potential match results
retain the limited-scope disclosure; unassessed clinical requirements are not
claimed satisfied.

Evidence follows #4:
- Narrative claims use exact source quotations and application-assigned identifiers.
- Patient-dependent conclusions reference the supplied patient field/value facts used.
- Absence claims identify examined sources and explain the basis; explicit
  unrestricted wording is also referenced when present.
- Genuine missing information is explained without invented values/quotations.
- Cross-criterion alternatives retain the full relationship in affected summaries.
- Equivalent wording and equally valid evidence selections may pass.

## Controlled acceptance fixtures

Common patient: age 45 completed years; Female; Breast cancer; stage II under
**Fixture staging system**; Recurrent disease.

Common study: brief title **Fictional breast cancer study**; condition
**Breast cancer**; structured minimum age **18 Years**, maximum age **65 Years**,
sex **ALL**. Cases change only the facts stated below.

### A1

Patient changes: None.

> Patients with breast cancer are eligible if they have stage IV disease or recurrent disease.

Expected cancer type / stage / disease state: **S / S / S**.
Overall: **Potential match** within the limited assessment scope.
The combined requirement is satisfied through recurrence, without inventing stage IV.

### A2

Patient changes: Disease state Unknown.

> Patients with breast cancer are eligible if they have stage IV disease or recurrent disease.

Expected cancer type / stage / disease state: **S / U / U**.
Overall: **Insufficient information** within the limited assessment scope.
Stage II fails one alternative; unknown recurrence leaves the combined requirement unresolved.

### A3

Patient changes: Disease state Newly diagnosed.

> Patients with breast cancer are eligible if they have stage IV disease or recurrent disease.

Expected cancer type / stage / disease state: **S / E / E**.
Overall: **Nonmatch** within the limited assessment scope.
Neither alternative is satisfied; explain the complete OR requirement in both affected summaries.

### A4

Patient changes: None.

> Patients with breast cancer are eligible. No additional eligibility requirements apply.

Expected cancer type / stage / disease state: **S / N / N**.
Overall: **Potential match** within the limited assessment scope.
Reference the explicit unrestricted statement and examined sources.

### A5

Patient changes: None.

Eligibility criteria are absent; retain the common title, condition, and structured eligibility.

Expected cancer type / stage / disease state: **S / U / U**.
Overall: **Insufficient information** within the limited assessment scope.
Title/condition support cancer alignment. Missing eligibility content does not establish absent restrictions.

### A6

Patient changes: No biomarker fact supplied.

> Only patients with breast cancer are eligible. Ovarian cancer is excluded. Patients with biomarker X present must have stage IV disease. Patients with biomarker X absent must have recurrent disease.

Expected cancer type / stage / disease state: **S / U / U**.
Overall: **Insufficient information** within the limited assessment scope.
Quote the cohort rules and explain that the applicable rule is unknown.

### A7

Patient changes: Cancer type Ovarian cancer; no biomarker fact supplied.

> Only patients with breast cancer are eligible. Ovarian cancer is excluded. Patients with biomarker X present must have stage IV disease. Patients with biomarker X absent must have recurrent disease.

Expected cancer type / stage / disease state: **E / U / U**.
Overall: **Nonmatch** within the limited assessment scope.
The independent cancer exclusion establishes Nonmatch; cohort uncertainty remains.

### A8

Patient changes: Disease state Unknown.

> Patients with breast cancer are eligible. No additional eligibility requirements apply.

Expected cancer type / stage / disease state: **S / N / N**.
Overall: **Potential match** within the limited assessment scope.
An unknown patient fact does not create a study restriction.

### A9

Patient changes: Cancer type Ovarian cancer.

> Only patients with breast cancer are eligible. Ovarian cancer is excluded. There are no stage or disease-state restrictions.

Expected cancer type / stage / disease state: **E / N / N**.
Overall: **Nonmatch** within the limited assessment scope.
Broad-search relevance cannot establish cancer alignment.

### A10

Patient changes: None.

> Patients with breast cancer and stage II under the Fixture staging system are eligible. No disease-state restriction applies.

Expected cancer type / stage / disease state: **S / S / N**.
Overall: **Potential match** within the limited assessment scope.
Reference supplied stage, classification system, and study requirement.

### A11

Patient changes: None.

> Patients with breast cancer and stage 2 under the Fixture staging system are eligible. No disease-state restriction applies.

Expected cancer type / stage / disease state: **S / S / N**.
Overall: **Potential match** within the limited assessment scope.
Same meaning as A10; this paired case changes notation only.

### A12

Patient changes: None.

> Patients with breast cancer must have stage IV disease and recurrent disease to be eligible.

Expected cancer type / stage / disease state: **S / E / S**.
Overall: **Nonmatch** within the limited assessment scope.
Preserve both mandatory requirements; recurrence does not satisfy stage IV.

### A13

Patient changes: stage.status is unknown; stage.value and
stage.classificationSystem are null. The study's staging-system label remains
in its eligibility text.

> Patients with breast cancer must have stage II under the Fixture staging system. No disease-state restriction applies.

Expected cancer type / stage / disease state: **S / U / N**.
Overall: **Insufficient information** within the limited assessment scope.
The study's classification label does not supply the missing patient stage.

### A14

Patient changes: None.

> Patients with breast cancer are eligible regardless of stage or disease state. Patients must have received previous anticancer treatment.

Expected cancer type / stage / disease state: **S / N / N**.
Overall: **Potential match** within the limited assessment scope.
Disclose that treatment history was not assessed; never claim it was satisfied.

## Controlled development fixtures

Common patient: age 52 completed years; Male; **Fictional cancer A**; stage I
under **Development staging system**; Refractory disease.

Common study: brief title **Fictional cancer A development study**; condition
**Fictional cancer A**; structured minimum age **21 Years**, maximum age
**75 Years**, sex **ALL**. Cases change only the facts stated below.

### D1

Patient changes: None.

> Patients with Fictional cancer A must have stage III disease or refractory disease.

Expected cancer type / stage / disease state: **S / S / S**.
Overall: **Potential match** within the limited assessment scope.
Satisfaction is through refractory disease, without inventing stage III.

### D2

Patient changes: Disease state Unknown.

> Patients with Fictional cancer A must have stage III disease or refractory disease.

Expected cancer type / stage / disease state: **S / U / U**.
Overall: **Insufficient information** within the limited assessment scope.
Unknown refractory status leaves the combined requirement unresolved.

### D3

Patient changes: Disease state Newly diagnosed.

> Patients with Fictional cancer A must have stage III disease or refractory disease.

Expected cancer type / stage / disease state: **S / E / E**.
Overall: **Nonmatch** within the limited assessment scope.
Neither alternative is satisfied.

### D4

Patient changes: None.

> Patients with Fictional cancer A are eligible. No additional eligibility requirements apply.

Expected cancer type / stage / disease state: **S / N / N**.
Overall: **Potential match** within the limited assessment scope.
Reference unrestricted wording and examined sources.

### D5

Patient changes: None.

Eligibility criteria are absent; retain the common title, condition, and structured eligibility.

Expected cancer type / stage / disease state: **S / U / U**.
Overall: **Insufficient information** within the limited assessment scope.
Cancer alignment is supported; relevant eligibility content is missing.

### D6

Patient changes: No biomarker fact supplied.

> Only patients with Fictional cancer A are eligible. Fictional cancer B is excluded. Patients with biomarker Y present must have stage III disease. Patients with biomarker Y absent must have refractory disease.

Expected cancer type / stage / disease state: **S / U / U**.
Overall: **Insufficient information** within the limited assessment scope.
Applicable cohort is unknown.

### D7

Patient changes: Cancer type Fictional cancer B; no biomarker fact supplied.

> Only patients with Fictional cancer A are eligible. Fictional cancer B is excluded. Patients with biomarker Y present must have stage III disease. Patients with biomarker Y absent must have refractory disease.

Expected cancer type / stage / disease state: **E / U / U**.
Overall: **Nonmatch** within the limited assessment scope.
Independent cancer exclusion; preserve cohort uncertainty.

### D8

Patient changes: Disease state Unknown.

> Patients with Fictional cancer A are eligible. No additional eligibility requirements apply.

Expected cancer type / stage / disease state: **S / N / N**.
Overall: **Potential match** within the limited assessment scope.
Unknown disease state does not create a restriction.

### D9

Patient changes: Cancer type Fictional cancer B.

> Only patients with Fictional cancer A are eligible. Fictional cancer B is excluded. There are no stage or disease-state restrictions.

Expected cancer type / stage / disease state: **E / N / N**.
Overall: **Nonmatch** within the limited assessment scope.
Reference the actual cancer restriction.

### D10

Patient changes: Stage III.

> Patients with Fictional cancer A and stage III under the Development staging system are eligible. No disease-state restriction applies.

Expected cancer type / stage / disease state: **S / S / N**.
Overall: **Potential match** within the limited assessment scope.
Classification label is the same in patient and study.

### D11

Patient changes: Stage III.

> Patients with Fictional cancer A and stage 3 under the Development staging system are eligible. No disease-state restriction applies.

Expected cancer type / stage / disease state: **S / S / N**.
Overall: **Potential match** within the limited assessment scope.
Paired notation-only variation of D10.

### D12

Patient changes: None.

> Patients with Fictional cancer A must have stage III disease and refractory disease to be eligible.

Expected cancer type / stage / disease state: **S / E / S**.
Overall: **Nonmatch** within the limited assessment scope.
Do not replace AND with OR.

### D13

Patient changes: stage.status is unknown; stage.value and
stage.classificationSystem are null. The study's staging-system label remains
in its eligibility text.

> Patients with Fictional cancer A must have stage III under the Development staging system. No disease-state restriction applies.

Expected cancer type / stage / disease state: **S / U / N**.
Overall: **Insufficient information** within the limited assessment scope.
Missing patient stage prevents a conclusion.

### D14

Patient changes: None.

> Patients with Fictional cancer A are eligible regardless of stage or disease state. Patients must have received previous anticancer treatment.

Expected cancer type / stage / disease state: **S / N / N**.
Overall: **Potential match** within the limited assessment scope.
Disclose unassessed treatment history; do not assert its satisfaction.

## Real-study snapshots and retrieval provenance

Retrieved on **2026-10-28 UTC** from ClinicalTrials.gov API v2. Exactly two
pages of ten records returned **20 distinct NCT identifiers**. There were no
duplicates, replacement pages, application imports, or inference calls.

The first request used:
- Endpoint: `https://clinicaltrials.gov/api/v2/studies`
- `format=json`
- `query.cond=cancer`
- `filter.overallStatus=RECRUITING,NOT_YET_RECRUITING`
- `filter.advanced=AREA[StudyType](INTERVENTIONAL OR OBSERVATIONAL)`
- `sort=@relevance`
- `pageSize=10`

The second request preserved every parameter and passed the first response's
`nextPageToken` as `pageToken`. No patient-specific search, geographic filter,
age filter, relevance manipulation, or further page retrieval occurred.

The four files below retain each selected full study object from the response,
unchanged in decoded JSON content; pretty-printing changes serialization only.
They are saved snapshots, not claims about future live registry content.
Use these files for reproducibility; a fresh retrieval is a new snapshot requiring
review. Full supplied titles, conditions, brief summary, detailed description,
eligibility criteria, and study population are used when present, with absent
sources explicit. The short excerpts below do not replace full inputs.

Patient evidence remains fictional and separate from the ten application patients.
No biomarker, treatment-history, laboratory, consent, or willingness fields are added.

### R1: supported alignment

Source: [NCT07277777](https://clinicaltrials.gov/api/v2/studies/NCT07277777).
Snapshot: [NCT07277777.json](fixtures/inference-evaluation/studies/NCT07277777.json).

Study: 125I Seed Brachytherapy Combined With Immunotherapy for Primary, Recurrent,
or Metastatic Malignant Tumors.

Patient: age 52 completed years; Female; Breast cancer; stage Unknown,
classification Unknown; Recurrent disease.

Structured eligibility: 18 Years to 80 Years; sex ALL.
Eligibility excerpt:

> Histologically or clinically confirmed primary, recurrent, or metastatic malignant tumor.

Expected age S, sex N, cancer type S, stage S, disease state S;
overall **Potential match within the limited scope**. Both affected stage/state
summaries explain satisfaction of the combined rule through recurrence,
without inventing a stage or current metastatic extent. Cancer alignment cites
the broad malignant-tumor inclusion. Cite the supplied recurrence fact and study
rule. Do not claim implantation suitability, laboratory criteria, treatment
eligibility, or other unassessed requirements are satisfied.

### R2: independent cancer-type exclusion

Source: [NCT06685289](https://clinicaltrials.gov/api/v2/studies/NCT06685289).
Snapshot: [NCT06685289.json](fixtures/inference-evaluation/studies/NCT06685289.json).

Study: Donafenib Combined With Capecitabine for Postoperative Adjuvant Therapy
of Biliary Malignant Tumors With High-risk Recurrence Risk.

Patient: age 52 completed years; Female; Breast cancer; stage Unknown,
classification Unknown; disease state Unknown.

Structured eligibility: 18 Years to 75 Years; sex ALL.
The supplied disease population is biliary malignancies. Eligibility also
contains detailed TNM/alternative risk requirements and recurrence restrictions.

Expected age S, sex N, cancer type E, stage U, disease state U;
overall **Nonmatch**. Reference the actual biliary disease requirement and
supplied breast-cancer diagnosis, not search relevance. Explain missing facts
and unresolved compound requirements in the other summaries rather than
inventing TNM values or disease status. All narrative summaries remain required.

### R3: disease-state exclusion despite unresolved age

Source: [NCT05615974](https://clinicaltrials.gov/api/v2/studies/NCT05615974).
Snapshot: [NCT05615974.json](fixtures/inference-evaluation/studies/NCT05615974.json).

Study: A Phase I/II Study of LM-101 Injection in Patients With Advanced Malignant Tumors.

Patient: age 52 completed years; Female; Breast cancer; stage Unknown,
classification Unknown; Newly diagnosed disease.

Structured minimum age: 18 Years. **Maximum age is absent.** Sex ALL.
Eligibility excerpt:

> recurrent or refractory advanced solid tumors

Expected age U, sex N, cancer type S, stage U, disease state E;
overall **Nonmatch**. Explain the independent recurrent/refractory requirement
and supplied newly diagnosed state. Broad solid-tumor inclusion supports cancer
alignment. Stage remains unresolved; do not infer current advanced disease.
Absence of maximum age is unresolved under #4, not an unlimited upper bound.
The reliable disease-state exclusion determines the overall outcome.

### R4: unresolved cohort applicability and missing facts

Source: [NCT07004244](https://clinicaltrials.gov/api/v2/studies/NCT07004244).
Snapshot: [NCT07004244.json](fixtures/inference-evaluation/studies/NCT07004244.json).

Study: Application of KRAS Vaccine in the Treatment of KRAS-mutated Malignancies.

Patient: age 52 completed years; Female; Lung adenocarcinoma; stage Unknown,
classification Unknown; disease state Unknown. No treatment or biomarker facts supplied.

Structured minimum age: 18 Years. **Maximum age is absent.** Sex ALL.
Cohort 2 eligibility excerpt:

> Newly diagnosed, treatment-naïve lung adenocarcinoma

That cohort also requires stage IB-IIIA under AJCC ninth-edition criteria.
Cohort 1 has different treatment-related entry conditions.

Expected age U, sex N, cancer type S, stage U, disease state U;
overall **Insufficient information**. Cite supplied broad solid-tumor/lung
disease scope and the actual cohort rules. Explain missing patient stage/state
and unresolved cohort applicability. Do not choose a favorable cohort, invent
an edition or stage, or add biomarker/treatment facts. A missing out-of-scope
biomarker alone is not an automatic in-scope exclusion. Maximum age remains unresolved.

## Controlled application checks

These are agreed coverage and expected behavior, not implemented tests or a
new test-harness specification. They do not contribute to the model-quality
acceptance request count.

| Group | Required coverage and expectation |
| --- | --- |
| Deterministic age/sex | Inclusive age boundaries; conservative comparisons for years, months, weeks, days, hours, minutes; invalid/inverted and missing bounds; sex ALL; unknown patient sex; independent reliable exclusions. Exact calendar arithmetic/test values remain later specification work. |
| Routing | Compute both deterministic summaries. A reliable age/sex exclusion skips inference and marks all narrative summaries Not assessed. Otherwise inference runs even with an Undetermined deterministic summary. |
| Invalid response | Prepared invalid JSON, missing narrative criterion, unexpected field, invalid result value: invalidResponse, failed attempt, no completed assessment. |
| Invalid evidence | Prepared nonexistent identifier, altered quotation, fabricated patient value: invalidEvidence, reject the whole response even if another part contains an authentic exclusion. |
| Semantic interpretation | Prepared authentic quotation of A1's OR rule with an incorrect stage-II Nonmatch: model-quality failure despite authentic evidence. |
| Initial unavailability | Stop clearly with inferenceUnavailable; preserve completed work and do not fabricate outcomes for unattempted pairs. |
| Pair request failure | Timeout or other request error after work starts: record inferenceTimeout/inferenceError and continue remaining pairs as #4 specifies. |
| Cancellation | Request cancellation, stop further work, retain completed work; interrupted active pair remains unfinished. |
| Context size | Agreed full inputs cannot fit: inputTooLarge, no silent truncation. Record separately and assess representative-case feasibility; frequent failures can make a configuration unsuitable. |

A deliberately injected failure passes its application check when handled
correctly. Unexpected failures in scored inference attempts remain unsuccessful
attempts. Mechanical validation cannot prove semantic correctness.

## Validation and remaining work

Local checkpoint validation passed: all four snapshot JSON files parsed and
matched the already-retrieved decoded study objects; all 14 acceptance and 14
development controlled-case headings were present exactly once; all four approved
glossary terms were present; all seven local document links resolved; and the new
documents/snapshots passed trailing-whitespace checks. Tracked git diff --check
also passed. Git status showed only the approved new files and the pre-existing
uncommitted glossary and assessment-contract document, with no tracked edits.
No application tests, model-quality runs,
latency measurements, independent automated code review, or application smoke
test are claimed for these documentation writes.

The four real-study expectations and all controlled fixture texts were reviewed
and approved by the human before any candidate output existed. No inference
evaluation has occurred. Numerical latency limits remain deferred by explicit
agreement until measurements are available. Experimental preparation and
execution still need their own concrete scope and approval.

The original local-write checkpoint preceded tracker publication. The human later
approved the prepared #5 resolution, closure, and #1/#2 updates; all were published
and verified. A subsequent documentation review found that A13/D13 retained a
patient classification label with Unknown stage, contrary to the delivered patient
contract. The human approved setting both patient stage value/classification to
null in those cases and making the matching correction in the published #5
resolution. Study requirements and expected results remain unchanged.

The approved local files remain uncommitted. The canonical published #4 decision
is unchanged; its local document now labels its original authorization checkpoint
as historical. Use the current overview for authorization. No branch, commit,
application change, or experiment is authorized by these corrections.
