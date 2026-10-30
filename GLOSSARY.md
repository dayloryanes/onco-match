# OncoMatch

OncoMatch screens fictional oncology patients against clinical research studies within a deliberately limited assessment scope.

## Language

**Pair assessment**:
A completed screening assessment of one patient against one study within the limited assessment scope.
_Avoid_: Match score, eligibility determination

**Criterion summary**:
The supplied patient facts, applicable requirements, findings, and explanations for one assessed aspect of a pair. A direct comparison is distinct from the conclusion about a complete condition label.

**Condition label**:
One complete text entry in a study's listed conditions. It can contain a disease name and explicit qualifiers; separate labels do not by themselves establish an eligibility AND/OR relationship.
_Avoid_: Eligibility rule, diagnosis code

**Qualifier**:
Information in a condition label that narrows or describes its disease scope, such as stage, disease state, a biomarker, or treatment history.

**Unassessed qualifier**:
An explicit modifier within a condition label that is outside the supported assessment scope. An unsupported standalone label retains its own assessment and explanation rather than being treated as a modifier of another label.

**Condition-label assessment**:
The interpretation of one complete condition label against supplied cancer-type, stage, and disease-state facts, preserving its explicit relationships and unassessed information.

**Pair summary**:
The summary of structured age/sex findings and complete condition-label results for one patient and one study. It is not a determination of full-study clinical eligibility.

**Cancer-type alignment**:
Supported correspondence between the patient's supplied cancer type and the study's disease scope. Search relevance alone does not establish this correspondence.

**Compound requirement**:
A requirement containing conditions that apply together or as alternatives. Its meaning includes how those conditions relate and which patient group they govern.

**Assessment input snapshot**:
The exact patient facts and study content supplied for an evaluation and retained with its completed pair assessment.

**Evidence reference**:
A precise reference to supplied facts or study content in an assessment input snapshot. An authentic reference establishes traceability but does not establish that its interpretation is correct.

## Evaluation language

**Evaluation fixture**:
Fixed patient facts and study content with reviewed expectations for assessing behavior.

**Development fixture**:
An evaluation fixture available for refining a candidate.

**Acceptance fixture**:
An evaluation fixture reserved for judging a frozen candidate; using it to guide changes retires it from acceptance.

**Candidate configuration**:
The recorded model, prompt, inference settings, and software versions evaluated together.

## Condition-label findings

**Alignment supported**:
The supplied facts support correspondence within the assessed scope. For a complete label, its explicit supported qualifiers and relationships are considered; unassessed information remains separate.

**Conflict identified**:
A supported comparison identifies a conflict with supplied facts. A direct comparison conflict is not necessarily a complete-label conflict when it is an alternative to another satisfied condition.

**Undetermined**:
Missing facts or ambiguous meaning prevent a supported conclusion at the level being described.

**Not specified in condition labels**:
No explicit in-scope stage value/classification or supported disease-state qualifier is stated in the examined label. Other extent wording can remain visibly unassessed; this says nothing about restrictions elsewhere in the study.
_Avoid_: No restriction identified

**Not assessed**:
Information for which no assessment was performed, with the reason retained. This is distinct from an undetermined comparison and does not establish that the underlying requirement is satisfied.

## Pair summaries

**Excluded by age/sex**:
At least one reliable structured age/sex comparison excludes the patient; condition-label assessment is skipped.

**Condition-label alignment supported**:
All assessed complete labels support alignment within scope. Structured uncertainty and unassessed qualifiers remain visible separately.

**Mixed condition-label findings**:
At least one complete label supports alignment and at least one other label conflicts or is undetermined.

**Condition-label conflicts identified**:
Every supplied complete label has a reliable conflict within the assessed scope. This does not establish full-study ineligibility.

**Condition-label alignment undetermined**:
No complete label supports alignment and at least one is undetermined, or no condition text is supplied without a reliable age/sex exclusion.

## Structured checks and historical narrative vocabulary

Satisfied, Excluded, and No restriction identified remain structured-check terms.
Earlier narrative design records also use them for narrative criteria; that use
does not define the revised condition-label findings.

**Satisfied**:
The applicable requirement is understood and supplied patient facts support it.

**Excluded**:
Reliable evidence establishes that the patient fails an applicable requirement.

**No restriction identified**:
An applicable structured value explicitly establishes unrestricted eligibility, such as study sex ALL. In historical narrative records it also described an absence claim; the revised label assessment uses Not specified in condition labels instead.
