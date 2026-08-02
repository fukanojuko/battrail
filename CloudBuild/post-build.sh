#!/usr/bin/env bash
#
# Unity Build Automation の post-build フックから呼ばれる。
# Build Automation の Advanced settings に CloudBuild/post-build.sh を登録すること。
#
# ビルドマシンに jq / python がある保証がないため、ここでは JSON を組み立てて
# repository_dispatch に投げるだけに留める。Release ノートの実際の編集は
# gh と jq が確実にある GitHub Actions 側 (.github/workflows/build-notes.yml) で行う。
#
# 必要な環境変数 (Build Automation の Advanced settings > Environment variables):
#   GITHUB_RELEASE_TOKEN  このリポジトリの Contents: write のみを持つ fine-grained PAT

# ビルドを落とさないことを最優先にするため -e は付けない
set -uo pipefail

REPO=fukanojuko/battrail
EVENT_TYPE=unity-build-complete
SETTINGS=ProjectSettings/ProjectSettings.asset

log() { echo "post-build: $*"; }

# post-build script は全ビルドで走る。リリースが存在するのは main のみ
if [ "${SCM_BRANCH:-}" != "main" ]; then
  log "branch '${SCM_BRANCH:-unknown}' is not main, skipping"
  exit 0
fi

# Editor が成果物を吐けていないビルドは通知しない
if [ -n "${UNITY_PLAYER_PATH:-}" ] && [ ! -e "$UNITY_PLAYER_PATH" ]; then
  log "no player at '$UNITY_PLAYER_PATH', skipping"
  exit 0
fi

if [ -z "${GITHUB_RELEASE_TOKEN:-}" ]; then
  log "GITHUB_RELEASE_TOKEN is not set, skipping"
  exit 0
fi
echo "::mask-value::$GITHUB_RELEASE_TOKEN"

if ! command -v curl > /dev/null 2>&1; then
  log "curl is unavailable, skipping"
  exit 0
fi

# どの Release に追記するかは、ビルドされた実際のリビジョンの bundleVersion で決まる
version=$(grep -E '^  bundleVersion: ' "$SETTINGS" 2> /dev/null | head -1 | awk '{print $2}')
if [ -z "$version" ]; then
  log "could not read bundleVersion from $SETTINGS, skipping"
  exit 0
fi

json_escape() {
  printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'
}

payload=$(printf '{"event_type":"%s","client_payload":{"version":"%s","build_number":"%s","target":"%s","platform":"%s","revision":"%s"}}' \
  "$EVENT_TYPE" \
  "$(json_escape "$version")" \
  "$(json_escape "${UCB_BUILD_NUMBER:-unknown}")" \
  "$(json_escape "${BUILDCFG_TARGET:-unknown}")" \
  "$(json_escape "${BUILD_PLATFORM:-unknown}")" \
  "$(json_escape "${BUILD_REVISION:-unknown}")")

response=$(curl -sS -w '\n%{http_code}' -X POST \
  -H 'Accept: application/vnd.github+json' \
  -H "Authorization: Bearer $GITHUB_RELEASE_TOKEN" \
  -H 'X-GitHub-Api-Version: 2022-11-28' \
  -d "$payload" \
  "https://api.github.com/repos/$REPO/dispatches" 2>&1)

status=${response##*$'\n'}
body=${response%$'\n'*}

if [ "$status" = "204" ]; then
  log "notified GitHub: v$version build #${UCB_BUILD_NUMBER:-unknown} (${BUILDCFG_TARGET:-unknown})"
else
  log "GitHub returned $status: $body"
fi

exit 0
