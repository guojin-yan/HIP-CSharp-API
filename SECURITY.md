# Security policy

## Reporting

Do not file a public issue for a suspected vulnerability. Contact the project owner privately through the repository owner account and include a minimal reproduction, affected version/commit, target framework, operating system, and any relevant native loader details.

Do not send credentials, SSH keys, cloud addresses, or full environment logs in a report. Redact them before sharing.

## Supply-chain boundary

The M0 core package has no ROCm native payload. Runtime packages remain disabled until official provenance, hashes, dependency closure, and component licensing are independently reviewed. Build and cloud scripts must never disable TLS verification or download native binaries from an untrusted mirror.
