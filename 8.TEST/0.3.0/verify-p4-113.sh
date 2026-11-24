#!/usr/bin/env bash
# 0.113.1 P4 verifier. The 0.111.1 verifier stays in verify-p4.sh.
set -u

root="$(cd "$(dirname "$0")/../.." && pwd)"
dir="$root/8.TEST/0.3.0"
state=0
checks=0

check() {
  local file="$1" pattern="$2" label="$3"
  checks=$((checks + 1))
  if rg -q -- "$pattern" "$file"; then
    printf 'PASS %s\n' "$label"
  else
    printf 'FAIL %s\n' "$label"
    state=1
  fi
}

deny() {
  local file="$1" pattern="$2" label="$3"
  checks=$((checks + 1))
  if rg -q -- "$pattern" "$file"; then
    printf 'FAIL %s\n' "$label"
    state=1
  else
    printf 'PASS %s\n' "$label"
  fi
}

mgr="$dir/20261125-0018-1131-p4-mgr.stdout"
watcher="$dir/20261125-0028-1131-p4-watcher.stdout"
vanilla="$dir/20261125-0038-1131-p4-vanilla.stdout"
cache="$dir/20261125-0048-1131-p4-cache3.stdout"
manual="$dir/20261125-0058-1131-p4-manual-mgr.stdout"

check "$mgr" 'p4 run character=MGR_MOD_CHARACTER_MGR_CHARACTER' 'MGR run start'
check "$mgr" '\[context\] active=True reason=mod-character-other source=Auto characterOwner=MGRMod protected=\[MGRMod\] isolated=\[Watcher\]' 'MGR context'
check "$mgr" 'p4 splash-audit .*attackCandidates=197 isolated=0' 'MGR Splash candidates'
check "$mgr" 'p4 kaleidoscope .*pools=7 all=8 removed=1 raw=False allowed=True' 'MGR Kaleidoscope'
check "$mgr" 'p4 orobas options=7 seaGlassCharacters=\[DEFECT,FOOL,IRONCLAD,MGR_MOD_CHARACTER_MGR_CHARACTER,NECROBINDER,REGENT,SILENT\]' 'MGR Orobas'
check "$mgr" 'p4 splash-select-candidates count=3 isolated=0' 'MGR Splash select'
check "$mgr" 'p4 color-event owner=MGR_MOD_CHARACTER_MGR_CHARACTER allowed=True' 'MGR Colorful event'
deny "$mgr" 'p4 color-event .*keys=\[[^]]*(MGR|Watcher)' 'MGR Colorful options exclude mods'
check "$mgr" 'category=cards-source removed=8 kept=42' 'MGR card source filtering'

check "$watcher" 'p4 run character=WATCHER' 'Watcher run start'
check "$watcher" '\[context\] active=True reason=mod-character-other source=Auto characterOwner=Watcher protected=\[Watcher\] isolated=\[MGRMod\]' 'Watcher context'
check "$watcher" 'p4 splash-audit .*attackCandidates=197 isolated=0' 'Watcher Splash candidates'
check "$watcher" 'p4 kaleidoscope .*pools=7 all=8 removed=1 raw=False allowed=True' 'Watcher Kaleidoscope'
check "$watcher" 'p4 orobas options=7 seaGlassCharacters=\[DEFECT,FOOL,IRONCLAD,NECROBINDER,REGENT,SILENT,WATCHER\]' 'Watcher Orobas'
check "$watcher" 'p4 splash-play owner=WATCHER' 'Watcher Splash actual play'
check "$watcher" 'p4 splash-select-candidates count=3 isolated=0' 'Watcher Splash select'
check "$watcher" 'p4 color-event owner=WATCHER allowed=True' 'Watcher Colorful event'
deny "$watcher" 'p4 color-event .*keys=\[[^]]*(MGR|Watcher)' 'Watcher Colorful options exclude mods'
check "$watcher" 'category=cards-source removed=3 kept=47' 'Watcher card source filtering'

