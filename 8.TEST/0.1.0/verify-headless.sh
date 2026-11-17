#!/usr/bin/env bash
set -u
log="${1:?usage: verify-headless.sh <godot.log> [manual]}"
mode="${2:-auto}"
expected_patches="${3:-51}"
fail=0

check() {
  if grep -q "$1" "$log"; then
    echo "PASS: $2"
  else
    echo "FAIL: $2"
    fail=1
  fi
}

check "Calling initializer method of type CharacterModIsolation.Plugin" "plate: initializer called"
check "\[CharacterModIsolation\] \[init\] CharacterModIsolation 0.1.0.0" "plate: assembly version 0.1.0.0"
check "\[CharacterModIsolation\] \[patches\] installed=${expected_patches} failed=0" "patches: ${expected_patches} applied, 0 failed"
check "\[CharacterModIsolation\] \[settings\] page registered" "settings: page registered"
check "CharacterModIsolation.texts.*fallbackEntries=0" "text: no missing zh entries"

if grep -Eq "\[CharacterModIsolation\].*(\[ERROR\]|Exception|failed=[1-9])" "$log"; then
  echo "FAIL: own error lines present"
  grep -E "\[CharacterModIsolation\].*(\[ERROR\]|Exception|failed=[1-9])" "$log" | head -10
  fail=1
else
  echo "PASS: no own error lines"
fi

if [ "$mode" = "manual" ]; then
  if grep -q "\[CharacterModIsolation\] \[auto\]" "$log"; then
    echo "FAIL: manual mode ran automatic detection"
    fail=1
  else
    echo "PASS: manual mode skipped automatic detection"
  fi
elif [ "$mode" = "mgr" ]; then
  check "\[CharacterModIsolation\] \[auto\] built\. mods=39 character=1" "auto: MGR detected as character mod"
  check "character=MGRMod kind=Character" "auto: MGRMod owner"
else
  check "\[CharacterModIsolation\] \[auto\] built" "auto: map built"
fi

exit "$fail"
