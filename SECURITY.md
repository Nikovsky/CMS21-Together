# Security Policy

CMS21 Together — Community Edition is an **unofficial** multiplayer mod for
Car Mechanic Simulator 2021. We welcome responsible reports of security and
privacy issues affecting this repository.

## Supported versions

There is currently **no guaranteed security-support or patch-release schedule**.

| Code/release | Security handling |
| --- | --- |
| `MainMod` development branch | Best-effort assessment and fixes |
| Published community releases | Check each release's notes; support is not guaranteed |
| Original upstream mod and third-party fixes | Outside this fork's maintenance scope |

An unmerged pull request does not count as a supported release.

## Privately report a vulnerability

**Do not publish exploit details, credentials, user data, or a working proof
of concept in a public GitHub issue, discussion, or pull request.**

1. If GitHub's **Private vulnerability reporting** is enabled for this
   repository, use the **Security → Report a vulnerability** flow.
2. Otherwise contact the repository maintainer through an already available
   **private** communication channel. Do not assume an email address exists.
3. If no private channel is available, open a public issue containing **only**
   a request for a private reporting channel — no vulnerability details or
   sensitive attachments.

Include privately, where possible:

- A concise description and potential impact.
- Affected commit, branch, or release and your game/mod/loader versions.
- Minimal reproduction steps and relevant configuration.
- Sanitized logs or proof of concept; redact Steam IDs, IP addresses,
  access tokens, save data, and other personal or sensitive information.
- Whether the issue requires a malicious host, malicious client, malformed
  packet, crafted save file, or local access.

Please avoid testing against other players or systems without permission.

## What we consider security-relevant

Examples include untrusted network input, unsafe deserialization or file
paths, remote or local code execution, unauthorized file access, sensitive
information exposure, authentication/session misuse, and exploitable
save-data corruption.

Ordinary gameplay/UI defects without a security consequence should be
reported through normal GitHub issues. For a suspected vulnerability,
prefer private reporting even if its impact is not yet confirmed.

## Triage and disclosure

Maintainers will evaluate reports on a best-effort basis; no acknowledgment,
resolution, or release deadline is promised. They may request additional
information, prepare a fix and regression tests, and coordinate public
disclosure after a mitigation is available.

Please give maintainers a reasonable opportunity to investigate before
publicly sharing technical exploitation details.

## Scope and dependencies

This policy covers code maintained in **this fork**, not vulnerabilities in
Car Mechanic Simulator 2021, Steam, MelonLoader, the operating system, or
third-party projects. Report upstream product vulnerabilities to their
respective maintainers. Do not include proprietary game assemblies,
credentials, or personal save files in vulnerability reports.

See `AGENTS.md` for contribution and code-review expectations and
`LICENSE` for this repository's license terms.
