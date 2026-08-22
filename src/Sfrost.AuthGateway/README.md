# SFROST authentication gateway

Small dependency-free Node.js gateway used by the `sfrost.cn` Nginx virtual
host. It replaces browser Basic Auth with a password-manager-compatible login
form and a signed, HTTPS-only session cookie. DeepSeek Harness remains bound to
`127.0.0.1:3080`; this gateway binds to `127.0.0.1:3081`.

Required environment variables:

- `SFROST_AUTH_USERNAME`
- `SFROST_AUTH_PASSWORD_SCRYPT` in `scrypt$<salt hex>$<64-byte hash hex>` format
- `SFROST_AUTH_COOKIE_SECRET` containing at least 32 random bytes as hex

Production stores them in `/etc/sfrost-auth-gateway.env` with mode `0600`.
Neither plaintext passwords nor session secrets belong in this repository.

The Nginx `auth_request` check fails closed: if the gateway is unavailable or a
cookie is invalid, Harness is not proxied to the requester. The original
DeepSeek Harness installation is not modified, so its package can still be
upgraded independently.
