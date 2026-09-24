#!/bin/sh
# Keep the web profile's core module links on the same DSH installation.
set -eu

profile_scope=${1:-/var/lib/deepseek-harness/.dsh/profiles/web/node_modules/@deepseek-ai}
runtime_scope=${2:-/opt/deepseek-harness-current/node_modules/@deepseek-ai}

test -d "$profile_scope"
test -d "$runtime_scope"

for package in cordis dsh-credentials dsh-fs dsh-tools schemastery; do
    link="$profile_scope/$package"
    target="$runtime_scope/$package"
    test -d "$target"
    if [ -e "$link" ] && [ ! -L "$link" ]; then
        echo "Refusing to replace a non-symlink profile module: $link" >&2
        exit 1
    fi
    if [ "$(readlink "$link" 2>/dev/null || true)" = "$target" ]; then
        continue
    fi
    ln -sfn "$target" "$link"
    chown -h deepseek-harness:deepseek-harness "$link"
done
