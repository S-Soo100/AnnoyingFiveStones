#!/bin/bash
# 손 모델 .blend → FBX 변환
#
# 왜 필요한가: Unity는 Blender가 설치돼 있어야 .blend를 읽는다(내부적으로 Blender를 호출).
# Blender를 설치했다면 이 스크립트 한 번으로 Assets에 FBX가 들어간다.
#
# 사용법:  tools/convert-hand-fbx.sh
# 다른 경로의 Blender를 쓰려면:  BLENDER=/path/to/Blender tools/convert-hand-fbx.sh

set -euo pipefail

BLEND="/Users/baek/Downloads/Low Poly Hand  Rigged/Rigged Low Poly Hand by erik90mx.blend"
OUTDIR="/Users/baek/unityProjects/AnnoyingFiveStones/Assets/Models/Hand"
OUT="$OUTDIR/LowPolyHand.fbx"
BIN="${BLENDER:-/Applications/Blender.app/Contents/MacOS/Blender}"

if [ ! -x "$BIN" ]; then
  echo "❌ Blender를 찾을 수 없습니다: $BIN"
  echo "   https://www.blender.org/download/ 에서 설치 후 다시 실행하세요."
  exit 1
fi

if [ ! -f "$BLEND" ]; then
  echo "❌ 원본 .blend를 찾을 수 없습니다: $BLEND"
  exit 1
fi

mkdir -p "$OUTDIR"

"$BIN" --background "$BLEND" --python-expr "
import bpy
bpy.ops.export_scene.fbx(
    filepath=r'''$OUT''',
    use_selection=False,
    add_leaf_bones=False,           # Unity에서 불필요한 말단 본 제거
    bake_anim=False,                # 애니메이션 없음 — 손가락은 코드로 제어한다
    axis_forward='-Z', axis_up='Y', # Unity 좌표계
    global_scale=1.0)
print('[convert] FBX 저장 완료')
"

echo "✅ 완료: $OUT"
echo "   Unity에서 Assets/Refresh 후 임포트 설정(Rig: Generic 또는 Humanoid)을 확인하세요."
