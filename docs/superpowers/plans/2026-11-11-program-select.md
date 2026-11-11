# 실행 중인 프로그램 선택 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 설정의 "표시할 앱" 목록에서 실행 중인 프로그램을 직접 골라 "Windows 곡 정보" 또는 "창 제목" 방식으로 위젯에 띄우고, 배포 exe의 메모리 사용량을 줄인다.

**Architecture:** 설정에 `Programs`(프로세스 이름 → 켜짐, 방식, 순서)를 더한다. "Windows 곡 정보" 줄은 그 앱이 처음 Windows에 잡힐 때 기존 `Apps`로 합쳐진다. "창 제목" 줄은 새 `TitleWatcher`가 1초마다 창 제목과 Windows 오디오 세션을 읽어 별도의 `NowPlaying`을 만들고, `Program`이 Windows 쪽 곡을 우선해 둘 중 하나를 방송한다.

**Tech Stack:** C# .NET 8 WinForms, `System.Diagnostics.Process`, Core Audio COM(직접 선언, 새 패키지 없음), xUnit, 바닐라 JS 설정 페이지.

**Spec:** `docs/superpowers/specs/2026-11-11-program-select-design.md`

## Global Constraints
- 새 NuGet 패키지 금지. Core Audio는 `[ComImport]` 인터페이스로 직접 호출한다.
- 기존 `Apps` 필드와 그 의미는 바꾸지 않는다. 예전 settings.json은 `Programs`가 빈 목록으로 읽혀야 한다.
- 창 제목 방식 줄이 하나도 켜져 있지 않으면 타이머가 돌지 않는다(지금과 같은 사용량).
- 설정 페이지로 창 제목을 보내지 않는다. `/api/programs`는 프로세스 이름만 준다.
- 화면 문구는 한국어·영어·일본어 3개(아래 Task 5 표의 문구 그대로).
- 설정 페이지 작업은 frontend-design 스킬을 따른다. 기존 settings.css의 모양을 이어 쓴다.
- 테스트 명령: `dotnet test tests\MusicNowPlayingWidget.Tests` (지금 82개 통과).

## Review Focus
1. 곡 제목 자체에 ` - `가 들어간 경우(`Song - Remastered - Artist`): "제목 - 아티스트" 순서는 **마지막** ` - `에서, "아티스트 - 제목" 순서는 **첫** ` - `에서 나눈다. → Task 2 테스트.
2. 음악 앱이 기본 출력 장치가 아닌 장치(오디오 인터페이스, 가상 믹서)로 소리를 내는 경우: 활성 출력 장치 **전부**의 오디오 세션을 본다. → Task 4 수동 확인.
3. 같은 이름의 프로세스가 여러 개(Electron 자식 프로세스)인 경우: 이름이 같은 모든 PID 중 하나라도 소리를 내면 재생 중. → Task 4 수동 확인.
4. 설정 페이지를 연 채 "Windows 곡 정보" 줄이 서버에서 합쳐진 경우: 저장하지 않은 변경이 없으면 페이지가 서버 목록으로 다시 그려 줄이 사라지고 위 목록에 나타난다. → Task 5 브라우저 확인.
5. 창을 닫았거나 프로그램을 종료한 경우: 다음 1초 안에 창 제목 곡이 사라지고 Windows 쪽 상태로 돌아간다. → Task 4 수동 확인.

---

### Task 1: 설정에 `Programs` 추가와 합치기 규칙

