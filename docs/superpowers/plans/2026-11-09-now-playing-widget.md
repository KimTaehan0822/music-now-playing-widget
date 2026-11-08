# 지금 재생 중 위젯 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Windows 미디어 세션(GSMTC)에서 지금 재생 중인 곡을 읽어, OBS 브라우저 소스용 위젯(3가지 모양)과 설정 페이지(한/영/일)를 localhost로 제공하는 exe 하나를 만든다.

**Architecture:** .NET 8 WinForms 트레이 앱(창 없음) 안에서 `HttpListener` 웹 서버가 돈다. `MediaWatcher`는 GSMTC 이벤트를 받는다. `SessionPicker`는 보여줄 세션을 고른다. 서버는 SSE(`/events`)로 위젯과 설정 페이지에 곡 정보와 설정을 밀어 준다. HTML/JS/CSS/글꼴/언어 파일은 exe에 포함된 리소스로 서비스한다.

**Tech Stack:** C# / .NET 8 (`net8.0-windows10.0.19041.0`, WinForms, Windows.Media.Control), xUnit, 순수 HTML/CSS/JS(빌드 도구 없음), Pretendard Variable 글꼴(OFL).

**Spec:** `docs/superpowers/specs/2026-11-09-now-playing-widget-design.md`

## Global Constraints

- 이 프로젝트는 독립된 저장소에서 만든다.
- Windows 10(1809 이상)/11 전용. 게시는 `win-x64`, self-contained, single-file로 한다. 받는 사람은 아무것도 설치하지 않는다.
- 서버는 `http://localhost:{port}/`에서만 받는다. 기본 포트는 **8976**이고 settings.json에서만 바꾼다.
- 경로: `/widget`, `/widget?style=A|B|C`, `/settings`, `/events`(SSE), `/art`, `/api/settings`(GET/POST), `/api/apps`(GET).
- 설정 파일 위치: `%APPDATA%\ObsNowPlaying\settings.json`.
- 처음 보는 앱은 켜짐이고, 알려진 브라우저는 꺼짐이다. 브라우저 목록: `chrome.exe, msedge.exe, whale.exe, firefox.exe, opera.exe, brave.exe, vivaldi.exe`. AppUserModelId에 이 이름이 포함되는지를 대소문자 무시로 비교한다.
- 언어: `ko`, `en`, `ja`. 처음에는 Windows 표시 언어를 따른다(ko→ko, ja→ja, 그 밖→en). 문구는 `wwwroot/i18n/{ko,en,ja}.json`에만 둔다. 트레이 메뉴도 같은 파일을 쓴다.
- 기본 설정: 모양 `A`, 글자색 `#FFFFFF`, 배경색 `#14161C`, 포인트색 `#1DB954`, 앨범 사진 켜짐, 진행 막대 켜짐.
- 일시정지 또는 재생 없음 → 1500ms 기다린 뒤 위젯을 숨긴다(페이드 아웃). 그 사이 다시 재생되면 숨기지 않는다.
- 이번 작업은 `dist/ObsNowPlaying-v0.1.0.zip`을 만드는 데까지다. 깃허브 원격 저장소와 릴리스 공개는 하지 않는다. 라이선스: MIT + Pretendard OFL.
- UI 작업은 frontend-design 스킬로 한다. 시안: `.superpowers/brainstorm/923-1794165481/content/widget-style.html`.

## Review Focus

