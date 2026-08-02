#!/usr/bin/env bash
#
# Unity Build Automation の post-build フックから呼ばれる。
# Build Automation の Advanced settings に CloudBuild/post-build.sh を登録すること。
#
# post-build は「Editor 終了後・Build Automation が成果物をアップロードする前」に走る。
# つまり Unity 側のダウンロード URL はまだ無いが、ビルド成果物はこのマシン上に実体が
# あるので、ここで zip して GitHub Release のアセットとして直接添付する。
#
# 添付が終わったら repository_dispatch を投げ、Release 本文の表への追記は
# .github/workflows/build-notes.yml に任せる。本文の read-modify-write は
# 複数プラットフォームが同時に終わると競合するため、直列化できる Actions 側で行う。
#
# 必要な環境変数 (Build Automation の Advanced settings > Environment variables):
#   GITHUB_RELEASE_TOKEN  このリポジトリの Contents: write のみを持つ fine-grained PAT

# ビルドを落とさないことを最優先にするため -e は付けない
set -uo pipefail

REPO=fukanojuko/battrail
EVENT_TYPE=unity-build-complete
SETTINGS=ProjectSettings/ProjectSettings.asset
MAX_ASSET_BYTES=2147483648 # GitHub の Release アセット上限は 1 ファイル 2 GiB

log() { echo "post-build: $*"; }

# post-build script は全ビルドで走る。Release が存在するのは main のみ
if [ "${SCM_BRANCH:-}" != "main" ]; then
  log "branch '${SCM_BRANCH:-unknown}' is not main, skipping"
  exit 0
fi

if [ -z "${GITHUB_RELEASE_TOKEN:-}" ]; then
  log "GITHUB_RELEASE_TOKEN is not set, skipping"
  exit 0
fi
echo "::mask-value::$GITHUB_RELEASE_TOKEN"

for cmd in curl python3; do
  if ! command -v "$cmd" > /dev/null 2>&1; then
    log "$cmd is unavailable, skipping"
    exit 0
  fi
done

# 成果物の場所。OUTPUT_DIRECTORY が本命で、無ければ player の親を使う
build_dir=${OUTPUT_DIRECTORY:-}
if [ -z "$build_dir" ] && [ -n "${UNITY_PLAYER_PATH:-}" ]; then
  build_dir=$(dirname "$UNITY_PLAYER_PATH")
fi
if [ -z "$build_dir" ] || [ ! -d "$build_dir" ]; then
  log "no build output at '${build_dir:-unset}', skipping"
  exit 0
fi

# どの Release に添付するかは、ビルドされた実際のリビジョンの bundleVersion で決まる
version=$(grep -E '^  bundleVersion: ' "$SETTINGS" 2> /dev/null | head -1 | awk '{print $2}')
if [ -z "$version" ]; then
  log "could not read bundleVersion from $SETTINGS, skipping"
  exit 0
fi
tag="v$version"

platform=${BUILD_PLATFORM:-unknown}
build_number=${UCB_BUILD_NUMBER:-unknown}
asset_name="battrail-$version-$platform-build$build_number.zip"

api() {
  # $1: method, $2: url, 残りは curl に渡す。標準出力に body、失敗時は空
  local method=$1 url=$2
  shift 2
  curl -sSf -X "$method" \
    -H 'Accept: application/vnd.github+json' \
    -H "Authorization: Bearer $GITHUB_RELEASE_TOKEN" \
    -H 'X-GitHub-Api-Version: 2022-11-28' \
    "$@" "$url"
}

release_json=$(api GET "https://api.github.com/repos/$REPO/releases/tags/$tag" 2>&1)
if [ $? -ne 0 ]; then
  log "release $tag not found, skipping"
  exit 0
fi
release_id=$(printf '%s' "$release_json" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])' 2> /dev/null)
if [ -z "$release_id" ]; then
  log "could not read release id for $tag, skipping"
  exit 0
fi

