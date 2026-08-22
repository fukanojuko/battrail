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
# Web ビルドは Release への zip 添付に加えて GitHub Pages (gh-pages ブランチ) へ公開する。
# zip を配っても Content-Encoding を返せる置き場が無いと動かないので、URL を開けば
# 遊べる状態を用意する。Release と同じく main のビルドだけが対象。
# gh-pages へ push した後、deploy-pages.yml を repository_dispatch で起動する。
#
# 事前に必要な設定:
#   - GitHub 側で Settings > Pages > Source を「GitHub Actions」にする
#     （gh-pages ブランチはこのスクリプトが初回に作る。公開は deploy-pages.yml が行う）
#   - Player Settings の Decompression Fallback を on にしておく
#     （Pages は .gz に Content-Encoding を付けないため。ProjectSettings に反映済み）
#
# 必要な環境変数 (Build Automation の Advanced settings > Environment variables):
#   GITHUB_RELEASE_TOKEN  このリポジトリの Contents: write のみを持つ fine-grained PAT
#                         （gh-pages への push にも同じ権限を使う）

# ビルドを落とさないことを最優先にするため -e は付けない
set -uo pipefail

REPO=fukanojuko/battrail
EVENT_TYPE=unity-build-complete
PAGES_EVENT_TYPE=web-build-published
SETTINGS=ProjectSettings/ProjectSettings.asset
MAX_ASSET_BYTES=2147483648 # GitHub の Release アセット上限は 1 ファイル 2 GiB
PAGES_BRANCH=gh-pages

log() { echo "post-build: $*"; }

# post-build script は全ビルドで走る。Release も Pages 公開も main のビルドだけが対象
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

headers=(
  -H 'Accept: application/vnd.github+json'
  -H "Authorization: Bearer $GITHUB_RELEASE_TOKEN"
  -H 'X-GitHub-Api-Version: 2022-11-28'
)

api() {
  local method=$1 url=$2
  shift 2
  curl -sSf -X "$method" "${headers[@]}" "$@" "$url"
}

build_dir=${OUTPUT_DIRECTORY:-}
if [ -z "$build_dir" ] && [ -n "${UNITY_PLAYER_PATH:-}" ]; then
  # Web は単体の実行ファイルが無く、UNITY_PLAYER_PATH が出力ディレクトリ自体を指す。
  # 素直に dirname すると親を掴んで無関係なものまで巻き込む
  if [ -d "$UNITY_PLAYER_PATH" ]; then
    build_dir=$UNITY_PLAYER_PATH
  else
    build_dir=$(dirname "$UNITY_PLAYER_PATH")
  fi
fi
if [ -z "$build_dir" ] || [ ! -d "$build_dir" ]; then
  log "no build output at '${build_dir:-unset}', skipping"
  exit 0
fi

# BUILD_PLATFORM の綴りは Build Automation 側の設定に依存するので、出力の中身でも判定する
is_web_build() {
  case "$(printf '%s' "${BUILD_PLATFORM:-}" | tr 'A-Z' 'a-z')" in
    *web*) return 0 ;;
  esac
  [ -f "$build_dir/index.html" ] && [ -d "$build_dir/Build" ]
}

# gh-pages は常に「最新の Web ビルドだけ」の 1 コミットに作り直す。履歴を残すと
# 1 ビルドごとに数十 MB 積み上がるため、clone せず orphan を force push する。
publish_web() {
  if ! command -v git > /dev/null 2>&1; then
    log "git is unavailable, skipping web publish"
    return 0
  fi

  local work remote
  remote="https://x-access-token:$GITHUB_RELEASE_TOKEN@github.com/$REPO.git"
  work=$(mktemp -d)
  cp -R "$build_dir/." "$work/"

  # Unity の出力には _ 始まりのファイルが混じるので Jekyll を通さない
  touch "$work/.nojekyll"

  git -C "$work" init -q
  git -C "$work" add -A
  if ! git -C "$work" -c user.name='Unity Build Automation' -c user.email='noreply@github.com' \
    commit -q -m "Deploy web build #${UCB_BUILD_NUMBER:-unknown} ($tag)"; then
    log "nothing to publish to $PAGES_BRANCH"
    rm -rf "$work"
    return 0
  fi

  if git -C "$work" push -q --force "$remote" "HEAD:$PAGES_BRANCH" > /dev/null 2>&1; then
    log "pushed to $PAGES_BRANCH"
    request_pages_deploy
  else
    log "push to $PAGES_BRANCH failed"
  fi
  rm -rf "$work"
}

# gh-pages への push では deploy-pages.yml は起動しない。push イベントで使われる
# workflow ファイルはプッシュ先ブランチのものであり、gh-pages は web ビルドだけの
# orphan として作り直しているので .github/ を持たない。
# repository_dispatch ならデフォルトブランチ側の workflow が動くので、そちらを叩く。
request_pages_deploy() {
  local payload
  payload=$(printf '{"event_type":"%s","client_payload":{"version":"%s","build_number":"%s"}}' \
    "$PAGES_EVENT_TYPE" "$version" "${UCB_BUILD_NUMBER:-unknown}")

  if api POST "https://api.github.com/repos/$REPO/dispatches" -d "$payload" > /dev/null 2>&1; then
    log "requested Pages deploy -> https://${REPO%%/*}.github.io/${REPO##*/}/"
  else
    log "Pages deploy dispatch failed. $PAGES_BRANCH is updated, so re-run deploy-pages.yml manually"
  fi
}

version=$(awk '/^  bundleVersion: /{print $2; exit}' "$SETTINGS" 2> /dev/null | tr -d '\r')
# このスクリプトは Editor 終了後、つまり Unity が再シリアライズした後の
# ProjectSettings.asset を読む。過去に空行の混入で bundleVersion が
# 既定値 1.0 に戻り、存在しない v1.0 を探して黙って skip した。
# tag-release.yml と同じ形式検証を掛けて、壊れていれば理由を残す
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  log "bundleVersion '$version' in $SETTINGS is not in major.minor.patch form, skipping"
  exit 0
fi
tag="v$version"

# Pages 公開は Release への添付とは独立。ここで失敗しても zip の添付は続行する
if is_web_build; then
  publish_web
fi

platform=${BUILD_PLATFORM:-unknown}
build_number=${UCB_BUILD_NUMBER:-unknown}
asset_name="battrail-$version-$platform-build$build_number.zip"

# -f だと 404 も 401 もまとめて非ゼロになり原因が分からない。
# ここは切り分けが要るのでステータスを明示的に取る
release_res=$(curl -sS -w '\n%{http_code}' "${headers[@]}" \
  "https://api.github.com/repos/$REPO/releases/tags/$tag")
release_status=${release_res##*$'\n'}
release_json=${release_res%$'\n'*}
if [ "$release_status" != "200" ]; then
  log "GET release $tag returned HTTP $release_status, skipping"
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