1. 재생 중이던 앱을 꺼서 세션이 사라짐 → 위젯이 숨겨지고 프로그램은 죽지 않는다. (Task 1 테스트: 빈 목록이면 null)
2. 앨범 사진이 없거나 읽다가 예외가 남 → 사진 자리 없이 글자만 보인다. `/art`는 404를 돌려준다. (Task 4 테스트)
3. 포트가 이미 사용 중이거나 exe를 두 번 실행함 → 두 번째 실행은 설정 페이지만 열고 종료한다. 다른 프로그램이 포트를 쓰고 있으면 알림을 띄우고 종료한다. (Task 6)
4. OBS가 exe보다 먼저 켜졌거나 exe를 재시작함 → 위젯이 SSE로 다시 연결해서 스스로 회복한다. (Task 5 확인 단계)
5. settings.json이 깨졌거나 모르는 값이 있음 → 기본값으로 시작하고 죽지 않는다. (Task 2 테스트)
6. 곡 제목에 `<b>` 같은 HTML 문자가 들어감 → 글자 그대로 보인다(`textContent`만 사용). (Task 5)

---

## 파일 구조

```
obs-now-playing/
  .gitignore                      bin/ obj/ dist/ .superpowers/
  ObsNowPlaying.sln
  src/ObsNowPlaying/
    ObsNowPlaying.csproj
    Program.cs                    시작: 단일 실행, 설정 로드, 서버·감시·트레이 연결
    Media/NowPlaying.cs           record NowPlaying (보여줄 곡 정보)
    Media/SessionPicker.cs        보여줄 세션 고르기(순수 함수)
    Media/MediaWatcher.cs         GSMTC 이벤트 → NowPlaying + 앨범 사진 바이트
    Settings/AppSettings.cs       설정 모델 + 기본값 + 앱 켜짐 판단
    Settings/SettingsStore.cs     json 읽기/쓰기
    Web/WebServer.cs              HttpListener 라우팅, 리소스 서비스, API
    Web/SseHub.cs                 SSE 연결 목록과 방송
    Tray/TrayApp.cs               트레이 아이콘과 메뉴(언어 반영)
    Localization/Strings.cs       i18n json 읽기, 언어 고르기
    wwwroot/widget.html|css|js
    wwwroot/settings.html|css|js
    wwwroot/i18n/ko.json en.json ja.json
    wwwroot/fonts/PretendardVariable.woff2
  tests/ObsNowPlaying.Tests/
    ObsNowPlaying.Tests.csproj
    SessionPickerTests.cs  AppSettingsTests.cs  SettingsStoreTests.cs
    StringsTests.cs  WebServerTests.cs
  README.md  README.ko.md  README.ja.md  LICENSE  licenses/Pretendard-OFL.txt
  scripts/package.ps1
```

---

### Task 1: 프로젝트 뼈대 + SessionPicker

**Files:**
- Create: `.gitignore`, `ObsNowPlaying.sln`, `src/ObsNowPlaying/ObsNowPlaying.csproj`, `src/ObsNowPlaying/Program.cs`(빈 Main), `src/ObsNowPlaying/Media/NowPlaying.cs`, `src/ObsNowPlaying/Media/SessionPicker.cs`
- Test: `tests/ObsNowPlaying.Tests/SessionPickerTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record SessionInfo(string AppId, string Title, string Artist, string Album, bool IsPlaying, DateTimeOffset LastPlayStarted, TimeSpan? Position, TimeSpan? Duration, DateTimeOffset PositionAt)`
  - `public static SessionInfo? SessionPicker.Pick(IReadOnlyList<SessionInfo> sessions, string? currentAppId, Func<string, bool> isAppEnabled)`
  - `public sealed record NowPlaying(bool Playing, string Title, string Artist, string Album, string AppId, string? ArtVersion, long? PositionMs, long? DurationMs, long PositionAtMs)`. `NowPlaying.Empty`는 Playing=false인 값이다.

