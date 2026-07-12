# Governance

CRT is a small, single-maintainer open source project. This document describes
how decisions get made — kept intentionally lightweight to match the size of
the project.

## Maintainer

The project is maintained by **Conner Glover** ([@connerglover](https://github.com/connerglover)),
who has final say over the project's direction, releases, and what gets merged.
See [CODEOWNERS](.github/CODEOWNERS) for review assignment.

## Decision-Making

- **Small, uncontroversial changes** (bug fixes, docs, translations, minor UI
  tweaks) — reviewed and merged directly by the maintainer, or by any
  contributor with merge access, using their own judgment ("lazy consensus").
- **Larger changes** (new features, breaking changes, UI/UX redesigns) —
  should start as an [issue](https://github.com/connerglover/crt/issues/new/choose)
  or draft pull request for discussion before significant work is put in. The
  maintainer makes the final call, weighing feedback from the discussion.
- Disagreements are resolved by discussion first; if no consensus is reached,
  the maintainer decides.

## Contributing Code

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to propose changes. All
contributions are expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Becoming a Maintainer

CRT doesn't currently have a formal process for adding co-maintainers — it's
a hobby project maintained by one person. Contributors who consistently submit
high-quality, well-scoped pull requests may be invited to become a collaborator
at the current maintainer's discretion. If you're interested, mention it on a
pull request or open an issue.

## Releases

Releases are cut by the maintainer by pushing a version tag (e.g. `1.2.2`),
which triggers the [build workflow](.github/workflows/build.yml) to build and
publish binaries for Windows, macOS, and Linux. There's no fixed release
schedule — releases happen when there's something worth shipping.

## Changes to This Document

Changes to governance are proposed via pull request and decided by the
maintainer, same as any other change.
