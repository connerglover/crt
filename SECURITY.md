# Security Policy

## Supported Versions

CRT is a desktop application distributed as standalone binaries (see [Releases](https://github.com/connerglover/crt/releases)). Only the **latest release** is supported — there are no long-term-support branches. If a vulnerability is found, the fix will ship in the next release, and users should always update to the latest version.

## Reporting a Vulnerability

Please **do not** report security vulnerabilities through public GitHub issues.

Instead, use GitHub's private vulnerability reporting:

1. Go to the [Security tab](https://github.com/connerglover/crt/security) of this repository.
2. Click **Report a vulnerability**.
3. Describe the issue, how to reproduce it, and its potential impact.

This opens a private conversation with the maintainer that isn't visible to the public until it's resolved.

CRT is maintained by a single volunteer maintainer, so response times are best-effort — expect an initial reply within about a week. Confirmed vulnerabilities will be fixed and released as soon as reasonably possible, with credit given to the reporter (unless anonymity is requested).

## Scope

CRT is an offline desktop tool with no network features beyond an optional check against the GitHub Releases API for updates. Relevant reports include (but aren't limited to):

- Vulnerable dependencies (see [requirements.txt](requirements.txt))
- Unsafe file parsing (session/time files, settings)
- Issues in the update-check flow

Reports about the app's UI/UX not working as intended, or feature requests, should go through a normal [issue](https://github.com/connerglover/crt/issues/new/choose) instead.
