# SFROST authentication gateway

Node.js application used by the `sfrost.cn` Nginx virtual host. It combines a
password-manager-compatible login gateway, a private Blog, account controls,
and the secure model-key bridge. DeepSeek Harness remains independently bound
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
