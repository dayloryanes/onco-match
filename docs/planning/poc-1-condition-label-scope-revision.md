# POC 1 condition-label scope revision and checkpoint

Date: 2026-10-29. Status: agreed direction, with detailed contract decisions still
open. This planning draft is preserved in a local commit separate from the
experimental runner checkpoint. The linked GitHub issue checkpoint is published;
the baseline/design files have not been revised. Draft status describes unfinished
design decisions, not permission to implement them or run another experiment.

The local draft, approved GitHub issue updates, temporary issue-file cleanup, and
separately approved local runner/planning commits form this checkpoint. Stop here;
the next focused design session awaits approval. Earlier experiment approvals are
consumed and do not authorize retries or additional batches. No push is approved.

## Why revise the planned scope

The tested local configurations did not meet the narrative-assessment quality
requirements and introduced substantial latency. Authentic quotations and valid
JSON repeatedly accompanied incorrect interpretations of OR requirements,
unknown patient facts, conditional cohorts, and absent restrictions.

POC 1 will therefore focus on deterministic age/sex checks and bounded AI
assessment of study condition labels, including explicitly stated qualifiers
that can be compared with supported patient facts. Narrative eligibility
interpretation is deferred. The narrower task still needs evaluation; no model
has been selected or shown to meet its requirements.

This revises the planned assessment and evaluation work. It does not undo the
delivered login and patient-browsing slice. Existing architecture, synthetic
patients, import lifecycle, and other unrelated POC agreements remain in place.

## Agreed direction

| Responsibility | Proposed POC 1 behavior |
| --- | --- |
| Study import/filtering: code | Keep the existing cancer search, Recruiting/Not yet recruiting statuses, and interventional/observational study types. No age or geography restriction and no new editable filters. |
| Age: code | Compare patient age with structured minimum/maximum ages using the reviewed parsing and uncertainty rules. |
| Sex: code | Compare patient sex with the structured allowed-sex value. Do not infer gender eligibility from it. |
| Condition-label alignment: AI | Interpret the complete supplied condition labels against the patient's cancer type, stage/classification, and disease state where the label explicitly contains those qualifiers and the comparison is supportable. |
| Unsupported qualifiers | Preserve and flag explicit qualifiers such as biomarkers or treatment history as unassessed; do not invent missing patient facts or favorable cohort applicability. |
| Validation: code | Validate response shape, allowed values, source identifiers, exact supplied patient values, and authentic condition-label references. Traceability does not establish semantic correctness. |
| Presentation/aggregation: code | Present the limited checked scope and unassessed information. Exact outcomes, wording, and aggregation remain open decisions. |

The AI task reads condition labels, not eligibility paragraphs, titles, summaries,
detailed descriptions, study-population narratives, or gender descriptions.
Do not silently add another input field to resolve an ambiguity.

Absence of a stage or disease-state qualifier means it is not specified in the
condition labels; it does not establish that the study has no such restriction.
Unknown patient facts and unassessed requirements remain distinct. Do not infer
staging-system equivalence, disease state, or a cohort selector. A condition-label
conflict does not automatically establish full-study exclusion; the exact
pair-assessment effect is a pending design decision.

Location information remains descriptive. Trial phase describes the research
phase, not the patient's cancer stage. Broad age groups are derived metadata,
not an additional independent eligibility criterion. Healthy-volunteer and
gender-based flags alone do not supply complete new patient-matching rules.

## Data-field distinctions verified during discussion

| Registry field | Representation |
| --- | --- |
| eligibilityModule.minimumAge/maximumAge | Normalized time quantities; the amount is entered, not a fixed shortlist of ages. |
| eligibilityModule.sex | Fixed values: FEMALE, MALE, ALL. |
| eligibilityModule.stdAges | Derived fixed categories: CHILD, ADULT, OLDER_ADULT. |
| conditionsModule.conditions | Text array of disease labels, not a guaranteed common code list. Established medical terminology is encouraged. |
| statusModule.overallStatus | Fixed status enumeration; existing import filters remain unchanged. |
| designModule.studyType/phases | Fixed enumerations, distinct from patient stage. |
| contactsLocationsModule.locations | Structured records containing names/geographic information, optional coordinates, and an enumerated site status. |

