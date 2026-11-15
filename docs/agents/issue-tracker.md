# Issue tracker: GitHub

Issues and specs live in GitHub Issues for `enpaze/winland`. Use the `gh` CLI from this clone.

Before the first issue creation of a session, check `gh repo view --json nameWithOwner`. The target must be `enpaze/winland`; resolve a mismatch before writing.

## Conventions

- Create: `gh issue create --title "..." --body-file <file>`.
- Read: `gh issue view <number> --json number,title,body,labels,comments`.
- List: `gh issue list --state open --json number,title,body,labels,comments`, with appropriate label and state filters.
- Update a body: `gh issue edit <number> --body-file <file>`.
- Comment: `gh issue comment <number> --body-file <file>`.
- Apply or remove labels: `gh issue edit <number> --add-label "..."` or `--remove-label "..."`.
- Close: post the explanation as a comment, then run `gh issue close <number>`.

Use a temporary UTF-8 file for multiline bodies.

## Parents and blockers

Create a child with `gh issue create --parent <parent> ...`, or link an existing child with `gh issue edit <parent> --add-sub-issue <child>`.

Record blockers using native GitHub dependencies:
`gh issue edit <child> --add-blocked-by <blocker>`.

Count open blockers with:
`gh api repos/enpaze/winland/issues/<number> --jq .issue_dependencies_summary.blocked_by`.

A ticket is unblocked when every blocker is closed. If native relationships are unavailable, use `Part of #<parent>` and `Blocked by: #<number>` at the top of the child body, plus a task list in the parent.

## Pull requests as a triage surface

**PRs as a request surface: no.**

## Skill operations

“Publish to the issue tracker” means create a GitHub issue.
“Fetch the relevant ticket” means read the GitHub issue, including comments.

## Wayfinding operations

- Map: one issue labelled `wayfinder:map`, containing Notes, Decisions-so-far, and Fog.
- Child tickets: sub-issues labelled `wayfinder:<type>`, where type is `research`, `prototype`, `grilling`, or `task`.
- Frontier: inspect the map's open children in map order; the first unassigned child with no open blockers is next.
- Claim: `gh issue edit <number> --add-assignee "@me"`.
- Resolve: comment with the answer, close the child, then append a gist and link to the map's Decisions-so-far.

Create missing wayfinding labels when using that workflow.