- [ ] **Step 1:** .NET 8 SDK가 없으면 `winget install Microsoft.DotNet.SDK.8 --accept-source-agreements --accept-package-agreements`로 설치한다. `dotnet --list-sdks` 결과에 `8.0.`이 보여야 한다.
- [ ] **Step 2:** `git init`을 하고 sln과 두 csproj를 만든다. 앱 csproj에 들어갈 값: `TargetFramework=net8.0-windows10.0.19041.0`, `OutputType=WinExe`, `UseWindowsForms=true`, `Nullable=enable`, `RuntimeIdentifier=win-x64`, `<EmbeddedResource Include="wwwroot\**" LogicalName="wwwroot/%(RecursiveDir)%(Filename)%(Extension)" />`. 테스트 csproj는 같은 TFM으로 xunit을 쓰고 앱 프로젝트를 참조한다.
- [ ] **Step 3: 실패하는 테스트 작성** (`SessionPickerTests`)
  - `Empty_ReturnsNull`: 빈 목록이면 `null`
  - `DisabledApp_IsIgnored`: chrome만 재생 중이고 chrome이 꺼져 있으면 `null`
  - `OnlyPlaying_IsChosen`: Spotify 일시정지 + Melon 재생이면 Melon
  - `CurrentSession_WinsAmongPlaying`: 둘 다 재생 중이고 current=Spotify이면 Spotify
  - `MostRecentStart_WinsWhenCurrentNotEligible`: 둘 다 재생 중이고 current=꺼진 chrome이면 `LastPlayStarted`가 늦은 쪽
  - `NothingPlaying_ReturnsNull`: 모두 일시정지면 `null`
- [ ] **Step 4:** `dotnet test`를 실행한다. 컴파일 실패나 테스트 FAIL이 나와야 한다.
- [ ] **Step 5:** `SessionPicker.Pick`을 구현한다. 순서: 꺼진 앱 제외 → 재생 중만 남김 → current가 있으면 그것 → 없으면 `LastPlayStarted`가 가장 늦은 것.
- [ ] **Step 6:** `dotnet test`를 실행한다. 6개 모두 PASS여야 한다.
- [ ] **Step 7:** 커밋한다. 앞서 쓴 설계·계획 문서도 함께 넣는다: `git add -A && git commit -m "feat: project skeleton and session picker"`

### Task 2: 설정 모델과 저장

**Files:**
- Create: `src/ObsNowPlaying/Settings/AppSettings.cs`, `src/ObsNowPlaying/Settings/SettingsStore.cs`
- Test: `tests/ObsNowPlaying.Tests/AppSettingsTests.cs`, `tests/ObsNowPlaying.Tests/SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class AppSettings { int Port=8976; string? Language=null; string DefaultStyle="A"; string TextColor="#FFFFFF"; string BackgroundColor="#14161C"; string AccentColor="#1DB954"; bool ShowArt=true; bool ShowProgress=true; Dictionary<string,bool> Apps (대소문자 무시) }`. JSON 속성 이름은 camelCase다.
  - `bool AppSettings.IsAppEnabled(string appId)`: 목록에 있으면 그 값을 쓴다. 없으면 `!IsKnownBrowser(appId)`다.
  - `bool AppSettings.RememberApp(string appId)`: 처음 보는 앱이면 기본값으로 `Apps`에 넣고 true를 돌려준다(저장이 필요하다는 뜻).
  - `static bool AppSettings.IsKnownBrowser(string appId)`
  - `AppSettings AppSettings.Sanitize()`: 스타일이 A/B/C가 아니면 "A"로 바꾼다. 색이 `#RRGGBB`가 아니면 기본색으로 바꾼다. 언어가 ko/en/ja/null이 아니면 null로 바꾼다. 포트가 1024~65535 밖이면 8976으로 바꾼다.
  - `SettingsStore(string path)`, `AppSettings Load()`, `void Save(AppSettings s)`, `static string DefaultPath`

- [ ] **Step 1: 실패하는 테스트 작성**
  - `Browser_DefaultsOff`: `IsAppEnabled("Chrome")`, `IsAppEnabled("msedge.exe")`가 false
  - `MusicApp_DefaultsOn`: `IsAppEnabled("Spotify.exe")`가 true
  - `ExplicitChoice_Wins`: `Apps["chrome.exe"]=true`이면 true
  - `RememberApp_AddsOnce`: 첫 호출은 true, 두 번째는 false
  - `Sanitize_FixesBadValues`: style "Z"→"A", color "red"→기본값, language "fr"→null, port 80→8976
  - `Load_MissingFile_ReturnsDefaults`, `Load_CorruptJson_ReturnsDefaults`(파일 내용 `{oops`)
  - `SaveThenLoad_RoundTrips`(임시 폴더 사용)
