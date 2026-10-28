# Development workflow

## Start and resume

The GitHub issue titled **OncoMatch POC 1 — Task development overview** is the
single entry point. Its URL is recorded in `issue-tracker.md`. Read its current
stage, authorization, next action, and relevant linked records before acting.
If GitHub is unavailable, report that limitation and use local documents only
for orientation; ask before proceeding with work whose authorization is unclear.

Read `../planning/poc-1-baseline.md` for agreed scope. The baseline records the
established decisions; the Wayfinder map tracks remaining decisions. Use existing
records rather than reconstructing or reopening settled choices.

At a session checkpoint, update the overview within the authorized scope with
the current stage, work links, validation performed, open questions, and next
action. Mark the next stage as awaiting approval when it has not been authorized.
Reference durable artifacts rather than creating a duplicate handoff document.

## Human involvement and authorization

This is a hands-on learning project. Explain meaningful tradeoffs in plain
language and keep the human involved in significant decisions.

Obtain explicit approval for scope changes, significant design decisions,
experiments, new specifications and implementation tickets, publication of
artifacts, starting an implementation slice, commits, and merges. Approval of
planning does not authorize implementation. Approval of creating local documents
does not by itself authorize publishing them. Present concrete proposed content
or behavior for review before asking for approval.

Approval of a bounded implementation slice covers its routine file creation,
naming, edits, and agreed tests. Resolve ordinary implementation details within
that scope without asking about each file or method. Ask when terminology or an
API contract exposes a meaningful ambiguity or changes approved behavior.
Present meaningful domain concepts, public API names, and data contracts during
design review. Stop before beginning an unapproved stage or unrelated work.

Track the current authorization in the overview and relevant work item.
Permissions do not expand merely because a skill normally publishes, commits,
spawns agents, or proceeds to another phase. Apply the human's restrictions.

## Select the skill for the work

Read the applicable installed SKILL.md before using its procedure. Some skills
require explicit invocation; recommend the next skill when needed rather than
assuming the human remembers the sequence. `ask-matt` is the installed router
for uncertainty about which skill or flow fits.

| Situation | Skill or flow |
| --- | --- |
| Large effort with connected unresolved decisions | wayfinder |
| Human decision or domain terminology needs clarification | grilling and domain-modeling; grill-with-docs when appropriate |
| External facts are needed | research, within approved artifact/publication boundaries |
| A concrete artifact is needed to decide behavior or UI | prototype, only when authorized |
| Settled behavior needs a specification | to-spec; confirm testing boundaries before publication |
| Approved work needs vertical slices and dependencies | to-tickets; approve the breakdown before publication |
| An implementation slice is authorized | implement with tdd at agreed testing boundaries |
| Changes need assessment against standards and the specification | code-review |
| Writing an authorized PR body | pr, using its existing format without duplicating or overriding it |
| Difficult bug or regression | diagnosing-bugs |
| Session reveals workflow/environment problems | consider retro; review proposals before changes |
| Context is not captured in durable artifacts and must travel | handoff, only when useful and authorized |

Use one implementation slice per fresh session where practical. A full parallel
`implement-spec` run is not the default for this learning project. Consider retro
after the first completed slice or a session that reveals a concrete process
problem. Automated review is not human approval.

## Naming

- Commit subjects and PR titles use Conventional Commits style: lowercase type
  and description, such as `feat: add demo login` or
  `fix(studies): preserve pagination parameters`.
- Use a scope when the change clearly belongs to a feature or area; omit it for
  changes without a useful single scope. Examples include `auth`, `patients`,
  `studies`, `match`, `frontend`, and `backend`; this is not a closed list.
- Branches use lowercase type prefixes and hyphenated descriptions, such as
  `feat/demo-login` or `docs/record-poc-1-planning`.
- Issue titles use sentence case and descriptive names. Preserve established
  Wayfinder labels exactly; title prefixes are not substitutes for labels.
- Commit subjects, PR titles, issue titles, and branch names have no trailing
  period. Description prose uses normal capitalization and punctuation.

### Merge recommendations

Before recommending a merge method, inspect the branch's commits and explain
the resulting history. Base the recommendation on the desired history and the
purpose of the commits. Commit count or a previous merge choice alone does not
determine the method. Clearly distinguish branch names, commit titles, PR titles,
and merge-commit titles.

## Implementation and review expectations

Prefer small, explicit, reviewable changes and familiar C# syntax initially;
explain useful newer features when they matter. Use validation, sensible HTTP
responses, asynchronous I/O, cancellation, error handling, and logging where
appropriate. Introduce abstractions and dependencies only for concrete needs.

Recognize generated backend/frontend boilerplate. Replace or remove it when
relevant to an approved slice and safe to do so; deleting all templates is not
a first-slice requirement. Retain useful configuration and infrastructure.

Introduce meaningful tests with features. Use deterministic fixtures and mock
external services in automated tests; live inference evaluation is separate.
Record automated tests, automated code review, and maintainer manual validation
separately. State the scope, result, and limitations of each. Use standard
validation terminology. Describe application smoke testing and source-code
review as distinct activities. In PRs, identify AI assistance and review status
accurately; claim only review that has occurred.
Use the installed pr skill for its visual summary, evidence, and risk format.

## Documentation and release

Document delivered behavior, configuration, setup from zero, tested inference
combinations, assessment limits, and significant tradeoffs. Separate POC 1 from
provisional future improvements. Exclusion does not imply a promised future
release. Fixes to agreed POC 1 behavior remain POC 1 fixes.

Treat detailed development-machine specifications supplied in conversation as
private context. Do not publish them. If hardware documentation is needed, agree
its contents first; only generic RAM and dedicated-VRAM capacities were permitted
for consideration, not identifying specifications.

Before releasing POC 1 and finalizing README/setup documentation, revisit
licensing with the human. The current intent is a public portfolio POC, not an
approved open-source licensing commitment. Do not select or add a license without
approval. Dependency, model, and external-data terms are separate considerations.

Reference: [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/).
