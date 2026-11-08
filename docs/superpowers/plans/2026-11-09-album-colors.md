# 앨범 색 자동 적용 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 설정의 '곡마다 자동 변경'을 켜면 위젯 색이 앨범 사진에서 뽑은 색으로 부드럽게 바뀐다.

**Architecture:** C# 쪽 `ArtPalette`가 앨범 사진(바이트)에서 배경·강조·글자색을 계산하고, 사진이 바뀔 때 한 번만 계산해 `AlbumArt`에 담는다. `NowPlaying`에 `Palette`가 실려 SSE로 위젯에 간다. 위젯은 `autoColors`가 켜져 있고 팔레트가 있으면 그 색을 CSS 변수에 넣고, CSS `transition`이 색을 부드럽게 바꾼다.

**Tech Stack:** .NET 8 WinForms(System.Drawing으로 이미지 해석), xUnit, 순수 HTML/CSS/JS.

**Spec:** `docs/superpowers/specs/2026-11-09-album-colors-design.md`

## Global Constraints

- 새 의존성 추가 없음. 이미지 해석은 이미 쓸 수 있는 `System.Drawing`(UseWindowsForms)으로.
- 글자색은 배경과 WCAG 대비 **4.5:1 이상**, 강조색은 배경과 **3:1 이상**.
- 자동 색은 저장하지 않는다. 사용자가 고른 `TextColor/BackgroundColor/AccentColor`는 그대로.
- `autoColors` 기본값 **false**.
- 색 전환 시간 **1.2s**.
- 스타일 C는 배경색을 쓰지 않으며, 자동일 때도 **강조색만** 바뀐다(글자색 = 사용자 색).
- OBS 브라우저(CEF 127): `color-mix` 사용 금지(지금처럼 `rgb 삼중값` 변수 방식).
- 모든 화면 문구 ko/en/ja 3개 언어.
- 사진이 없거나 해석 실패 → 팔레트 `null` → 사용자 색.

## Review Focus

1. 아주 어둡거나 하얀 단색 앨범 → 강조색이 배경과 3:1 이상이어야 함 (`From_SolidColors_MeetContrast`가 고정).
2. 손상된/빈 이미지 바이트 → 예외 없이 `null` (`From_BadBytes_ReturnsNull`).
3. 일시정지 동안 사라지는 1.5초 사이에 색이 사용자 색으로 튀면 안 됨 → 위젯은 지금처럼 정지 시 `render`를 건너뛰므로 유지됨. 손으로 확인.
4. 설정 페이지 미리보기(샘플 곡, 팔레트 없음)는 체크를 켜도 사용자 색 → 실제 곡 재생 중이면 앨범 색. 손으로 확인.
5. 밝은 배경에서 진행 막대 빈 부분이 안 보이는 문제 → 막대 바탕을 글자색 기준 반투명으로. 손으로 확인.

---

### Task 1: 앨범 사진에서 색 뽑기 (`ArtPalette`)

**Files:**
- Create: `src/ObsNowPlaying/Media/ArtPalette.cs`
- Test: `tests/ObsNowPlaying.Tests/ArtPaletteTests.cs`

**Interfaces:**
- Produces: `public sealed record ArtPalette(string Background, string Accent, string Text)` (값은 `#RRGGBB`),
  `public static ArtPalette? From(byte[] image)`, `public static double Contrast(string a, string b)` (WCAG 대비비).