- [ ] **Step 2:** `dotnet test`를 실행한다. FAIL이 나와야 한다.
- [ ] **Step 3:** 구현한다. `System.Text.Json`을 쓰고, `Load`는 모든 예외를 잡아 기본값을 돌려준다. `Save`는 폴더를 만들고 임시 파일에 먼저 쓴 다음 이름을 바꾼다(쓰다가 꺼져도 파일이 깨지지 않게).
- [ ] **Step 4:** `dotnet test`를 실행한다. 모두 PASS여야 한다.
- [ ] **Step 5:** 커밋한다: `feat: settings model and store`

### Task 3: 언어 파일과 언어 고르기

**Files:**
- Create: `src/ObsNowPlaying/Localization/Strings.cs`, `src/ObsNowPlaying/wwwroot/i18n/ko.json`, `en.json`, `ja.json`
- Test: `tests/ObsNowPlaying.Tests/StringsTests.cs`

**Interfaces:**
- Produces:
  - `static string Strings.ResolveLanguage(string? setting, CultureInfo uiCulture)`: setting이 있으면 그것, 없으면 ko/ja면 그대로, 그 밖에는 "en"
  - `static IReadOnlyDictionary<string,string> Strings.Load(string lang)`: 포함된 리소스 `wwwroot/i18n/{lang}.json`을 읽는다.
  - 키 목록(세 파일이 똑같아야 함): `tray.openSettings, tray.copyWidgetUrl, tray.quit, tray.urlCopied, error.portInUse, settings.title, settings.style, settings.styleA, settings.styleB, settings.styleC, settings.colors, settings.textColor, settings.backgroundColor, settings.accentColor, settings.showArt, settings.showProgress, settings.apps, settings.appsHint, settings.appsEmpty, settings.language, settings.languageAuto, settings.widgetUrls, settings.copy, settings.copied, settings.save, settings.saved, settings.preview, settings.nothingPlaying`

- [ ] **Step 1: 실패하는 테스트 작성**
  - `Resolve_UsesSetting`: ("ja", en-US) → "ja"
  - `Resolve_FollowsWindows`: (null, ko-KR) → "ko", (null, ja-JP) → "ja", (null, de-DE) → "en"
  - `AllLanguages_HaveSameKeys`: 세 파일의 키 집합이 같고, 위 목록을 모두 담고 있다.
- [ ] **Step 2:** `dotnet test`를 실행한다. FAIL이 나와야 한다.
- [ ] **Step 3:** 구현하고 세 언어 문구를 쓴다. `settings.appsHint`는 "브라우저(크롬 등)는 영상 소리까지 잡혀서 처음엔 꺼져 있어요. 유튜브 뮤직을 브라우저로 쓰면 켜 주세요."라는 뜻을 담는다.
- [ ] **Step 4:** `dotnet test`를 실행한다. PASS여야 한다.
- [ ] **Step 5:** 커밋한다: `feat: ko/en/ja strings`

### Task 4: MediaWatcher + 웹 서버 + SSE

**Files:**
- Create: `src/ObsNowPlaying/Media/MediaWatcher.cs`, `src/ObsNowPlaying/Web/WebServer.cs`, `src/ObsNowPlaying/Web/SseHub.cs`, 임시 `wwwroot/widget.html`(곡 JSON만 출력)
- Modify: `src/ObsNowPlaying/Program.cs`(아직 트레이 없이 서버와 감시기만 켠다)
- Test: `tests/ObsNowPlaying.Tests/WebServerTests.cs`