# Windows ビルダーの python は Cygwin パスを解釈しないので変換しておく
to_native() {
  if [ "${BUILDER_OS:-}" = "WINDOWS" ] && command -v cygpath > /dev/null 2>&1; then
    cygpath -wa "$1"
  else
    printf '%s' "$1"
  fi
}

# post-build の後に Build Automation が成果物をアップロードするため、zip を出力の
# 隣に置くとアーティファクトに巻き込まれる。作業用ディレクトリに逃がす
tmp_dir=$(mktemp -d)
trap 'rm -rf "$tmp_dir"' EXIT
zip_path="$tmp_dir/$asset_name"
log "zipping $build_dir -> $asset_name"
python3 - "$(to_native "$build_dir")" "$(to_native "$zip_path")" << 'PY'
import os, shutil, sys, zipfile

src, dst = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED) as archive:
    for root, _, files in os.walk(src):
        for name in files:
            path = os.path.join(root, name)
            if os.path.islink(path):
                continue
            # macOS の .app は実行ビットが落ちると起動しなくなるので mode を持ち越す
            info = zipfile.ZipInfo.from_file(path, os.path.relpath(path, src))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = (os.stat(path).st_mode & 0xFFFF) << 16
            with open(path, "rb") as src_file, archive.open(info, "w") as out:
                shutil.copyfileobj(src_file, out)
PY
if [ $? -ne 0 ] || [ ! -f "$zip_path" ]; then
  log "failed to create $asset_name, skipping"
  exit 0
fi

size=$(python3 -c 'import os,sys; print(os.path.getsize(sys.argv[1]))' "$(to_native "$zip_path")" 2> /dev/null)
if [ -n "$size" ] && [ "$size" -gt "$MAX_ASSET_BYTES" ]; then
  log "$asset_name is $size bytes, over GitHub's 2 GiB asset limit. skipping upload"
  exit 0
fi
log "$asset_name is $size bytes"

# 同名アセットが残っていると 422 になるので、再ビルド時は先に消す
existing_id=$(printf '%s' "$release_json" \
  | python3 -c 'import json,sys; n=sys.argv[1]; print(next((a["id"] for a in json.load(sys.stdin)["assets"] if a["name"]==n), ""))' \
    "$asset_name" 2> /dev/null)
if [ -n "$existing_id" ]; then
  log "replacing existing asset $asset_name"
  api DELETE "https://api.github.com/repos/$REPO/releases/assets/$existing_id" > /dev/null 2>&1
fi

upload_json=$(api POST \
  "https://uploads.github.com/repos/$REPO/releases/$release_id/assets?name=$asset_name" \
  -H 'Content-Type: application/zip' \
  --data-binary "@$zip_path" 2>&1)
upload_status=$?

if [ $upload_status -ne 0 ]; then
  log "upload failed: $upload_json"
  exit 0
fi
asset_url=$(printf '%s' "$upload_json" | python3 -c 'import json,sys; print(json.load(sys.stdin)["browser_download_url"])' 2> /dev/null)
log "uploaded $asset_name"

json_escape() {
  printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'
}

payload=$(printf '{"event_type":"%s","client_payload":{"version":"%s","build_number":"%s","target":"%s","platform":"%s","revision":"%s","asset_name":"%s","asset_url":"%s"}}' \
  "$EVENT_TYPE" \
  "$(json_escape "$version")" \
  "$(json_escape "$build_number")" \
  "$(json_escape "${BUILDCFG_TARGET:-unknown}")" \
  "$(json_escape "$platform")" \
  "$(json_escape "${BUILD_REVISION:-unknown}")" \
  "$(json_escape "$asset_name")" \
  "$(json_escape "${asset_url:-}")")

if api POST "https://api.github.com/repos/$REPO/dispatches" -d "$payload" > /dev/null 2>&1; then
  log "notified GitHub: $tag build #$build_number"
else
  log "dispatch failed, but the asset is attached to $tag"
fi

exit 0
