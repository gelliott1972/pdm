# App status

Updated 21 Oct 2026. Working to the revised brief (Cognito, DynamoDB, local page).

## Done

| Item | Result |
| --- | --- |
| Brief read | `docs\briefs\app-mvp.md` (revised version) |
| .NET 8 SDK | Installed by Grant |
| Organisation choice | Child account under Candelic, not a new organisation (Grant agreed) |
| PDM account | Created: `PDM`, `588966315587`, root email `grant+pdm@candelic.com` |
| Hong Kong region | Enabled in the PDM account (22 Oct) |
| Grant's admin access | `AdministratorAccess` on PDM assigned to `candelic-admin`; profile `pdm-admin` added (reuses the `candelic-admin-sso` session), tested |
| Bucket | `pdm-apex`, `ap-east-1`: versioning on, all public access blocked, SSE-S3 encryption with bucket key |
| DynamoDB | `pdm-files` (key `fileId`, a GUID; index `by-path` on `path`) and `pdm-partnos` (key `partNo`, holds `fileId` + configuration). On-demand billing, point-in-time recovery on. GUID key is Grant's idea: renames change one field, references are by `fileId`. Each S3 object also carries its `fileId` as metadata so a rebuild keeps the IDs. PITR kept (Grant). |
| Cognito login pool | `pdm-users` (`ap-east-1_8pzjtaAIo`): email login, admin-created users only, MFA off, password 12+ chars, deletion protection on. App client `pdm-app` (`7873328kl19l8fphufb1e5mldb`), no secret, SRP login, 30-day refresh. |
| Cognito identity pool | `pdm` (`ap-east-1:e6866600-7344-486c-b450-f4aca45bdb49`), logged-in only. Role from token; default `pdm-engineer`. |
| Roles | `pdm-engineer`: list and read the vault, upload except `_Shared/*`, `*/_Shared/*`, `APEX/GNRIC/*`; read and write both tables. `pdm-shared-editor`: same plus upload to shared folders. No delete in either. Credentials last 1 hour. |
| Group | `shared-editors` → `pdm-shared-editor`. Grant and James are members (Grant: shared files still default to read-only and unticked in the app). |
| Users | Grant created (temporary password emailed), in `shared-editors`. |

## Next

1. James's Cognito user (asking Grant when to send the invite).
4. Engine from a test command line.
5. Start `docs\setup-machine.md`.

## Open questions

- Bucket name `candelic-pdm-apex`: OK?
- Cognito users: email addresses for Grant and James.

## Notes

- `OrganizationAccountAccessRole` in the new account refused `candelic-admin` (AccessDenied, retried). Not pursued; going via Identity Center instead.
- Trusted access for Account Management is not enabled in the organisation; not changing it.
- Identity Center assignment of `AdministratorAccess` on PDM to `candelic-admin`: Grant approved; it **fails repeatedly** ("Obtaining permissions to manage your AWS account ... is taking longer than usual"), 7 tries from 14:2x to 18:51. Account shows ACTIVE in the organisation. Both routes in (step-in role and SSO) fail, so the new account looks stuck in setup on AWS's side.
- **Cause found (22 Oct):** the Candelic Identity Center and the `candelic-admin` profile's default region are both `ap-east-1`, which is opt-in. A new account hasn't opted in, so Hong Kong refuses to issue access to it. Fix: step in via a non-opt-in region (`--region ap-southeast-2`), enable `ap-east-1` in the account, then retry the SSO assignment. **Lesson for any future account in this organisation: enable `ap-east-1` first.**
- Grant has set a root password and MFA on the PDM account (console sign-in only, no access keys).
- Bucket name: `pdm-apex` (Grant). Cognito users: Grant and James (emails from Grant; kept out of the repo).

## Grant to do by hand

- Approve the permission set assignment.
- Run the SSO logins when asked.
