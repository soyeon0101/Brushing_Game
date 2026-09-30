#!/usr/bin/env bash
# 그림 편집 (Open AI Service Hub, Qwen-Image-Edit): 기준 그림의 캐릭터를 유지한 채 자세·방향·배경을 바꾼다
#   사용: Tools/edit-image.sh <기준 그림> "<프롬프트>" <저장할 png> [크기 WxH, 기본 1024x1024]
#   키: ~/.openai_key (저장소에 넣지 않는다). 한도는 gen-image.sh와 같다 (동시 1건, 분당 10건)
set -euo pipefail

ref="$1"
prompt="$2"
out="$3"
size="${4:-1024x1024}"
key=$(tr -d '\r\n ' < "$HOME/.openai_key")

body=$(mktemp)
resp=$(mktemp)
trap 'rm -f "$body" "$resp"' EXIT
REF="$(cygpath -w "$ref" 2>/dev/null || echo "$ref")" PROMPT="$prompt" SIZE="$size" OUT="$(cygpath -w "$body" 2>/dev/null || echo "$body")" \
  powershell -NoProfile -Command '
    $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($env:REF))
    $mime = if ($env:REF -match "\.jpe?g$") { "image/jpeg" } else { "image/png" }
    @{ model = "Qwen-Image-Edit"; prompt = $env:PROMPT; reference = "data:$mime;base64,$b64"; size = $env:SIZE; n = 1 } |
      ConvertTo-Json -Compress | Out-File -Encoding utf8 $env:OUT'

for attempt in 1 2 3; do
  curl -sS -m 300 -H "Authorization: Bearer $key" -H "Content-Type: application/json; charset=utf-8" \
    --data-binary @"$body" https://open.hasa.re.kr/v1/images/generations -o "$resp"
  wait=$(sed -n 's/.*"retry_after":\([0-9]*\).*/\1/p' "$resp")
  [ -z "$wait" ] && break
  sleep $((wait + 2))
done

mkdir -p "$(dirname "$out")"
if ! sed -n 's/.*"b64_json":"\([^"]*\)".*/\1/p' "$resp" | base64 -d > "$out" || [ ! -s "$out" ]; then
  echo "편집 실패: $(head -c 500 "$resp")" >&2
  rm -f "$out"
  exit 1
fi
echo "$out"
