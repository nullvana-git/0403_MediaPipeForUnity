# MediaPipe Hand Tracking for Unity

MediaPipe Unity Plugin을 사용해 웹캠으로 손 랜드마크를 추적하고, 주먹/손 펼침 제스처를 감지하는 Unity 프로젝트입니다.

## 주요 기능

- 웹캠 실시간 손 랜드마크 추적 (최대 2개 손)
- 주먹 / 손 펼침 제스처 감지 (`FistGesture`)
- 인스펙터 드롭다운으로 연결된 웹캠 선택
- UnityEvent 기반으로 제스처에 반응하는 로직 연결 가능

## 씬 구성

| 씬 | 설명 |
|----|------|
| `HandTracking` | 손 랜드마크 추적 + 주먹 제스처 감지 메인 씬 |
| `WebcamTestScene` | 웹캠 단독 출력 테스트 |
| `SampleScene` | 기본 템플릿 |

## 커스텀 스크립트

| 파일 | 역할 |
|------|------|
| `HandTrackingManager.cs` | MediaPipe 초기화, 웹캠 입력, 랜드마크 결과 제공 |
| `FistGesture.cs` | MCP→TIP 거리 비율로 주먹/손 펼침 판단, UnityEvent 발생 |

## 시작하기

### 1. 요구 사항

- Unity 2022.3 이상
- [MediaPipe Unity Plugin v0.16.3](https://github.com/homuler/MediaPipeUnityPlugin/releases/tag/v0.16.3)

### 2. 플러그인 설치

이 저장소에는 플러그인 파일이 포함되어 있지 않습니다. 아래 방법으로 직접 설치해야 합니다.

1. 위 링크 Releases 탭에서 `MediaPipeUnityPlugin-0.16.3.unitypackage` 다운로드
2. Unity 메뉴 → **Assets → Import Package → Custom Package**
3. 다운로드한 `.unitypackage` 파일 선택 후 임포트

플러그인, 샘플 씬, 모델 파일이 모두 포함되어 있어 별도 추가 설치가 필요 없습니다.

### 3. 실행

1. `Assets/Scenes/HandTracking` 씬 열기
2. `HandTrackingManager` 컴포넌트 인스펙터에서 **카메라 선택** 드롭다운으로 웹캠 지정
3. Play

## 제스처 감지 조정

`FistGesture` 컴포넌트의 **Fist Threshold** 값으로 민감도를 조절합니다.

- 값이 낮을수록 더 강하게 쥐어야 주먹으로 판정
- 화면 하단에 현재 `Ratio` 값이 표시되므로 이를 보며 조정

## 라이선스

이 프로젝트의 커스텀 코드(`HandTrackingManager.cs`, `FistGesture.cs` 등)는 MIT 라이선스입니다.  
MediaPipe Unity Plugin은 [homuler/MediaPipeUnityPlugin](https://github.com/homuler/MediaPipeUnityPlugin)의 MIT 라이선스를 따릅니다.
