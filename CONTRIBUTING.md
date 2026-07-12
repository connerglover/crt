# Contributing to CRT

Thanks for your interest in contributing to Conner's Retime Tool! This document
covers how to ask questions, report issues, and submit changes.

By participating, you're expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).
For how project decisions get made, see [GOVERNANCE.md](GOVERNANCE.md).

## Asking Questions

Have a question about using CRT? [Open an issue](https://github.com/connerglover/crt/issues/new?labels=question&template=question.md)
using the Question template. Please compose a clear, specific question — the
more context you give, the faster we can help.

## Reporting Issues

Found a bug, or have a feature request? We want to hear about it.

1. **Search first.** Check [open issues](https://github.com/connerglover/crt/issues)
   to see if it's already been reported or requested. If it has, add a 👍
   reaction and any new details in a comment rather than filing a duplicate.
2. **File one issue per problem.** If you can't find an existing report,
   [open a new issue](https://github.com/connerglover/crt/issues/new?template=proposal.md)
   using the Proposal & Issue Report template. Keep bugs and feature requests
   separate so each can be tracked on its own.
3. **Include details.** For bugs: your OS, CRT version, steps to reproduce, and
   what you expected vs. what happened. The more reproducible the report, the
   faster it can be fixed.

**Security vulnerabilities are the exception** — please don't file those as a
public issue. See [SECURITY.md](SECURITY.md) for how to report them privately.

## Contributing Code

1. Fork the repo and create a branch for your change.
2. Set up a dev environment — see [Running from Source](README.md#-running-from-source)
   in the README.
3. Make your change. CRT doesn't currently have an automated test suite or
   enforced linter/formatter, so please:
   - Manually run the app and exercise the feature/fix you touched before
     opening the PR.
   - Match the existing code style in the file(s) you're editing.
4. Open a pull request against `main` using the PR template. Reference any
   related issue.
5. A maintainer will review your PR — see [GOVERNANCE.md](GOVERNANCE.md) for
   how larger changes get discussed and decided.

For anything beyond a small fix (new features, breaking changes, UI redesigns),
consider opening an issue first to discuss the approach before investing a lot
of time — it saves rework on both sides.

## Thank You!

Your contributions to open source, large or small, make projects like this
possible. Thanks for taking the time to contribute.