**Files:**
- Modify: `src/MusicNowPlayingWidget/Settings/AppSettings.cs`
- Create: `src/MusicNowPlayingWidget/Settings/ProgramEntry.cs`
- Create: `src/MusicNowPlayingWidget/Media/ProgramName.cs`
- Test: `tests/MusicNowPlayingWidget.Tests/AppSettingsTests.cs`, `tests/MusicNowPlayingWidget.Tests/SettingsHostTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class ProgramEntry { bool Enabled = true; string Mode = "windows"; string Order = "title-artist"; }` (get/set 속성). 상수 `ProgramEntry.Windows = "windows"`, `Title = "title"`, `TitleArtist = "title-artist"`, `ArtistTitle = "artist-title"`.
  - `AppSettings.Programs : Dictionary<string, ProgramEntry>` (키 = 프로세스 이름, `.exe` 없음, 대소문자 무시). JSON 이름 `programs`.
  - `static class ProgramName { string Normalize(string s); bool Matches(string programName, string appId); }` — Normalize는 글자·숫자만 남기고 소문자. Matches는 정규화한 프로그램 이름이 비어 있지 않고, 정규화한 앱 ID에 포함되면 참.
  - `AppSettings.IsAppEnabled(appId)`: `Apps` → 이름이 맞는 "windows" 방식 `Programs` 줄의 `Enabled` → 기존 기본값(브라우저 아니면 켬) 순서.
  - `AppSettings.RememberApp(appId)`: 이미 있으면 false. 없으면 이름이 맞는 "windows" 방식 `Programs` 줄이 있을 때 그 `Enabled`로 `Apps`에 넣고 그 줄을 `Programs`에서 지운다. 없으면 기존 기본값.

- [ ] **Step 1: 실패하는 테스트 작성** (`AppSettingsTests`, `SettingsHostTests`)
  - `ProgramName_Matches_IgnoresCaseSpacesAndDots`: `Matches("Melon Player", "com.kakaoent.melon.player")` 참, `Matches("Spotify", "Spotify.exe")` 참, `Matches("foobar2000", "Spotify.exe")` 거짓, `Matches("...", "anything")` 거짓.
  - `Sanitize_FixesBadProgramValues`: `Mode = "x"`, `Order = "y"` → `"windows"`, `"title-artist"`. `Programs = null` → 빈 사전. 키 대소문자 무시(`Programs.ContainsKey("FOOBAR2000")`).
  - `RememberApp_MergesPreselectedWindowsRow`: `Programs["Melon Player"] = { Enabled = false, Mode = "windows" }` → `RememberApp("com.kakaoent.melon.player")` 후 `Apps[...] == false`, `Programs`에 그 키 없음.
  - `RememberApp_KeepsTitleRows`: 같은 이름이라도 `Mode = "title"`이면 `Programs`에 남고 `Apps`는 기본값(true).
  - `IsAppEnabled_UsesPreselectedRowBeforeSeen`: `Apps` 비어 있고 "windows" 줄 `Enabled = false` → `IsAppEnabled("com.kakaoent.melon.player") == false`.
  - `SettingsHostTests.Programs_RoundTripThroughStore`: `Merge("{\"programs\":{\"foobar2000\":{\"enabled\":true,\"mode\":\"title\",\"order\":\"artist-title\"}}}")` → 새 `SettingsStore(같은 경로).Load().Programs["foobar2000"].Order == "artist-title"`.
  - `SettingsHostTests.OldSettingsFile_LoadsEmptyPrograms`: `{"port":8976}`만 쓴 파일 → `Load().Programs`가 비어 있음(null 아님).

- [ ] **Step 2: 실행해서 실패 확인**
  Run: `dotnet test tests\MusicNowPlayingWidget.Tests --filter "FullyQualifiedName~AppSettingsTests|FullyQualifiedName~SettingsHostTests"`
  Expected: 컴파일 오류(`ProgramEntry`, `ProgramName` 없음).

- [ ] **Step 3: 구현** — 위 Interfaces 그대로. `Sanitize`에서 `Programs`를 대소문자 무시 사전으로 새로 만들고 각 값의 Mode/Order를 고친다. null 값 항목은 버린다.

- [ ] **Step 4: 전체 테스트 통과 확인**
  Run: `dotnet test tests\MusicNowPlayingWidget.Tests`
  Expected: 기존 82개 + 새 테스트 모두 통과.

- [ ] **Step 5: 커밋** — `feat: remember picked programs in settings`

---

### Task 2: 창 제목 나누기와 조용함 판정 (순수 로직)