check "$vanilla" 'p4 run character=REGENT' 'Vanilla run start'
check "$vanilla" '\[context\] active=True reason=vanilla-run source=Auto characterOwner=vanilla protected=\[\] isolated=\[MGRMod,Watcher\]' 'Vanilla context'
check "$vanilla" 'p4 splash-audit .*attackCandidates=165 isolated=0' 'Vanilla Splash candidates'
check "$vanilla" 'p4 kaleidoscope .*pools=6 all=8 removed=2 raw=False allowed=True' 'Vanilla Kaleidoscope'
check "$vanilla" 'p4 orobas options=6 seaGlassCharacters=\[DEFECT,FOOL,IRONCLAD,NECROBINDER,REGENT,SILENT\]' 'Vanilla Orobas'
check "$vanilla" 'p4 splash-play owner=REGENT' 'Vanilla Splash actual play'
check "$vanilla" 'p4 splash-select-candidates count=3 isolated=0' 'Vanilla Splash select'
check "$vanilla" 'p4 color-event owner=REGENT allowed=True' 'Vanilla Colorful event'
deny "$vanilla" 'p4 color-event .*keys=\[[^]]*(MGR|Watcher)' 'Vanilla Colorful options exclude mods'
check "$vanilla" 'category=cards-source removed=11 kept=39' 'Vanilla card source filtering'

check "$cache" 'p4-cache phase=run-a-vanilla-auto active=True reason=vanilla-run source=Auto' 'Cache run A context'
check "$cache" 'p4-cache-pool phase=run-a-vanilla-auto id=MGR_MOD_CARD_POOL_MGR_CARD_POOL total=0' 'Cache run A MGR pool'
check "$cache" 'p4-cache-pool phase=run-a-vanilla-auto id=WATCHER_CARD_POOL total=0' 'Cache run A Watcher pool'
check "$cache" 'p4-cache phase=run-b-vanilla-manual-watcher active=True reason=vanilla-run source=Manual' 'Cache run B context'
check "$cache" 'p4-cache-pool phase=run-b-vanilla-manual-watcher id=MGR_MOD_CARD_POOL_MGR_CARD_POOL total=91' 'Cache run B MGR pool'
check "$cache" 'p4-cache-pool phase=run-b-vanilla-manual-watcher id=WATCHER_CARD_POOL total=0' 'Cache run B Watcher pool'
check "$cache" 'p4-cache phase=run-c-mgr-auto active=True reason=mod-character-other source=Auto owner=MGRMod isolated=\[Watcher\]' 'Cache run C context'
check "$cache" 'p4-cache-pool phase=run-c-mgr-auto id=MGR_MOD_CARD_POOL_MGR_CARD_POOL total=91' 'Cache run C MGR pool'
check "$cache" 'p4-cache-pool phase=run-c-mgr-auto id=WATCHER_CARD_POOL total=0' 'Cache run C Watcher pool'
check "$cache" 'p4-cache-driver complete' 'Cache driver completion'
check "$cache" 'p4-cache settings restored source=Auto' 'Cache settings restore'

check "$manual" 'p4-cache phase=manual-watcher-mgr active=True reason=mod-character-other source=Manual owner=MGRMod isolated=\[Watcher\]' 'Manual unselected current context'
check "$manual" 'p4-cache-pool phase=manual-watcher-mgr id=MGR_MOD_CARD_POOL_MGR_CARD_POOL total=91' 'Manual unselected MGR pool'
check "$manual" 'p4-cache-pool phase=manual-watcher-mgr id=WATCHER_CARD_POOL total=0' 'Manual unselected Watcher pool'
check "$manual" 'p4-cache settings restored source=Auto' 'Manual settings restore'

printf 'CHECKS %d\n' "$checks"
if [[ "$state" -eq 0 ]]; then
  printf 'RESULT PASS\n'
else
  printf 'RESULT FAIL\n'
fi
exit "$state"
