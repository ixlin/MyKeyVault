# SFROST authentication gateway

Node.js application used by the `sfrost.cn` Nginx virtual host. It combines a
password-manager-compatible login gateway, a private Blog and knowledge base,
account controls, and the secure model-key bridge. DeepSeek Harness remains independently bound
to `127.0.0.1:3080`; this application binds to `127.0.0.1:3081`.

Required environment variables:

- `SFROST_AUTH_USERNAME`
- `SFROST_AUTH_PASSWORD_SCRYPT` in `scrypt$<salt hex>$<64-byte hash hex>` format
- `SFROST_AUTH_COOKIE_SECRET` containing at least 32 random bytes as hex

Production stores them in `/etc/sfrost-auth-gateway.env` with mode `0600`.
Neither plaintext passwords nor session secrets belong in this repository.

After the first start, the gateway copies the password hash into its protected
systemd state directory. An authenticated administrator can then change the
password at `/__sfrost-auth/account`. Password changes are written atomically
and increment the credential revision, invalidating sessions on other devices.
The initial environment password remains a recovery seed only when no state
file exists.

The Blog stores posts and tags in the local PostgreSQL service, using the
dedicated `sfrost_blog` database and same-named peer-authenticated system role.
Its schema is namespaced with `sfrost_blog_*` tables. No MyKeyVault application
tables or database credentials are shared. The service therefore uses the same
database technology and server as MyKeyVault while retaining an independent
security boundary.

Knowledge-base metadata uses `sfrost_kb_docs` in the same portal database.
Original files are held under the gateway's private systemd state directory;
HTML is displayed through a CSP-sandboxed authenticated preview and is never
executed as a trusted portal page. Word and other binary artifacts remain
authenticated downloads.

Harness publishes artifacts without sudo through a setgid inbox shared only by
the `deepseek-harness` and `sfrost_blog` services. The `sfrost-publish` command
copies a supported file into that inbox and returns its authenticated
`https://sfrost.cn/kb/d/<uuid>` URL. The gateway validates and imports the
manifest on first access. Harness receives no database credentials and cannot
write the production file store. The persistent DSH skill in
`scripts/deepseek-harness-publish-artifacts.md` instructs agents to use this
route instead of `localhost` links. The publisher rejects symbolic links and,
by default, only accepts source files below `/var/lib/deepseek-harness` or
`/srv/sfrost-workspaces`; deployments can narrow or extend that list with
`SFROST_KB_SOURCE_ROOTS`.

Vectorization is deliberately not simulated. Documents are recorded as not
vectorized; a real embedding/indexing worker can later update that state after
successful ingestion.

The Nginx `auth_request` check fails closed: if the gateway is unavailable or a
cookie is invalid, Harness is not proxied to the requester. The upstream
DeepSeek Harness package is not patched. Its current production version is
`@deepseek-ai/dsh@0.1.5-rc.1`, pinned with its dependency lockfile in
`scripts/deepseek-harness-runtime/`. The systemd unit points at
`/opt/deepseek-harness-current`, a symlink to the installed version; the
previous package and a consistent pre-upgrade state backup are retained for
rollback.

Before each start, `scripts/deepseek-harness-profile-links.sh` checks the five
core `@deepseek-ai` module links in the Web profile and points them at the
current installation. An old profile-local `dsh-tools` link causes tool calls
to fail with `Cannot read properties of undefined (reading 'prepare')` even
when the Web UI and model replies still work. The script refuses to replace a
real profile package directory.

The Nginx attachment endpoint `/api/session/uploadFileBinary` allows a 100 MiB
body only after portal authentication. Other routes keep their existing body
limits. The default 1 MiB limit previously rejected normal PPTX uploads with
HTTP 413 before Harness could inspect the file.

Since DSH 0.1.2, the Web app also requires its own authority-bound browser
cookie. The systemd launch-token script places the current per-process token in
`/run/deepseek-harness-launch/token` (`root:sfrost_publish`, `0640`). The
authenticated gateway exchanges it over loopback using the public Host and
forwards only DSH's signed, HttpOnly, SameSite=Strict cookie with `Secure` added.
No launch token is sent to a browser URL. Nginx uses this bridge only when an
authenticated `/harness` request receives a DSH 401. The same gateway bridges
the new `/api/credentials/{describe,set,unset}` RPCs, with the previous RPC
shape retained for rollback. The old third-party `dsh-file-upload` profile
bundle must not be mounted: this DSH version already includes the official
`@deepseek-ai/dsh-client-file-upload` bundle. The desired new profile is in
`scripts/deepseek-harness-web-profile.json`.

The authenticated `/__sfrost-auth/models` page manages the write-only
`DEEPSEEK_API_KEY` through Harness's loopback-only credential API. The public
browser never receives credential values. Nginx adds the gateway's small model
key entry script to Harness HTML, because upstream intentionally disables its
configuration plane in non-loopback browsers. This integration does not patch
the Harness package. Future upgrades still require testing the browser-auth,
credential RPC, third-party bundle, and old-session compatibility contracts
before changing the symlink.

The production model catalog is tracked in
`scripts/deepseek-harness-settings.yaml`. It uses the stable
`deepseek-flash` API name for DeepSeek V4.1 Flash, declares native text and
image input, and retains `deepseek-v4-pro` as the second selectable model.
The catalog is stored outside the upstream Harness package, so model changes
and normal Harness upgrades remain independent.
