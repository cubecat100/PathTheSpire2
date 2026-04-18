# Path the spire2

## English

### Overview

Path the spire2 is a Slay the Spire 2 mod that analyzes the current map and draws recommended routes directly on the map UI.

The mod is focused on route planning support:

- automatically recommends a path when entering the map
- highlights the best path and the second-best path with different ring colors
- allows quick preference editing for `Unknown`, `Shop`, `Rest`, and `Elite` nodes
- allows score tuning through an in-game options popup
- includes simple test hotkeys for node marking and random path selection

### Current Features

- Recommended path is calculated from the current map state
- The top 2 weighted routes are visualized on the map
- Scoring reflects:
  - node preferences from the drag UI
  - branch count and elite child bonus
  - act-based weighting for `Elite` and `Unknown`
  - rest/elite proximity synergy
  - act-based shop weighting and shop distance
  - current HP and rest/elite risk
- A `Random` button can display a random route from start to boss
- Score weights can be adjusted live from the `Options` popup
- Option labels include tooltips in English

### How To Use

1. Enter the map screen.
2. The mod automatically evaluates the map and highlights a recommended route.
3. Use the `Path` panel near the map legend.
4. Drag node icons between `Prefer`, `Default`, and `Avoid`.
5. The route is recalculated immediately after a block is moved.
6. Open `Options` to adjust score weights.
7. Hover the option labels to read what each setting means.
8. Press `Random` if you want to display a random route instead of the recommended one.

### Hotkeys

- Debug only
- `F9`: cycles test node markers for all map nodes by type  
  `None -> Monster -> Elite -> Rest -> Treasure -> Shop -> Unknown -> None`
- `F10`: displays a random route from map start to boss

### Route Visualization

- Best route: stronger highlight ring
- Second-best route: weaker highlight ring
- Test node markers: separate colored ring markers by node type

These visuals are mod-only overlays and are intended for local use. They do not exist as native game markers.

---

## 한글

### 개요

Path the spire2는 현재 맵을 분석해서 추천 경로를 맵 UI 위에 직접 표시해주는 Slay the Spire 2 모드입니다.

이 모드는 경로 계획 보조에 초점을 두고 있습니다.

- 맵에 진입하면 자동으로 추천 경로를 계산
- 최적 경로와 2순위 경로를 서로 다른 원형 강조 색으로 표시
- `미지`, `상인`, `휴식`, `엘리트` 노드에 대한 선호도를 빠르게 조정 가능
- 게임 내 옵션 팝업으로 점수 가중치를 조정 가능
- 노드 표시와 랜덤 경로 선택을 위한 테스트용 단축키 포함

### 현재 기능

- 현재 맵 상태를 기준으로 추천 경로를 계산합니다.
- 가중치 상위 2개 경로를 맵에 시각화합니다.
- 점수 계산에는 아래 요소들이 반영됩니다.
  - 드래그 UI에서 설정한 노드 선호도
  - 갈림길 개수와 엘리트 자식 보너스
  - `엘리트`, `미지`에 대한 Act 기반 가중치
  - 휴식/엘리트 간 거리 시너지
  - Act 기반 상점 가중치와 상점 거리
  - 현재 체력과 휴식/엘리트 위험도

- `Random` 버튼으로 시작 지점부터 보스까지 랜덤 경로를 표시할 수 있습니다.
- `Options` 팝업에서 점수 가중치를 실시간으로 조정할 수 있습니다.
- 옵션 라벨에는 영어 툴팁이 들어 있습니다.

### 사용 방법

1. 맵 화면에 진입합니다.
2. 모드가 자동으로 맵을 평가하고 추천 경로를 표시합니다.
3. 맵 범례 근처의 `Path` 패널을 사용합니다.
4. 노드 아이콘을 `Prefer`, `Default`, `Avoid` 영역으로 드래그합니다.
5. 블록을 옮기면 즉시 경로가 다시 계산됩니다.
6. `Options`를 열어서 점수 가중치를 조정합니다.
7. 각 옵션 라벨 위에 커서를 올리면 설명 툴팁을 볼 수 있습니다.
8. 추천 경로 대신 랜덤 경로를 보고 싶다면 `Random`을 누릅니다.

### 단축키

- 디버그용
- `F9`: 맵에 생성된 모든 노드에 대해 테스트 표시를 타입별로 순환  
  `없음 -> 일반 적 -> 엘리트 -> 휴식 -> 보물 -> 상인 -> 미지 -> 없음`
- `F10`: 맵 시작 지점부터 보스까지 랜덤 경로를 표시

### 경로 시각화

- 최적 경로: 더 강한 강조 링
- 2순위 경로: 더 약한 강조 링
- 테스트 노드 표시: 노드 타입별 별도 원형 링 마커

이 시각 요소들은 모드 전용 오버레이이며, 게임 기본 마커가 아니라 로컬 표시용입니다.
