# OncoMatch

OncoMatch screens fictional oncology patients against clinical research studies within a deliberately limited assessment scope.

## Language

**Pair assessment**:
A completed screening assessment of one patient against one study within the limited assessment scope.
_Avoid_: Match score, eligibility determination

**Criterion summary**:
The requirements, supplied patient facts, conclusions, and explanations for one assessed criterion within a pair assessment. Requirements may include alternatives or combinations that must retain their meaning.

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

## Criterion results

These describe individual criteria, separately from the overall pair-assessment outcomes.

**Satisfied**:
The applicable requirement is understood and supplied patient facts support it.

**Excluded**:
Reliable evidence establishes that the patient fails an applicable requirement.

**Undetermined**:
Missing information or ambiguity prevents a criterion-level conclusion.

**No restriction identified**:
No applicable restriction was identified in the supplied study content examined for a criterion. This is distinct from missing or unusable relevant content and from an unclear requirement.

**Not assessed**:
A criterion for which no assessment was performed, with the reason retained. This describes assessment status, not a criterion result or evidence of uncertainty in the underlying facts.
