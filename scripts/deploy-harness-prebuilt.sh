#!/usr/bin/env bash
# Run only after independently building and validating the Linux x86_64 runtime.
set -euo pipefail
runtime=/opt/deepseek-harness-0.2.0-rc.2-prebuilt
backup=${1:-/var/backups/sfrost/preupgrade-0.2.0-rc.2-20261003}
[[ "$backup" =~ ^/var/backups/sfrost/preupgrade-0\.2\.0-rc\.2-20261003(-retry[0-9]+)?$ ]]
state=/var/lib/deepseek-harness/.dsh
node=/opt/node-v22.23.3/bin/node
test "$EUID" -eq 0
test "$(readlink -f /opt/deepseek-harness-current)" = /opt/deepseek-harness-0.1.5-rc.1
test -s "$backup/workspaces.tgz"
test -s "$backup/sfrost_blog.dump"
test -s "$backup/deepseek-harness.service"
test -f /tmp/deepseek-harness.service
test -d "$state"
test "$($node "$runtime/node_modules/@deepseek-ai/dsh/lib/bin.js" --version)" = 0.2.0-rc.2
test ! -e "$backup/harness-state.tgz"
$node /tmp/harness-readiness-check.mjs 3080 deepseek-harness.service --active-only

switched=0
rollback() {
  status=$?
  if [ "$status" -ne 0 ]; then
    if [ "$switched" -eq 1 ]; then
      systemctl stop deepseek-harness || true
      if [ -s "$backup/harness-state.tgz" ]; then
        # Preserve all failed-upgrade state instead of discarding possible writes.
        mv "$state" "$backup/failed-upgrade-state"
        tar -xzf "$backup/harness-state.tgz" -C /
      fi
      ln -sfn /opt/deepseek-harness-0.1.5-rc.1 /opt/deepseek-harness-current
      install -m 644 "$backup/deepseek-harness.service" /etc/systemd/system/deepseek-harness.service
      systemctl daemon-reload
    fi
    systemctl start deepseek-harness || true
    echo 'Upgrade failed; prior runtime restored, failed state retained in the private backup.' >&2
  fi
}
trap rollback EXIT
find /var/lib/deepseek-harness/TopSecret /var/lib/deepseek-harness/Normal /srv/sfrost-workspaces /var/lib/sfrost-auth-gateway/kb-files /var/lib/sfrost-auth-gateway/blog-media -type f -print0 | sort -z | xargs -0 sha256sum > "$backup/content.sha256"
chmod 600 "$backup/content.sha256"
systemctl stop deepseek-harness
nice -n 19 tar -czf "$backup/harness-state.tgz" -C / var/lib/deepseek-harness/.dsh
chmod 600 "$backup/harness-state.tgz"
tar -tzf "$backup/harness-state.tgz" >/dev/null
switched=1
install -m 644 /tmp/deepseek-harness.service /etc/systemd/system/deepseek-harness.service
ln -s "$runtime" /opt/deepseek-harness-current.next
mv -Tf /opt/deepseek-harness-current.next /opt/deepseek-harness-current
systemctl daemon-reload
systemctl start deepseek-harness
$node /tmp/harness-readiness-check.mjs 3080 deepseek-harness.service
$node /tmp/sfrost-maintenance-20261003/scripts/sfrost-health-check.mjs
sha256sum --check --status "$backup/content.sha256"
echo 'PASS production upgrade, authentication, existing content hashes and session compatibility.'
