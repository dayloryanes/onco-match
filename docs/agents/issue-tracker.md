# Issue tracker: GitHub

Issues and specs live in dayloryanes/onco-match on GitHub.
Use the gh CLI from this clone, or pass --repo dayloryanes/onco-match.

## Development entry point

Start at [OncoMatch POC 1 — Task development overview](https://github.com/dayloryanes/onco-match/issues/1).
It tracks the current stage, next action/skill, authorization, and links; it is
not a Wayfinder map or an implementation ticket. Read
`docs/agents/development-workflow.md` for standing approval and naming rules.

The agreed baseline lives in `docs/planning/poc-1-baseline.md`.
[Resolve remaining OncoMatch POC 1 decisions](https://github.com/dayloryanes/onco-match/issues/2)
is the Wayfinder map for unresolved decisions. Keep established scope in the
baseline, new decision details in their issues, and navigation in the overview.
Link records rather than duplicating their contents. These local documents
must be committed and published with approval before a fresh clone can read them.

Specifications are GitHub issues created through to-spec after approval.
Implementation tickets are approved vertical slices from to-tickets where
needed. A decision's resolution does not authorize specification publication or
implementation. PR descriptions use the installed pr skill; automated review and
skill invocation do not replace human approval.

## Operations

- Create: gh issue create --title "..." --body-file <file>
- Read: gh issue view <number> --json number,title,body,labels,comments
- List: gh issue list --state open --json number,title,body,labels
- Comment: gh issue comment <number> --body-file <file>
- Apply/remove labels: gh issue edit <number> --add-label "..." / --remove-label "..."
- Close: gh issue close <number>

Use a UTF-8 file for multiline bodies.

When a skill says "publish to the issue tracker", create a GitHub issue.
When it says "fetch the relevant ticket", read the GitHub issue.

## Pull requests as a triage surface

PRs as a request surface: no.

## Delivery and post-merge cleanup

Within the maintainer's authorized publication/cleanup scope:

1. Verify the PR is merged and compare its merged files with the reviewed feature
   tip. Explain that this confirms the merge preserved the reviewed code; retain
   the original test settings and limits when reporting prior test results.
2. Synchronize a clean local main with origin/main. Preserve local changes if the
   working tree is dirty rather than overwriting them during cleanup.
3. Verify the native PR closing relationship targets the delivered implementation
   ticket, not an unfinished parent specification. Remove active readiness labels
   such as ready-for-agent from the closed ticket; preserve Wayfinder labels.
4. Put one delivery comment on the closed issue, linking the PR and recording
   implementation, validation and known limits. Update an existing delivery
   comment if one is already present. Keep PR evidence and stable source links in
   the PR description; add a PR cleanup comment only if specifically requested.
5. Update the overview, map, parent and next-ticket references where relevant.
   Verify native dependency status and preserve the maintainer's recorded
   authorization; an unblocked ticket is not automatically started or authorized.
6. Replace branch-based document links with immutable merged-commit links before
   removing the verified merged branch locally/remotely. Clean that task's
   disposable files as described in the development workflow.

Finish by reading back the issue state, labels, comments, PR closing references
and updated links, then verify branch removal, synchronized main and a clean
working tree. Historical source snapshots can remain unchanged when the current
overview clearly supersedes their old authorization/publication checkpoints.

## Wayfinding

Use one issue labelled wayfinder:map as the map.
Maps and decision children carry only the applicable wayfinder labels, not triage
labels. Refer to issues by their linked titles in human-facing explanations.
Do not work a ticket merely because it is unblocked; check current authorization
and obtain approval for an unapproved stage, experiment, or publication.
Link child tickets as GitHub sub-issues; if unavailable, use a task list
in the map and "Part of #<map>" in each child.
Label children wayfinder:research, wayfinder:prototype,
wayfinder:grilling, or wayfinder:task.

Represent blockers with native GitHub issue dependencies; if unavailable,
put "Blocked by: #<number>" at the top of the ticket.
A ticket is unblocked when every blocker is closed.

Choose the first open, unassigned, unblocked child in map order.
Claim it with gh issue edit <number> --add-assignee @me.
On resolution, comment with the result, close the child, and append
a result summary and link to the map's Decisions-so-far.
