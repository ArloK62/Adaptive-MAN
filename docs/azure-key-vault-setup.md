# Azure Key Vault Setup

Phase 2 of [DEVELOPMENT_PLAN.md](../DEVELOPMENT_PLAN.md). The Observability API loads its secrets
from a per-environment Key Vault via Managed Identity in deployed environments. No
secrets live in committed files.

## Vaults

One vault per environment to limit blast radius:

| Vault                       | Environment   | Soft-delete | Purge protection |
|-----------------------------|---------------|-------------|------------------|
| `AdaptiveToolsKeyVault`     | Development   | yes         | not enabled      |
| `kv-observability-uat`      | UAT           | yes         | recommended      |
| `kv-observability-prod`     | Production    | yes         | **required**     |

> **Dev deviation from the plan:** Phase 2 currently shares the existing
> `AdaptiveToolsKeyVault` (centralus) for Development to avoid spinning up a
> dedicated vault. All observability secrets are tagged `purpose=adaptive-observability`
> so the operator can tell them apart from other tooling secrets. UAT and Prod must
> still use dedicated `kv-observability-{env}` vaults — the isolation requirement
> stands for any environment storing real PHI/PII-adjacent state. Cut over Dev to
> a dedicated vault if/when the shared vault picks up principals that should not
> see observability secrets.

## Required secrets per vault

| Secret name (in vault)        | Bound config key                      | Purpose                              |
|-------------------------------|---------------------------------------|--------------------------------------|
| `ObservabilityDbConnection`   | `ConnectionStrings:ObservabilityDb`   | Azure SQL connection string          |
| `ApiKeyHashPepper`            | `Observability:ApiKeyHashPepper`      | Pepper for SHA-256 of API keys       |
| `JwtSigningKey`               | `Observability:JwtSigningKey`         | Dashboard auth (Phase 8)             |
| `EncryptionKey`               | `Observability:EncryptionKey`         | Reserved for field-level encryption  |

The mapping is defined in [`backend/src/Observability.Api/Configuration/KeyVaultConfiguration.cs`](../backend/src/Observability.Api/Configuration/KeyVaultConfiguration.cs).
Other secrets follow the standard `Section--Key` → `Section:Key` convention.

In `Development`, secrets fall back to user secrets / `appsettings.Development.json` when
`KeyVault:Uri` is empty. In UAT/Prod the API **fails fast at startup** if `KeyVault:Uri`
is missing or any required secret is unbound.

## Provisioning a fresh environment

The shape below is portal-equivalent; team should pick an IaC tool (Bicep/Terraform — see
DEVELOPMENT_PLAN.md open question 2.1).

1. Create the resource group and vault:
   - Region: same as the App Service (e.g. `eastus`).
   - SKU: `standard`.
   - Permission model: **RBAC** (Azure role-based access control), not access policies.
   - Soft-delete: on. Retention 90 days. Purge protection on for `prod`.
2. Create the App Service with **system-assigned managed identity** enabled.
3. Grant the App Service's managed identity the `Key Vault Secrets User` role on its
   same-environment vault. Do not grant cross-environment access.
4. Add the four required secrets (see table above) using their exact names.
5. Set `KeyVault:Uri` on the App Service:
   - As an app setting: `KeyVault__Uri = https://kv-observability-{env}.vault.azure.net/`.
6. Set `ASPNETCORE_ENVIRONMENT` to `UAT` or `Production` as appropriate.
7. Deploy. On startup the API:
   - Loads the vault into configuration.
   - Validates each required secret resolves to a non-empty string.
   - Logs `Critical` and refuses to start if any are missing in non-Development.

## Identity flow

```
App Service ──(managed identity, MSAL via DefaultAzureCredential)──▶ Key Vault
       │                                                                │
       │   GET /secrets (RBAC: Key Vault Secrets User)                  │
       │◀──────── secret values for ObservabilityDb, pepper, ... ───────┘
       │
       ▼
ASP.NET Core IConfiguration (overlaid on appsettings + env vars)
       │
       ▼
DI: ObservabilityDbContext, ApiKeyHasher, ...
```

`DefaultAzureCredential` resolves to the system-assigned MI in App Service, to the
developer's Azure CLI/VS login locally, and to workload identity in container scenarios.
No code change is needed across environments.

## Local development

Either:

- Leave `KeyVault:Uri` empty in `appsettings.Development.json`. Set
  `Observability:ApiKeyHashPepper` and `ConnectionStrings:ObservabilityDb` directly
  (committed defaults already do so for Docker). This is the default path.
- Or set `KeyVault:Uri` to a personal `kv-observability-dev` and `az login` so
  `DefaultAzureCredential` picks up your CLI token.

## Rotation runbook

> Run rotations in **UAT first**, then Prod. Always have a rollback (the prior secret
> version stays available in Key Vault for `soft-delete-retention` days).

### Rotate `ObservabilityDbConnection` (DB password)

1. In Azure SQL: change the SQL login's password (or rotate the AAD-auth credential).
2. In Key Vault: add a **new version** of `ObservabilityDbConnection` with the updated
   password. Do not delete the prior version.
3. Restart the App Service (or hit the `/health` endpoint after waiting one
   `KeyVault` refresh interval). New connections use the new credential immediately.
4. Verify: `/health` is `200`; ingestion endpoints return `202` for a smoke event.
5. After 24h with no errors, disable the prior secret version.

### Rotate `ApiKeyHashPepper`

> ⚠️ Pepper rotation invalidates **every existing API key** because the stored hash
> includes the pepper. Treat this as a key re-issue event.

1. Schedule a maintenance window. Notify all onboarded apps.
2. Generate a new high-entropy pepper (≥256 bits, base64).
3. Add a new version of `ApiKeyHashPepper` in the target vault.
4. Re-issue every active API key (via the dashboard's admin/apps view, Phase 3+):
   - Create a new key per (Application, Environment, KeyType) using the new pepper.
   - Distribute to onboarded apps; have them deploy the new key.
   - Revoke the old keys after a grace window (default 24h in UAT, 72h in Prod).
5. Verify ingestion: zero `401`s after the grace window expires.
6. Disable the prior pepper version in Key Vault.

### Rotate `JwtSigningKey` (Phase 8 dashboard auth)

1. Add a new version of `JwtSigningKey`.
2. Roll the App Service so new tokens sign with the new key.
3. Existing tokens will be rejected on next refresh — users see one re-login.
4. Disable the prior version after the longest valid token TTL has passed.

### Rotate `EncryptionKey`

Out of scope until field-level encryption ships. When it does: the rotation runbook must
include a backfill step that re-encrypts existing rows with the new key before disabling
the prior version.

## Audit + alerting

- Enable **Key Vault diagnostic logs** to a Log Analytics workspace per environment.
- Alert on:
  - Any `Forbidden` or `Unauthorized` response on the vault.
  - Secret reads from a principal other than the App Service MI.
  - Any `Delete` on `kv-observability-prod`.

## Acceptance checklist (Issue 2.5)

- [x] Step-by-step fresh-env setup
- [x] Rotation runbooks (DB password, hash pepper, JWT key)
- [x] Identity + secret flow documented
- [ ] Bicep/Terraform module committed (open question — pick tool first)
- [ ] Diagnostic logs + alert rules configured (Phase 8 cross-cut)
