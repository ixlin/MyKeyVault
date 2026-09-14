#!/bin/sh
# Publish the per-process DSH browser token only to the authenticated gateway.
set -eu

action=${1:-}
unit=${2:-deepseek-harness.service}
token_dir=${3:-/run/deepseek-harness-launch}
token_file=$token_dir/token

case "$action" in
    prepare)
        # systemd owns RuntimeDirectory and reapplies its mode on start.
        install -d -m 0755 -o deepseek-harness -g deepseek-harness "$token_dir"
        rm -f "$token_file"
        ;;
    capture)
        attempt=0
        token=
        while [ "$attempt" -lt 45 ]; do
            pid=$(systemctl show "$unit" --property=MainPID --value)
            if [ -n "$pid" ] && [ "$pid" != 0 ]; then
                token=$(journalctl -u "$unit" _PID="$pid" -o cat --no-pager -n 80 |
                    sed -nE 's@^dsh web: http://127\.0\.0\.1:[0-9]+/\?token=([A-Za-z0-9_-]{16,128})$@\1@p' |
                    tail -1)
            fi
            if [ -n "$token" ]; then
                umask 027
                temporary=$(mktemp "$token_dir/.token.XXXXXX")
                printf '%s' "$token" > "$temporary"
                chown root:sfrost_publish "$temporary"
                chmod 0640 "$temporary"
                mv -f "$temporary" "$token_file"
                exit 0
            fi
            attempt=$((attempt + 1))
            sleep 1
        done
        echo 'DeepSeek Harness launch token was not emitted within 45 seconds' >&2
        exit 1
        ;;
    *)
        echo 'usage: deepseek-harness-launch-token.sh prepare|capture [unit] [directory]' >&2
        exit 2
        ;;
esac