**Interfaces:**
- Consumes: `SessionPicker.Pick`, `AppSettings`, `SettingsStore`
- Produces:
  - `MediaWatcher(Func<string,bool> isAppEnabled)`, `Task StartAsync()`, `event Action<NowPlaying>? Changed`, `event Action<string>? AppSeen`, `NowPlaying Current`, `byte[]? ArtBytes`, `string? ArtContentType`, `void Refresh()`(설정이 바뀌어 다시 골라야 할 때 부른다)
  - `WebServer(int port, Func<NowPlaying> current, Func<(byte[]?, string?)> art, SettingsHost settings)`, `void Start()`(포트가 사용 중이면 `HttpListenerException`을 던진다), `void Broadcast(string evt, object data)`
  - `SettingsHost`: `AppSettings Get()`, `void Update(AppSettings next)`(Sanitize → 저장 → `settings` 이벤트 방송 → watcher.Refresh), `IReadOnlyList<(string appId, bool enabled)> Apps()`
  - SSE 이벤트: `nowplaying`(NowPlaying JSON, camelCase), `settings`(AppSettings JSON). 연결 직후 두 이벤트를 한 번씩 보낸다. 15초마다 `: ping` 주석을 보낸다.
  - `ArtVersion`은 앨범 사진 바이트의 해시 앞 8자리다. 위젯은 `/art?v={ArtVersion}`을 쓴다.
- 구현 메모: GSMTC는 `CurrentSessionChanged`, `SessionsChanged`, 그리고 각 세션의 `MediaPropertiesChanged`, `PlaybackInfoChanged`, `TimelinePropertiesChanged`를 구독한다. 이벤트가 오면 전체를 다시 읽고 `SessionPicker.Pick`을 한다. 결과가 바뀌었을 때만 `Changed`를 부른다. 썸네일은 `Thumbnail.OpenReadAsync()`로 읽고, 실패하면 `ArtBytes=null`로 둔다. `LastPlayStarted`는 세션이 재생 상태로 바뀐 시각을 watcher가 기억한다.

- [ ] **Step 1: 실패하는 테스트 작성** (`WebServerTests`. 빈 포트를 골라 서버를 띄운다. 가짜 current/art를 쓴다.)
  - `Widget_ServesHtml`: GET `/widget` → 200, `text/html`
  - `UnknownPath_404`
  - `Art_None_404`: art가 (null,null)이면 404
  - `Settings_PostSanitizesAndSaves`: POST `/api/settings`에 `{"defaultStyle":"Z"}` → 200, 이어서 GET 결과의 `defaultStyle`이 "A"이고 파일이 생겼다
  - `Events_SendsInitialState`: GET `/events`의 첫 응답 조각에 `event: nowplaying`과 `event: settings`가 있다
  - `PortInUse_Throws`: 같은 포트로 두 번째 Start → 예외