**Files:**
- Create: `src/MusicNowPlayingWidget/Media/WindowTitle.cs`
- Create: `src/MusicNowPlayingWidget/Media/SilenceTimer.cs`
- Test: `tests/MusicNowPlayingWidget.Tests/WindowTitleTests.cs`

**Interfaces:**
- Produces:
  - `static class WindowTitle { (string Title, string Artist)? Parse(string? windowTitle, string programName, bool artistFirst); }`
  - `sealed class SilenceTimer(TimeSpan grace) { bool IsPlaying(bool? audible, bool hasTitle, DateTimeOffset now); }` — 기본 grace는 호출하는 쪽에서 3초.

규칙(Parse):
1. 앞뒤 공백 제거. 끝에 ` - <프로그램 이름>` 또는 ` — <프로그램 이름>`이 있으면 뗀다(`ProgramName.Normalize`로 비교).
2. 남은 것이 비었거나, 정규화했을 때 프로그램 이름과 같으면 `null`.
3. ` - `가 없으면 `(전체, "")`.
4. `artistFirst == false`: **마지막** ` - `에서 나눠 앞 = 제목, 뒤 = 아티스트. `true`: **첫** ` - `에서 나눠 앞 = 아티스트, 뒤 = 제목.

규칙(SilenceTimer): `audible == true`면 마지막 소리 시각을 `now`로 두고 참. `audible == false`면 마지막 소리 시각이 있고 `now - 그 시각 < grace`일 때만 참. `audible == null`(오디오를 못 읽음)이면 `hasTitle`.

- [ ] **Step 1: 실패하는 테스트 작성**
  - `Parse_TitleArtist`: `("Finest Thing - RESCENE (리센느)", "Melon", false)` → `("Finest Thing", "RESCENE (리센느)")`.
  - `Parse_ArtistTitle`: `("Lil Baby - Dead Fresh", "x", true)` → `("Dead Fresh", "Lil Baby")`.
  - `Parse_SplitsAtLastDash_ForTitleFirst`: `("Song - Remastered - Artist", "x", false)` → `("Song - Remastered", "Artist")`.
  - `Parse_SplitsAtFirstDash_ForArtistFirst`: `("Artist - Song - Live", "x", true)` → `("Song - Live", "Artist")`.
  - `Parse_StripsTrailingProgramName`: `("Song - Artist - foobar2000", "foobar2000", false)` → `("Song", "Artist")`.
  - `Parse_NoSeparator_IsWholeTitle`: `("Just a title", "x", false)` → `("Just a title", "")`.
  - `Parse_ProgramNameOnly_IsNull`: `("TIDAL", "TIDAL", false)` → null. `("", "x", false)`와 `(null, "x", false)` → null.
  - `Silence_StaysPlayingWithinGrace`: grace 3초. t0 audible true → 참, t0+2초 false → 참, t0+3초 false → 거짓.
  - `Silence_NeverHeard_IsNotPlaying`: 처음부터 false → 거짓.
  - `Silence_UnknownAudio_FollowsTitle`: null + hasTitle true → 참, null + false → 거짓.

- [ ] **Step 2: 실패 확인** — `dotnet test tests\MusicNowPlayingWidget.Tests --filter FullyQualifiedName~WindowTitleTests` → 컴파일 오류.
- [ ] **Step 3: 구현** — 위 규칙대로.
- [ ] **Step 4: 전체 테스트 통과 확인** — `dotnet test tests\MusicNowPlayingWidget.Tests` → 모두 통과.
- [ ] **Step 5: 커밋** — `feat: parse song info from window titles`

---

### Task 3: Windows 곡 우선 규칙

**Files:**
- Modify: `src/MusicNowPlayingWidget/Media/SessionPicker.cs`
- Test: `tests/MusicNowPlayingWidget.Tests/SessionPickerTests.cs`

**Interfaces:**
- Produces: `public static NowPlaying Prefer(NowPlaying windows, NowPlaying title)` — `windows.Playing`이면 `windows`, 아니면 `title.Playing`이면 `title`, 둘 다 아니면 `windows`.

