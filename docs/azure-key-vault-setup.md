# Azure Key Vault Setup

Phase 2 work. Placeholder until provisioning runs.

## Vaults

- `kv-observability-dev`
- `kv-observability-uat`
- `kv-observability-prod` (soft-delete + purge protection on)

## Required secrets per vault

| Secret name              | Purpose                                       |
|--------------------------|-----------------------------------------------|
| `ObservabilityDbConnection` | Azure SQL connection string                |
| `JwtSigningKey`          | Dashboard auth (Phase 8)                      |
| `ApiKeyHashPepper`       | Pepper for SHA-256 of API keys                |
| `EncryptionKey`          | Reserved for future field-level encryption    |

In `Development`, fall back to user secrets / `appsettings.Development.json`. In UAT/Prod, **fail-fast at startup** if any required secret is missing.

## Identity & access

- System-assigned managed identity per App Service.
- Each MI granted `get`/`list` on its same-environment vault only.

## Rotation runbook

TODO — Phase 2.5. At minimum: DB password and `ApiKeyHashPepper` (note: rotating pepper invalidates existing keys; design migration before exercising).