- [ ] **Step 2:** `dotnet test`를 실행한다. FAIL이 나와야 한다.
- [ ] **Step 3:** WebServer, SseHub, SettingsHost를 구현한다. 리소스 이름 → Content-Type은 확장자 표로 정한다(html, css, js, json, woff2). 위젯 경로는 쿼리를 무시하고 `widget.html`을 준다.
- [ ] **Step 4:** `dotnet test`를 실행한다. PASS여야 한다.
- [ ] **Step 5:** MediaWatcher를 구현하고 Program.cs에서 연결한다.
- [ ] **Step 6: 실제 확인.** `dotnet run --project src/ObsNowPlaying`을 백그라운드로 실행한 뒤 `curl -N http://localhost:8976/events`를 연다. 스포티파이에서 재생 → 다음 곡 → 일시정지를 하면, 그때마다 `nowplaying` 이벤트의 title이나 playing이 바뀌어야 한다. `curl -I http://localhost:8976/art?v=...`는 200 image/* 이어야 한다.
- [ ] **Step 7:** 커밋한다: `feat: media watcher, web server and SSE`

### Task 5: 위젯 페이지 (3가지 모양) — frontend-design 스킬 사용

**Files:**
- Create/Replace: `src/ObsNowPlaying/wwwroot/widget.html`, `widget.css`, `widget.js`, `wwwroot/fonts/PretendardVariable.woff2`, `licenses/Pretendard-OFL.txt`

**Interfaces:**
- Consumes: SSE `nowplaying` / `settings`, `/art?v=`
- Produces: `widget.js` 안의 `render(state, settings)`. 설정 페이지 미리보기가 같은 파일을 iframe(`/widget?style=X&preview=1`)으로 쓴다. `preview=1`이면 재생 중인 곡이 없을 때 예시 곡을 보여주고 숨기지 않는다.

- [ ] **Step 1:** Pretendard Variable woff2와 OFL 라이선스를 공식 깃허브 릴리스(`orioncactus/pretendard`)에서 받아 넣는다. 받은 파일은 따로 만든 빈 폴더에 두고 확인한 다음 복사한다.
- [ ] **Step 2:** 시안을 바탕으로 A/B/C를 구현한다. 색은 CSS 변수(`--text`, `--bg`, `--accent`)로 설정에서 받는다. 배경은 투명하다. 글꼴은 `Pretendard, "Yu Gothic UI", "Microsoft YaHei UI", sans-serif` 순이다. 글자는 `textContent`로만 넣는다.
- [ ] **Step 3:** 동작을 넣는다.
  - `style` 쿼리가 없으면 `settings.defaultStyle`을 쓴다.
  - playing=false가 1500ms 이어지면 페이드 아웃한다.
  - 제목/가수가 넘치면 천천히 옆으로 흘러간다(`prefers-reduced-motion`이면 말줄임표로 대신한다).
  - 진행 막대는 `positionMs + (now - positionAtMs)`로 1초마다 계산한다. `durationMs`가 없거나 `showProgress=false`면 숨긴다.
  - `showArt=false` 또는 `artVersion=null`이면 사진 자리를 없앤다.
  - 곡이 바뀔 때만 짧은 전환 효과를 한 번 준다.
  - `EventSource`가 끊기면 브라우저가 스스로 다시 연결한다. 연결이 끊긴 동안에는 위젯을 숨긴다.
- [ ] **Step 4: 확인.** 브라우저 창에서 `/widget?style=A`, `B`, `C`를 각각 열어 스크린샷을 찍는다. 곡 넘김과 일시정지 반응을 본다. 테스트용 곡 제목에 `<b>x</b>`를 넣었을 때 글자 그대로 보이는지는 `render`를 콘솔에서 직접 불러 확인한다. exe를 껐다 켜서 위젯이 회복되는지도 확인한다.
- [ ] **Step 5:** 커밋한다: `feat: widget with card, pill and big-text styles`

### Task 6: 설정 페이지 + 트레이 + 단일 실행 — frontend-design 스킬 사용

**Files:**
- Create: `src/ObsNowPlaying/wwwroot/settings.html`, `settings.css`, `settings.js`, `src/ObsNowPlaying/Tray/TrayApp.cs`
- Modify: `src/ObsNowPlaying/Program.cs`

**Interfaces:**
- Consumes: `/api/settings`, `/api/apps`, `/i18n/{lang}.json`, SSE `settings`, `Strings.ResolveLanguage/Load`, `SettingsHost`
- Produces: 완성된 실행 흐름

- [ ] **Step 1:** 설정 페이지를 만든다. 들어갈 것: 언어 선택(자동/한/영/일), 기본 모양 A/B/C, 색 3개(`<input type=color>`), 앨범 사진·진행 막대 스위치, 표시할 앱 체크 목록(`/api/apps`, 감지된 앱이 없으면 `settings.appsEmpty`), 모양별 위젯 주소 3개와 복사 버튼, 미리보기 iframe, 저장 버튼. 값을 바꾸면 미리보기에 바로 반영된다(iframe에 `postMessage`로 임시 설정을 보낸다). 저장하면 POST하고 `settings.saved`를 보여준다. 모든 문구는 i18n 키로만 쓴다.
- [ ] **Step 2:** TrayApp을 만든다. 메뉴는 설정 열기(기본 브라우저로 `/settings`), 위젯 주소 복사(`/widget`을 클립보드에 넣고 풍선 알림), 종료다. 언어는 `ResolveLanguage`를 따르고, 설정에서 언어가 바뀌면 메뉴 글자도 바꾼다. 아이콘은 exe에 포함한 .ico를 쓴다.
- [ ] **Step 3:** Program.cs에서 단일 실행을 처리한다. 이름 있는 Mutex `Local\ObsNowPlaying`이 이미 있으면 설정 페이지를 열고 종료한다. 서버 Start가 `HttpListenerException`을 던지면 `error.portInUse`(포트 번호 포함) 메시지 상자를 띄우고 종료한다. AppSeen이 오면 `RememberApp`을 부르고, true면 저장한 뒤 설정 페이지에 `apps` 갱신을 알린다(`settings` 이벤트를 다시 보낸다).
- [ ] **Step 4: 확인.**
  - 설정 페이지를 ko/en/ja로 바꿔 가며 스크린샷을 찍는다.
  - 색과 모양을 바꾸고 저장하면, 따로 열어 둔 `/widget` 탭이 새로고침 없이 바뀌어야 한다.
  - 크롬 체크 해제 상태에서 크롬 유튜브 영상을 재생해도 위젯이 바뀌지 않아야 한다. 체크하면 바뀌어야 한다.
  - exe를 한 번 더 실행하면 설정 페이지만 열리고 두 번째 프로세스는 종료돼야 한다.
- [ ] **Step 5:** `dotnet test`가 모두 PASS인지 확인하고 커밋한다: `feat: settings page, tray menu and single instance`

### Task 7: 포장, 문서, OBS 실제 확인

**Files:**
- Create: `scripts/package.ps1`, `README.md`(영어), `README.ko.md`, `README.ja.md`, `LICENSE`(MIT, 저작자 Kimtan2)

- [ ] **Step 1:** `scripts/package.ps1`을 만든다. 하는 일: `dotnet publish src/ObsNowPlaying -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist/ObsNowPlaying`을 실행하고, README 3개·LICENSE·licenses/를 복사한 뒤 `dist/ObsNowPlaying-v0.1.0.zip`으로 묶는다.
- [ ] **Step 2:** README 3개를 쓴다. 내용: 받기와 실행, SmartScreen 경고에서 "추가 정보 → 실행", OBS에서 소스 추가 → 브라우저 → URL `http://localhost:8976/widget`(장면별 `?style=B` 등), 권장 크기(A 400×110, B 420×60, C 520×140), 설정 페이지, 브라우저가 처음에 꺼져 있는 이유, 같은 브라우저 안의 탭은 구분하지 못한다는 한계, 플레이어 지원 표(스포티파이 확인됨, 나머지는 실제로 확인한 것만 확인됨).
- [ ] **Step 3:** `scripts/package.ps1`을 실행한다. zip이 생기고, 압축을 푼 exe가 .NET SDK 경로와 상관없이 실행돼야 한다.
- [ ] **Step 4: OBS 실제 확인.** 압축을 푼 exe를 실행하고, OBS에 브라우저 소스(`http://localhost:8976/widget`)를 추가한다. 스포티파이로 재생 → 다음 곡 → 일시정지를 하며, OBS 화면에서 곡 표시, 즉시 갱신, 숨김을 스크린샷으로 확인한다. `?style=B`, `?style=C` 소스도 하나씩 추가해서 동시에 다른 모양이 나오는지 본다. 설정에서 색을 바꾸면 OBS에도 반영되는지 확인한다.
- [ ] **Step 5:** 이 PC에서 확인한 플레이어를 README 지원 표에 반영하고 커밋한다: `chore: packaging and docs for v0.1.0`
