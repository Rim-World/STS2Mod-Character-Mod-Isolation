#!/usr/bin/env bash
set -euo pipefail

log=${1:?stdout log path required}
[[ -f "$log" ]] || { echo "FAIL log not found: $log"; exit 1; }

fail=0
need() {
  if ! rg -q "$1" "$log"; then
    echo "FAIL missing: $1"
    fail=1
  fi
}

need "splash-audit .*attackCandidates=[1-9][0-9]* isolated=0"
need "splash-inject count=5"
need "splash-play "
need "combat-generation-enter .*isolated=0"
need "combat-generation-result .*isolated=0"
need "splash-select-candidates .*isolated=0"
need "cards-source removed=11 kept="
need "Run completed successfully with seed=1"

if rg -n -P "splash-audit .*isolated=[1-9]|splash-select-candidates .*isolated=[1-9]|combat-generation-.*isolated=[1-9]" "$log" >/tmp/cmi-splash-bad 2>/dev/null; then
  echo "FAIL bad Splash evidence:"
  cat /tmp/cmi-splash-bad
  fail=1
fi

if [[ "$fail" -eq 0 ]]; then
  echo "PASS Splash evidence"
else
  exit 1
fi
