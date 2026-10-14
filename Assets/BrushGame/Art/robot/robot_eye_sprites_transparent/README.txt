ROBOT EYE SPRITE ASSETS

7 transparent RGBA PNG sheets; 16 frames per sheet; 112 separate frame PNGs.
All delivered filenames use ASCII English.

IMPORT
Sheet: 2048 x 1280 pixels. Grid: 4 columns x 4 rows.
Cell: 512 x 320 pixels. Read left to right, then top to bottom.
Use a fixed center pivot (0.5, 0.5). Do not auto-trim individual frames.
Use normal straight-alpha blending. No navy background is included in sprites.
Suggested inspection playback: 100 ms per frame (10 fps).
Power-on, low-battery, expression transition and eating are one-shot actions.
Do not automatically loop a shutdown animation back to the first lit frame.
The preview GIFs repeat for inspection only, including one-shot actions.

ROBOT PLACEMENT
Reference robot image dimensions: 1230 x 1278.
Preview overlay: x=415, y=397, width=400, height=250 pixels.
Same position and scale are used for all 112 frames. Sprites remain inside
this robot's display. Different robot crops/resolutions need proportional placement.
Preview GIFs and the contact image have an opaque robot/background for viewing;
only sheets/ and frames/ are the transparent game assets.

PROCESSING
Removed the baked navy background and gridlines, extracted the original 16 poses,
and padded/rescaled each sheet uniformly to a common frame size. Kept aspect ratio.
Original drawing shapes, action sequence and within-sheet movement were retained.
Faint shutdown dots and the empty final shutdown frame were preserved.
Edge colors were unmatted against the estimated original navy to reduce dark fringes.
Opaque foreground colors were retained. No mouth, blush or new artwork was added.
PNG transparency cannot recover the original pre-composite edge colors exactly;
edge alpha/color separation was estimated from the flattened sources.

검수 결과
- 7종 모두 16프레임, 총 112개. 모든 결과 프레임 크기는 동일합니다.
- 시트/개별 프레임의 배경은 실제 알파 0이며 격자선을 제거했습니다.
- 방전 16번 프레임은 의도된 빈 프레임입니다.
- 원본 동작은 완벽하게 매끄러운 상태가 아닙니다. 아래 문제를 숨기지 않았습니다.
- 표정 전환: 프레임이 진행되면서 눈의 중심 위치가 위로 이동합니다.
- 먹기: 그릇과 눈의 위치가 조금씩 달라지고 밥알 이동이 불연속적입니다.
- 노래: 음표가 크게 이동하는 구간과 마지막에서 처음으로 넘어갈 때 차이가 있습니다.
- 공놀이: 눈/공의 이동 간격이 일정하지 않으며 반복 경계도 완전히 일치하지 않습니다.
- 전원 켜짐: 마지막 구간의 눈 높이가 일정하지 않습니다.
- 방전: 12→13번에서 눈 밝기가 다시 높아지는 원본 문제가 있습니다.
- 서로 다른 동작의 눈 크기와 위치가 일치하지 않습니다. 전환 시 곧바로 교체하면 튈 수 있습니다.
- 요청에 따라 이번에는 이 동작들을 임의로 다시 그리거나 재배열하지 않았습니다.
- GIF는 적용 위치/동작 검토용이며 실제 게임 엔진에서의 실행 검증은 아닙니다.
