# Security Policy

DeltaZulu OÜ publishes open-source tools for research, operations, learning, and practical use by the community.

These projects are provided as free and open-source software under their respective licenses, usually GPL-3.0 unless stated otherwise in the repository. They are provided **as is**, without paid support, warranty, service-level commitment, or guarantee of fitness for any particular environment.

This file exists to make security communication clear. It is not a commercial support agreement.

## Reporting a Security Vulnerability

If you believe you have found a security vulnerability in one of these projects, please report it privately before publishing details.

Preferred contact:

- Email: `security@deltazulu.ee`

If the repository has GitHub private vulnerability reporting enabled, you may also use GitHub’s “Report a vulnerability” feature.

Please include as much relevant information as possible:

- The affected repository and version, commit, or release.
- A clear description of the issue.
- Steps to reproduce the issue.
- The expected and actual behavior.
- Any proof-of-concept code, logs, screenshots, or packet captures, if relevant.
- The security impact you believe the issue has.
- Whether the issue is already public or known to be exploited.

Do not include unrelated personal data, production secrets, credentials, private keys, customer data, or third-party confidential material in your report.

## What Counts as a Security Vulnerability

Examples of issues that may be treated as security vulnerabilities include:

- Remote code execution.
- Privilege escalation.
- Authentication or authorization bypass.
- Insecure handling of secrets, credentials, or private keys.
- Unsafe default behavior that creates a material security risk.
- Injection vulnerabilities.
- Path traversal or arbitrary file write/read.
- Denial-of-service issues with realistic operational impact.
- Security boundary violations.
- Supply-chain or build/release integrity issues.

The following are normally not handled through the private security process unless they create a direct security impact:

- General bugs.
- Feature requests.
- Compatibility issues.
- Documentation errors.
- Hardening suggestions.
- Questions about deployment or configuration.
- Requests for operational help.
- Issues caused by unsupported modifications, unsafe local configuration, or misuse of the tool.

Those should usually be opened as normal GitHub issues or discussions, if enabled.

## Support Boundaries

DeltaZulu OÜ does not provide paid support, managed services, consulting, emergency response, or guaranteed maintenance for these open-source projects.

Submitting a vulnerability report does not create a support relationship, service obligation, confidentiality agreement, or guarantee that a fix will be produced.

We will make a reasonable effort to review valid security reports, but response time may vary. Some projects may be experimental, inactive, incomplete, or maintained only as time allows.

Users are responsible for assessing whether the software is suitable for their own environment, threat model, compliance obligations, and operational risk.

## Coordinated Disclosure

Please give a reasonable opportunity to assess and address a reported vulnerability before public disclosure.

A typical process may include:

1. Acknowledgement of the report, where practical.
2. Initial assessment of whether the issue is valid and security-relevant.
3. Development of a fix, mitigation, documentation update, or advisory, if appropriate.
4. Public disclosure through a GitHub advisory, release notes, commit history, or issue, depending on severity and project status.

No fixed disclosure timeline is promised. For serious vulnerabilities, a 90-day coordinated disclosure window is generally reasonable, but this may vary depending on complexity, exploitability, project activity, and maintainer availability.

If a vulnerability is already being exploited publicly, or if details are already public, disclosure and remediation may need to happen faster and with less coordination.

## Safe Harbor

Good-faith security research is welcome when it is lawful, limited, and does not harm others.

When researching these projects, please:

- Do not attack systems you do not own or have explicit permission to test.
- Do not access, modify, delete, or exfiltrate data that does not belong to you.
- Do not disrupt third-party services.
- Do not use social engineering, phishing, spam, or physical attacks.
- Do not publish exploit details before giving reasonable notice.
- Stop testing and report the issue if you encounter sensitive data or unintended access.

This policy does not authorize testing against third-party deployments, users, networks, services, or infrastructure. Permission can only be granted by the owner of the system being tested.

## Security Fixes and Releases

Security fixes may be handled in different ways depending on the project:

- A patch commit.
- A new tagged release.
- A GitHub security advisory.
- A documentation update.
- A configuration recommendation.
- A decision not to fix, if the behavior is intentional, out of scope, not reproducible, or not security-relevant.

Older versions may not receive backported fixes. Unless a repository states otherwise, only the latest public version should be considered for security updates.

## No Bug Bounty

There is no bug bounty program.

Reports are appreciated, but no monetary reward, compensation, swag, public credit, CVE assignment, or other benefit is promised. Credit may be given where appropriate and with the reporter’s consent.

## Public Issues

Please do not open a public GitHub issue for a suspected vulnerability until the issue has been assessed.

Public issues are appropriate for normal bugs, questions, documentation improvements, feature requests, and non-sensitive hardening suggestions.

If you are unsure whether something is security-sensitive, report it privately first.

## License and Warranty

The software is provided under the license stated in the repository.

Unless otherwise stated, DeltaZulu OÜ projects are distributed without warranty and without any implied guarantee of merchantability, fitness for a particular purpose, security, correctness, availability, or non-infringement.

Use of the software is at your own risk.
