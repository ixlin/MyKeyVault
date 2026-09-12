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
cookie is invalid, Harness is not proxied to the requester. The original
DeepSeek Harness installation is not modified, so its package can still be
upgraded independently.

The authenticated `/__sfrost-auth/models` page manages the write-only
`DEEPSEEK_API_KEY` through Harness's loopback-only credential API. The public
browser never receives credential values. Nginx adds the gateway's small model
key entry script to Harness HTML, because upstream intentionally disables its
configuration plane in non-loopback browsers. This integration does not patch
the Harness package and therefore survives normal package upgrades.

The production model catalog is tracked in
`scripts/deepseek-harness-settings.yaml`. It uses the stable
`deepseek-flash` API name for DeepSeek V4.1 Flash, declares native text and
image input, and retains `deepseek-v4-pro` as the second selectable model.
The catalog is stored outside the upstream Harness package, so model changes
and normal Harness upgrades remain independent.