References: [official field metadata](https://clinicaltrials.gov/api/v2/studies/metadata),
[official API schema](https://clinicaltrials.gov/api/oas/v2),
[condition-field definition](https://clinicaltrials.gov/policy/protocol-definitions#PrimaryCondition).
No application/API response contract is finalized by this data inventory.

## Focused decisions to resolve next

Use the grilling skill, with domain-modeling for meaningful terminology. Preserve
the agreed direction above; do not restart the whole project discussion.

1. What are the condition-label alignment results and qualifier results, and how
   do they relate to the existing criterion/outcome vocabulary?
2. What does a clear label conflict mean for the displayed pair result, without
   representing it as complete clinical eligibility assessment?
3. How are multiple labels and unclear relationships handled without assuming
   that all labels are mandatory or that an unsupported relationship holds?
4. How do missing patient facts, absent qualifiers, ambiguous staging context,
   and unsupported qualifiers affect uncertainty and review flags?
5. Which values and exact source references must the model return, what can code
   verify mechanically, and which interpretation checks remain part of evaluation?
6. Does an age/sex exclusion still skip inference, as in the existing design?
7. What result wording and assessment-limit notice accurately explain this scope?
8. Which development/acceptance fixtures and latency/repetition boundaries are
   appropriate for this narrowed task before running models?

The old three-criterion narrative schema and expected answers must not simply be
relabelled as validation of the new task. Prepare new cases for label variations,
unrelated/broad conditions, explicit qualifiers, unknown facts, unsupported
qualifiers, and multiple labels. Agree expectations before candidate output.

The proposed first comparison uses the three installed candidates without
reasoning: Qwen Instruct-2507, Phi Mini, and the hybrid Qwen with explicit
think:false. That is a proposed future evaluation, not current authorization.
Freeze common inputs/settings after design review, verify each model's controls,
and record native template differences. Reassess budget needs for the new task.
A smaller Qwen candidate can be considered later after its exact tag/size are
verified. No additional download or thinking experiment is planned automatically.

## Narrative work deferred, without a release promise

Narrative eligibility interpretation, narrative/structured consistency checks,
and broader clinical criteria remain possible future investigations. No POC 2
or later release is promised for them. Future consideration requires an agreed
scope, independent fixtures, evidence-valid and semantically correct outputs,
reliable uncertainty handling, and measured practical execution costs.

Do not describe narrower label alignment as solving the earlier narrative task.

## Published GitHub checkpoint and pending design-record changes

- Created [Revise POC 1 assessment scope to condition-label alignment (#12)](https://github.com/dayloryanes/onco-match/issues/12),
  labelled wayfinder:grilling and linked as a native sub-issue of map #2. Detailed
  decisions remain open; no implementation tickets were created.
- Updated [overview #1](https://github.com/dayloryanes/onco-match/issues/1) with
  the current stage, authorization, workspace checkpoint, next action and resume
  prompt. It remains the canonical published entry point.
- Updated [map #2](https://github.com/dayloryanes/onco-match/issues/2) with the new
  frontier and corrected earlier no-experiment statements.
- Added scope-revision notices to [assessment decision #4](https://github.com/dayloryanes/onco-match/issues/4)
  and [evaluation decision #5](https://github.com/dayloryanes/onco-match/issues/5).
  Both remain closed; their original bodies and historical evidence are retained.
- Updated [model-selection decision #6](https://github.com/dayloryanes/onco-match/issues/6)
  with measured findings and current boundaries. Native issue dependencies now
  record that #6 is blocked by #12. No model/API selection has been made.
- Read all six published bodies back and verified exact content, preserved closed
  states, the decision label, parent #2, and the #12 blocking relationship for #6.
- After resolving the new decision and obtaining the applicable approvals,
  revise the baseline, glossary if needed, assessment design, and evaluation
  design. Preserve the old experiment fixtures/results as historical evidence.

Issues remain authoritative in GitHub. This document is a scope draft/checkpoint,
not a second issue tracker. A later GitHub publication does not automatically
authorize a local commit, push, PR, new specification, or live run.

## Experiment checkpoint

Latest completed six-case batches used D1/D3/D5/D6/D9/D13 once each through the
native API. Mechanical completion is distinct from correct interpretation.

| Configuration | Mechanically completed | All expected criterion labels match | Total request time |
| --- | ---: | ---: | ---: |
| Phi Mini, corrected evidence request | 0/6 | 0/6 raw answers | 4m29s |
| Qwen Instruct-2507, same corrected request | 6/6 | 2/6 | 8m34s |
| Ordinary Qwen hybrid, explicit thinking | 5/6 | 0/6 | 18m10s |

The Phi/Instruct requests differed only by model. The hybrid trial changed
checkpoint, reasoning mode, sampling and deadline together, so it does not isolate
thinking's causal effect. Even the two matching Instruct label sets did not prove
complete explanation quality. None met the narrative development requirements.
Full repeated acceptance and live compatible-route comparisons remain unrun.

The hybrid D1 hit the combined 2,048 generation-token cap without final JSON.
Its five completed answers contained interpretation errors. All six attempts
finished; no timeout or retry occurred. A separate earlier hybrid attempt stopped
with D1 unfinished during a Modern Standby interruption. Offline investigation
found that UTC gaps did not demonstrate failure of the Windows relative timer.
Two local-stub HTTP timeout probes and the existing 67 checks passed. No model
request ran during that diagnosis. The user controls power settings; no automatic
sleep prevention was implemented or approved.

Workspace-only evidence, ignored by Git:

- [Latest Phi review](../../tools/inference-evaluation/results/native-phi4mini-evidence2-20261029-100810/REVIEW.md).
- [Latest Qwen Instruct review](../../tools/inference-evaluation/results/native-qwen4b-evidence2-20261029-104125/REVIEW.md).
- [Completed hybrid thinking review](../../tools/inference-evaluation/results/native-qwen4b-thinking2-20261029-114250/REVIEW.md).
- [Standby/timeout postmortem](../../tools/inference-evaluation/results/diagnosis/timeout-postmortem/DIAGNOSIS.md).
- [Candidate research](../../tools/inference-evaluation/results/research/next-reasoning-candidate-20261029.md).

These paths require this workspace. They are not GitHub links and are not present
in a fresh clone. Any public findings summary needs separate publication approval.
Reviews were presented through chat; no additional human file inspection,
independent automated code review, final model acceptance, or clinical validation
is claimed. No application tests were rerun for the console experiments.

## Workspace and file hygiene checkpoint

Branch: chore/inference-evaluation. Previous checkpoint:9651aed,
fix: ground evaluation evidence and correct input sizing. The five experimental
runner files are now committed as917fa26, feat: support bounded native thinking
trials. This planning draft is committed separately afterward; use git log for
that commit's identifier. These local commits have not been pushed. The last
published design-file checkpoint is main at
860452c through PR11. The published overview now records this checkpoint and
links #12 and #6. The issue checkpoint is refreshed with the local commit status;
repository files on GitHub remain at the preceding design checkpoint until a
separate push is approved. This section is a checkpoint snapshot; git status is
the live authority for later workspace changes.

The reusable evaluation console is tracked under tools/inference-evaluation,
separate from the application. It has 12 tracked files. Ignored results/ contains
requests, responses, exact input snapshots, metrics, consumed approvals, research,
and diagnostics. bin/, obj/, and the local execution-approval.json are ignored.
Model weights were downloaded through Ollama and are not evaluation Git artifacts.

The five previously pending tracked files are included in runner commit917fa26:

| File | Committed experimental work |
| --- | --- |
| tools/inference-evaluation/Evaluation.cs | Explicit native thinking control, compatible-control guard, reviewed timeout validation, and reasoning-observed boolean without retained reasoning text. |
| tools/inference-evaluation/OfflineChecks.cs | 67 offline checks, including the new control and completion guards. |
| tools/inference-evaluation/Program.cs | Use the configured 120/300-second deadline in execution/previews. |
| tools/inference-evaluation/README.md | Experimental setup/history and thinking-configuration documentation. |
| tools/inference-evaluation/candidate.json | The last tested hybrid thinking profile, not a selected POC model or a condition-label candidate. |

The runner diff was reviewed as completed experimental preparation and its README
updated with the completed trials and scope boundary. The code was unchanged from
the version previously passing67 offline checks; no application tests or inference
were repeated for these commits. This document is preserved in the separate
planning commit. Neither commit implements condition-label assessment, finalizes
the draft contract, selects a model, or authorizes the next experiment. Retain the
last tested profile as historical configuration rather than silently treating it
as the new scope's baseline.

Temporary issue-inspection copies and outgoing Markdown payloads were held in the
ignored tools/inference-evaluation/results/checkpoint-publication-20261029/
directory. After publication/read-back verification, all 11 known temporary files
and that empty directory were removed under the approved cleanup. Absolute paths
and file names were checked before deletion. No permanent issue mirror is kept.
Actual run evidence, research and diagnostics were outside that cleanup target
and remain intact. Build outputs are regenerable and can be cleaned separately
if desired. Do not add results/ or issue JSON mirrors to Git.

## Resume here

Start with the project workflow and overview #1, then read this local draft and
the linked baseline/designs. Distinguish the agreed narrowed direction from the
unfinished detailed contract and from old published narrative requirements.

The next proposed work is a focused grilling session on the open decisions,
followed by approved design-record updates and new evaluation preparation.
Permission is required before starting that next stage. No model run, download,
power change, code implementation, cleanup, commit or GitHub mutation follows
automatically from this checkpoint. The local/GitHub checkpoint task ends here.