- [ ] **Step 1: 실패하는 테스트 작성** — 테스트 안에서 `System.Drawing.Bitmap`으로 PNG 바이트를 만든다.
  - `From_SolidColors_MeetContrast` — `[Theory]` 단색 `#000000, #FFFFFF, #808080, #1DB954, #FFE600, #0A1F6B`: 결과가 null 아님, `Contrast(Text, Background) >= 4.5`, `Contrast(Accent, Background) >= 3.0`.
  - `From_TwoColorImage_BackgroundIsLargerArea` — 왼쪽 75% 남색(#14204A), 오른쪽 25% 주황(#FF7A00): Background가 남색에 가깝고(각 채널 차 ≤ 24), Accent가 주황 계열(R > G > B).
  - `From_DarkBackground_WhiteText` — 단색 #101010 → `Text == "#FFFFFF"`; 단색 #F0F0F0 → `Text == "#000000"`.
  - `From_BadBytes_ReturnsNull` — `new byte[] {1,2,3}`과 빈 배열 → null.
  - `Contrast_KnownValues` — `Contrast("#000000","#FFFFFF")` ≈ 21 (±0.01), 순서 바꿔도 같음.
- [ ] **Step 2: 실행해서 실패 확인** — `dotnet test --filter ArtPaletteTests` → 컴파일 실패(ArtPalette 없음).
- [ ] **Step 3: 구현** — 방법:
  - `Bitmap`으로 읽어 24×24로 줄여 그린 뒤 픽셀을 채널당 상위 4비트로 묶고(묶음별 개수·합계), 각 묶음 평균색을 쓴다. 알파 < 128 픽셀은 무시.
  - Background = 개수 최대 묶음.
  - Accent = Background와 RGB 거리 > 60인 묶음 중 `채도 × sqrt(개수)` 최대. 없으면 Background.
  - Accent가 Background와 대비 3:1 미만이면 흰색 또는 검정 쪽(대비가 더 커지는 쪽)으로 10%씩 섞어 3:1 이상이 될 때까지.
  - Text = 흰색과 검정 중 Background와 대비가 큰 쪽 (`#FFFFFF`/`#000000`; 이 둘이면 수학적으로 항상 ≥ 4.58).
  - 해석 실패(예외)·픽셀 없음 → null.
- [ ] **Step 4: 실행해서 통과 확인** — `dotnet test --filter ArtPaletteTests` → PASS.
- [ ] **Step 5: 커밋** — `feat: pick widget colors from album art`

### Task 2: 팔레트를 곡 정보에 싣고 설정 추가

**Files:**
- Modify: `src/ObsNowPlaying/Media/NowPlaying.cs` (`AlbumArt`, `NowPlaying`)
- Modify: `src/ObsNowPlaying/Media/MediaWatcher.cs:155-156, 120-122`
- Modify: `src/ObsNowPlaying/Settings/AppSettings.cs` (속성 추가)
- Test: `tests/ObsNowPlaying.Tests/SettingsStoreTests.cs`, `tests/ObsNowPlaying.Tests/WebServerTests.cs`

**Interfaces:**
- Consumes: `ArtPalette.From(byte[])` (Task 1)
- Produces: `AlbumArt(byte[] Bytes, string ContentType, string Version, ArtPalette? Palette)`;
  `NowPlaying(..., long PositionAtMs, ArtPalette? Palette)` — SSE JSON에 `palette: {background, accent, text}` 또는 `null`;
  `AppSettings.AutoColors` (bool, 기본 false) — JSON `autoColors`.

- [ ] **Step 1: 실패하는 테스트 작성**
  - `SettingsStoreTests.AutoColors_DefaultsOff` — 파일 없이 `Load().AutoColors == false`; true로 저장 후 다시 읽으면 true.
  - `WebServerTests`의 SSE 테스트 방식으로 `Events_NowPlayingCarriesPalette` — `current()`가 `Palette = new ArtPalette("#112233","#FF8800","#FFFFFF")`인 NowPlaying을 돌려줄 때 `nowplaying` data 줄에 `"palette":{"background":"#112233"` 포함.
- [ ] **Step 2: 실행해서 실패 확인** — 컴파일 실패(속성/매개변수 없음).
- [ ] **Step 3: 구현**
  - `AlbumArt`/`NowPlaying`에 마지막 매개변수 `ArtPalette? Palette` 추가, `NowPlaying.Empty`는 `null`.
  - `MediaWatcher.LoadArtAsync`: 새 버전일 때만 `ArtPalette.From(bytes)` 계산해 `AlbumArt`에 넣는다(같은 사진은 재계산 없음).
  - `RefreshOnceAsync`: `next`에 `Art?.Palette` 전달.
  - `AppSettings.AutoColors { get; set; } = false;` (`Sanitize` 변경 불필요).
  - 다른 `new NowPlaying(...)`/`new AlbumArt(...)` 호출부(테스트 포함)를 grep으로 찾아 모두 맞춘다.
- [ ] **Step 4: 전체 테스트 통과 확인** — `dotnet test` → 전부 PASS.
- [ ] **Step 5: 커밋** — `feat: send the album palette with now-playing and add the auto colors setting`

### Task 3: 위젯이 자동 색을 부드럽게 적용

**Files:**
- Modify: `src/ObsNowPlaying/wwwroot/widget.js` (`render`의 색 지정 부분)
- Modify: `src/ObsNowPlaying/wwwroot/widget.css`

**Interfaces:**
- Consumes: SSE `nowplaying.palette`, `settings.autoColors` (Task 2)

- [ ] **Step 1: `widget.js`** — `render`에서 `const p = settings.autoColors && shown.palette;` 이면 배경=`p.background`, 강조=`p.accent`, 글자=`style === 'C' ? settings.textColor : p.text`; 아니면 사용자 색. `--text-rgb` 변수도 함께 설정(`hexToRgb` 재사용).
- [ ] **Step 2: `widget.css`** — `:root`에 `--text-rgb: 255, 255, 255`. 전환 1.2s ease 추가: `body`의 `color`, `.w`의 `background-color`(기존 opacity/transform 전환 유지), `.bar i`의 `background-color`(기존 width 전환 유지), `.style-B .eq i`의 `background-color`. `.bar` 바탕을 `rgba(var(--text-rgb), .18)`로.
- [ ] **Step 3: 확인** — 헤드리스 Chrome(CDP 스크립트, 이전 `cdp-check.mjs` 방식)으로 `/widget?style=A`를 열고 `window.render`로 팔레트 A→B 곡을 차례로 넣어, 0.3초 뒤 `.w`의 계산된 `background-color`가 두 색의 중간값이고 1.5초 뒤 B 색인지 확인. `autoColors:false`면 사용자 색인지, 스타일 C에서 글자색이 사용자 색인지 확인.
- [ ] **Step 4: `dotnet test`** → 전부 PASS (위젯 파일은 리소스로 들어가므로 빌드 확인).
- [ ] **Step 5: 커밋** — `feat: widget fades to album colors when auto colors is on`

### Task 4: 설정 페이지 체크와 문구, README

**Files:**
- Modify: `src/ObsNowPlaying/wwwroot/settings.html` (색 fieldset, `.colors` 아래), `settings.js` (`readForm`/`fillForm`), `settings.css`(필요 시 힌트 간격만)
- Modify: `src/ObsNowPlaying/wwwroot/i18n/ko.json`, `en.json`, `ja.json`
- Modify: `README.md`, `README.ko.md`, `README.ja.md`

- [ ] **Step 1: 문구 추가** — 키 `settings.autoColors`, `settings.autoColorsHint`:
  - ko: "곡마다 자동 변경 (앨범 사진 색)" / "앨범 사진이 없을 때는 위에서 고른 색을 써요. 큰 글씨 스타일은 강조색만 바뀌어요."
  - en: "Change colors with each song (from album art)" / "Songs without album art use the colors above. The big text style only changes the accent color."
  - ja: "曲ごとに自動で色を変える（アルバムアートの色）" / "アルバムアートがない曲は上で選んだ色を使います。大きな文字スタイルはアクセント色だけが変わります。"
- [ ] **Step 2: 화면** — `.colors` 바로 아래 `<label class="switch"><input type="checkbox" id="autoColors">…</label>`와 `<p class="hint" data-i18n="settings.autoColorsHint">`. `readForm`에 `autoColors: $('autoColors').checked`, `fillForm`에 반영. 미리보기는 기존 postMessage로 자동 반영됨.
- [ ] **Step 3: README 3개 언어** — 색 설정 설명 근처에 한 줄씩(위 힌트와 같은 뜻).
- [ ] **Step 4: 확인** — `dotnet test`(StringsTests가 3개 언어 키 일치를 확인) → PASS. 앱을 다시 빌드·재시작(시작 시각 확인)하고, 설정 페이지에서 체크 → 저장 → 새로고침 후 체크 유지, 재생 중 미리보기 색이 바뀌는지 헤드리스 Chrome으로 확인.
- [ ] **Step 5: 커밋** — `feat: auto colors switch on the settings page`

### Task 5: 마무리

- [ ] 패키지 스크립트로 zip 다시 만들기(버전 이름은 사용자에게 물어 정함; 지금은 v0.1.0 유지), 앱 재시작 후 사용자에게 OBS 확인 요청(경로 + 탐색기 열기).
