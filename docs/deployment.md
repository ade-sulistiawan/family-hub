# Deploying with Coolify

Family Hub ships a [Dockerfile](../Dockerfile) and [docker-compose.yml](../docker-compose.yml)
(API + Postgres). Coolify deploys this compose file directly — no extra Coolify-specific files
needed.

## Prerequisites

- Coolify installed on the mini server (see [coolify.io/docs](https://coolify.io/docs) for the
  install script). On a Raspberry Pi, use the 64-bit (arm64) OS — ARM support in Coolify is not
  as battle-tested as x86.
- The repo pushed to a Git remote Coolify can reach (GitHub, or a local Git server).
- A Google OAuth client (Client ID/Secret) with an authorized redirect URI of
  `http://<server-ip-or-domain>:8013/signin-google`.
- VAPID keys for web push, generated with `npx web-push generate-vapid-keys`.

## Create the resource

1. In Coolify: **Project → + New Resource → Docker Compose**.
2. Point it at this repo/branch, with `docker-compose.yml` as the compose file path.
3. Coolify lists the two services (`postgres`, `api`) it found in the compose file — leave both
   enabled.

## Environment variables

Set these under the resource's **Environment Variables** tab (Coolify injects them at build/deploy
time, so no `.env` file needs to exist on the server):

| Variable                                    | Notes                                  |
| ------------------------------------------- | -------------------------------------- |
| `POSTGRES_PASSWORD`                         | required — compose fails fast if unset |
| `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` | leave blank to disable Google sign-in  |
| `VAPID_SUBJECT`                             | e.g. `mailto:admin@example.com`        |
| `VAPID_PUBLIC_KEY` / `VAPID_PRIVATE_KEY`    | from `web-push generate-vapid-keys`    |

## Ports

`docker-compose.yml` already publishes the API as `8013:8000` on the host, so it's reachable at
`http://<server-ip>:8013` without any Coolify proxy/domain configuration. If you'd rather put it
behind Coolify's managed Traefik proxy with a domain and HTTPS, remove the `ports:` entry from the
`api` service and set a **Domain** on the resource instead.

## Deploy

Click **Deploy**. Coolify builds the image from the `Dockerfile`, starts `postgres`, waits for its
healthcheck, then starts `api`. EF Core migrations run automatically on API startup — no manual
`dotnet ef database update` step.

The `postgres-data` volume is a named Docker volume, so it survives redeploys.

## Redeploying

Push to the tracked branch and either let Coolify's webhook auto-deploy or click **Redeploy**
manually. This rebuilds the `api` image and restarts both containers; the Postgres data volume is
untouched.

## Troubleshooting

- **Logs**: Coolify's resource view has a Logs tab per service (`api`, `postgres`).
- **Google sign-in missing**: `GOOGLE_CLIENT_ID`/`GOOGLE_CLIENT_SECRET` are unset — the app only
  registers the Google handler when both are present (see `Program.cs`).
- **API can't reach Postgres**: check the `postgres` service is healthy before `api` starts
  (compose's `depends_on: condition: service_healthy` should handle this, but a slow first boot on
  a Pi can still race — just redeploy).
