#!/usr/bin/env bash
# 그림 생성 (Open AI Service Hub, OpenAI 호환 API)
#   사용: Tools/gen-image.sh "<프롬프트>" <저장할 png> [크기 WxH, 기본 1024x1024] [모델, 기본 Qwen-Image]
#   키: ~/.openai_key (저장소에 넣지 않는다)
#   투명 배경이 필요하면 프롬프트에 "plain solid bright green background"를 넣고
#   ArtDrafts/Raw에 저장한 뒤 BrushGame 메뉴의 초록 배경 처리 도구로 지운다.
set -euo pipefail

prompt="$1"
out="$2"
size="${3:-1024x1024}"
model="${4:-Qwen-Image}"
key=$(tr -d '\r\n ' < "$HOME/.openai_key")

body=$(PROMPT="$prompt" MODEL="$model" SIZE="$size" powershell -NoProfile -Command \
  '@{ model = $env:MODEL; prompt = $env:PROMPT; size = $env:SIZE; n = 1 } | ConvertTo-Json -Compress')

tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT
# 한도: 동시 요청 1건, 분당 10건. 여러 장은 반드시 하나씩 6초 이상 간격으로 부른다 (넘기면 경고가 쌓여 제한된다)
for attempt in 1 2 3; do
  curl -sS -m 300 -H "Authorization: Bearer $key" -H "Content-Type: application/json; charset=utf-8" \
    --data-binary "$body" https://open.hasa.re.kr/v1/images/generations -o "$tmp"
  wait=$(sed -n 's/.*"retry_after":\([0-9]*\).*/\1/p' "$tmp")
  [ -z "$wait" ] && break
  sleep $((wait + 2))
done

mkdir -p "$(dirname "$out")"
if ! sed -n 's/.*"b64_json":"\([^"]*\)".*/\1/p' "$tmp" | base64 -d > "$out" || [ ! -s "$out" ]; then
  echo "생성 실패: $(head -c 500 "$tmp")" >&2
  rm -f "$out"
  exit 1
fi
echo "$out"