- [ ] **Step 1: 실패하는 테스트** — `Prefer_WindowsPlayingWins`, `Prefer_TitleWhenWindowsIdle`, `Prefer_EmptyWhenBothIdle`(결과가 `NowPlaying.Empty`와 같음).
- [ ] **Step 2: 실패 확인** — `--filter FullyQualifiedName~SessionPickerTests` → 컴파일 오류.
- [ ] **Step 3: 구현.**
- [ ] **Step 4: 전체 테스트 통과.**
- [ ] **Step 5: 커밋** — `feat: prefer Windows media over window titles`

---

### Task 4: 창 제목 감시, 오디오 확인, 실행 중인 프로그램 목록, 연결

**Files:**
- Create: `src/MusicNowPlayingWidget/Media/AudioActivity.cs`
- Create: `src/MusicNowPlayingWidget/Media/RunningPrograms.cs`
- Create: `src/MusicNowPlayingWidget/Media/TitleWatcher.cs`
- Modify: `src/MusicNowPlayingWidget/Web/WebServer.cs`, `src/MusicNowPlayingWidget/Program.cs`
- Test: `tests/MusicNowPlayingWidget.Tests/WebServerTests.cs`

**Interfaces:**
- Consumes: `ProgramEntry`, `ProgramName`(Task 1), `WindowTitle`, `SilenceTimer`(Task 2), `SessionPicker.Prefer`(Task 3).
- Produces:
  - `static class AudioActivity { HashSet<int>? AudiblePids(); }` — 활성(`DEVICE_STATE_ACTIVE`) **출력 장치 전부**의 세션을 돌며 `IAudioSessionControl2.GetProcessId`와 `IAudioMeterInformation.GetPeakValue() > 0.0001f`인 PID를 모은다. COM 오류가 나면 `null`. COM 객체는 쓰고 나서 `Marshal.ReleaseComObject`로 놓는다.
  - `static class RunningPrograms { IReadOnlyList<string> List(); }` — `MainWindowHandle != 0`인 프로세스 이름, 이 프로그램 자신 제외, 중복 없이(대소문자 무시) 이름순.
  - `sealed class TitleWatcher : IDisposable { TitleWatcher(); NowPlaying Current; event Action<NowPlaying> Changed; void Update(AppSettings s); }`
    - `Update`: 켜진 "title" 방식 줄이 있으면 1초 `System.Threading.Timer`를 켜고, 없으면 끄고 `Current`를 `Empty`로 바꾼다(바뀌면 `Changed`).
    - 틱마다: 켜진 "title" 줄마다 `Process.GetProcessesByName(이름)`의 `MainWindowTitle` 중 `WindowTitle.Parse`가 null이 아닌 첫 값을 쓴다. 소리는 한 틱에 `AudiblePids()`를 한 번만 부르고, 그 이름의 PID 중 하나라도 들어 있으면 audible(전체가 null이면 null). 프로그램마다 `SilenceTimer(TimeSpan.FromSeconds(3))`. 재생 중인 첫 줄(이름순)로 `new NowPlaying(true, 제목, 아티스트, "", 프로세스 이름, null, null, null, 0, null)`. 없으면 `Empty`. 얻은 `Process` 객체는 Dispose한다. 틱이 겹치지 않게 한다.
  - `WebServer` 생성자에 마지막 선택 인자 `Func<IReadOnlyList<string>>? programs = null`. `GET /api/programs`(programs가 있을 때) → 이름 배열 JSON. CORS 헤더는 붙이지 않는다.
  - `Program`: `TitleWatcher titles`를 만들고 `titles.Update(settings.Get())`. `settings.Changed`에서 `titles.Update(s)`. 방송은 하나로 모은다: `NowPlaying Combined() => SessionPicker.Prefer(watcher.Current, titles.Current)`. `watcher.Changed`와 `titles.Changed` 둘 다에서 `Combined()`를 계산해 마지막으로 보낸 값과 다를 때만 `server.Broadcast("nowplaying", …)`(lock으로 보호). `WebServer`의 `current` 인자는 `Combined`, `programs` 인자는 `RunningPrograms.List`.

- [ ] **Step 1: 실패하는 테스트** — `WebServerTests.Programs_ReturnsInjectedNames`: `programs: () => ["foobar2000", "Melon"]`로 서버를 띄우고 `GET api/programs` → `["foobar2000","Melon"]`. 기존 테스트 생성자 헬퍼를 넓혀 쓴다.
- [ ] **Step 2: 실패 확인** — `--filter FullyQualifiedName~WebServerTests` → 컴파일 오류.
- [ ] **Step 3: 구현** — 위 Interfaces 그대로.
- [ ] **Step 4: 전체 테스트 통과** — `dotnet test tests\MusicNowPlayingWidget.Tests`.
- [ ] **Step 5: 실제 프로그램으로 확인** (자동 테스트 불가 부분)
  - 테스트용 창: scratchpad에 PowerShell 스크립트를 만들어, 제목이 `Test Song - Test Artist`인 WinForms 창을 띄우고 `System.Media.SoundPlayer`로 wav를 반복 재생한다(프로세스 이름 `powershell`). wav는 `C:\Windows\Media`의 파일을 쓴다.
  - Release로 빌드한 앱을 실행(기존 앱 종료 후)하고, `settings.json`의 `programs`에 `"powershell": {"enabled":true,"mode":"title","order":"title-artist"}`를 `/api/settings` POST로 넣는다.
  - 확인할 것: `/events`의 nowplaying이 `Test Song`/`Test Artist`로 바뀜. 소리를 멈추면 3초쯤 뒤 `playing:false`. 창을 닫으면 1초 안에 사라짐. 그동안 Spotify 등 Windows 곡을 틀면 그쪽이 우선. 줄을 끄면 앱의 1초 타이머가 멈춤(스레드 수/CPU로 확인).
  - 끝나면 `programs`에서 테스트 줄을 지우고, 사용자의 dist 앱으로 되돌려 실행한다(자동 실행 경로가 dist를 가리키는지 확인).
- [ ] **Step 6: 커밋** — `feat: show songs from window titles of picked programs`

---

### Task 5: 설정 페이지 — 프로그램 추가 줄

**Files:**
- Modify: `src/MusicNowPlayingWidget/wwwroot/settings.html`, `settings.js`, `settings.css`, `i18n/ko.json`, `i18n/en.json`, `i18n/ja.json`
- Test: `tests/MusicNowPlayingWidget.Tests/StringsTests.cs` (`RequiredKeys`에 새 키 추가)

**Interfaces:**
- Consumes: `GET /api/programs`(Task 4), 설정의 `programs`(Task 1, JSON 이름 `programs`, 값 `{enabled, mode, order}`).

화면:
- `#apps` 목록 아래에 버튼 `#addProgram`. 누르면 `/api/programs`를 받아 그 아래 펼침 목록을 보여 준다. 이미 `programs`에 있는 이름은 빼고 보여 준다. 비어 있으면 `settings.programsNone`. 하나를 누르면 `{enabled:true, mode:"windows", order:"title-artist"}` 줄이 추가되고 펼침이 닫힌다.
- 프로그램 줄(`#programs` 목록, `#apps` 바로 아래): 체크박스, 이름, 방식 `<select>`(windows/title), title일 때만 순서 `<select>`, 삭제 버튼. 방식이 windows면 안내 `settings.windowsModeHint`, title이면 `settings.titleModeHint`.
- `readForm()`이 `programs` 객체를 함께 보낸다(서버 Merge가 통째로 바꾸므로 삭제도 반영됨).
- 서버 `settings` 이벤트: 페이지에서 프로그램 줄을 건드리지 않았으면(`programsDirty == false`) 서버 값으로 다시 그린다. 건드렸으면 페이지 값을 유지한다. 저장에 성공하면 `programsDirty = false`.

문구(키 / ko / en / ja):
| 키 | ko | en | ja |
|---|---|---|---|
| `settings.addProgram` | 실행 중인 프로그램 추가 | Add a running program | 実行中のプログラムを追加 |
| `settings.programsNone` | 창이 열린 프로그램이 없어요. | No programs with an open window. | ウィンドウが開いているプログラムがありません。 |
| `settings.modeWindows` | Windows 곡 정보 | Windows media info | Windows の曲情報 |
| `settings.modeTitle` | 창 제목 | Window title | ウィンドウのタイトル |
| `settings.orderTitleArtist` | 제목 - 아티스트 | Title - Artist | 曲名 - アーティスト |
| `settings.orderArtistTitle` | 아티스트 - 제목 | Artist - Title | アーティスト - 曲名 |
| `settings.windowsModeHint` | 이 프로그램에서 곡을 틀면 위 목록으로 합쳐져요. | Once this program plays a song, it joins the list above. | このプログラムで曲を再生すると、上のリストにまとめられます。 |
| `settings.titleModeHint` | 앨범 사진과 재생 시간은 표시되지 않아요. | Album art and playback time won't show. | アルバム写真と再生時間は表示されません。 |
| `settings.removeProgram` | 삭제 | Remove | 削除 |

- [ ] **Step 1: 실패하는 테스트** — `StringsTests.RequiredKeys`에 위 9개 키 추가.
- [ ] **Step 2: 실패 확인** — `--filter FullyQualifiedName~StringsTests` → 키 없음으로 실패.
- [ ] **Step 3: 구현** — frontend-design 스킬을 읽고, 기존 `.apps` 줄 모양에 맞춘다. 좁은 폭(360px)에서도 한 줄이 넘치지 않게 select가 줄바꿈되도록.
- [ ] **Step 4: 전체 테스트 통과.**
- [ ] **Step 5: 브라우저 확인** — 앱을 Release로 다시 켜고 내장 브라우저로 `http://localhost:8976/settings`를 연다. 추가 → 줄 생김 → 방식 바꾸면 순서·안내가 바뀜 → 저장 → 새로고침해도 유지 → 삭제 → 저장 후 사라짐. 3개 언어로 바꿔 문구 확인. 360px 폭 스크린숏. Review Focus 4: 저장하지 않은 상태에서 서버가 줄을 합치면 다시 그려지는지 `/api/settings` POST로 흉내 내 확인.
- [ ] **Step 6: 커밋** — `feat: pick running programs on the settings page`

---

### Task 6: 메모리 줄이기와 문서

**Files:**
- Modify: `scripts/package.ps1` (`-p:EnableCompressionInSingleFile=true` → `false`)
- Modify: `README.md`, `README.ko.md`, `README.ja.md`, `docs/manual/manual.ko.html`, `manual.en.html`, `manual.ja.html`
- Regenerate: `docs/manual/*.pdf` 3개

- [ ] **Step 1: 배포 설정 변경 후 빌드** — 앱 종료 → `powershell -ExecutionPolicy Bypass -File scripts\package.ps1` → 앱 다시 실행.
  Expected: exe 약 181MB. zip 크기를 기록한다.
- [ ] **Step 2: 메모리 확인** — 실행 45초 뒤 `PrivateMemorySize64`가 약 40MB 안팎(압축 배포판 약 83MB 대비 절반 이하).
- [ ] **Step 3: 문서** — README 3개의 "표시할 앱" 설명에 "실행 중인 프로그램 추가"와 두 방식, 창 제목 방식의 한계(앨범 사진·재생 시간 없음, 창 제목에 곡 이름을 넣는 프로그램만)를 더한다. 설명서 HTML 3개도 같은 내용. pdf 스킬을 따르고 PDF는 headless Chrome(`--user-data-dir=<scratchpad>\chrome-prof`)으로 다시 만든다. 글꼴 Noto Sans KR(일본어 전용 한자는 Noto Sans JP). 쪽 수와 글꼴을 확인한다.
- [ ] **Step 4: 전체 테스트 통과** — `dotnet test tests\MusicNowPlayingWidget.Tests`.
- [ ] **Step 5: 커밋** — `perf: ship the exe uncompressed to halve memory` 와 `docs: picking running programs` (두 커밋).
