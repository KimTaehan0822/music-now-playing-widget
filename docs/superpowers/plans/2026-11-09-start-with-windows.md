# Start with Windows Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 설정 페이지의 체크 하나로 Windows 로그인 시 프로그램이 자동 실행되게 한다.

**Architecture:** 새 클래스 `StartupEntry`가 HKCU Run 레지스트리 값 하나를 읽고/쓰고/고친다. WebServer에 `/api/startup` GET/POST를 추가하고, settings.json(AppSettings)에는 넣지 않는다. 설정 페이지는 이 API로 체크 상태를 읽고, 저장 버튼을 누를 때 함께 보낸다. Program은 시작할 때 `Repair()`로 경로를 맞춘다.

**Tech Stack:** .NET 8 WinForms, `Microsoft.Win32.Registry`, xUnit, 순수 JS.

**Spec:** `docs/superpowers/specs/2026-11-09-start-with-windows-design.md`

## Global Constraints

- 기본값은 꺼짐. Run 값이 없으면 꺼진 상태다. 프로그램이 스스로 값을 새로 만들지 않는다(사용자가 체크했을 때만).
- 레지스트리 키: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, 값 이름 `MusicNowPlayingWidget`, 값 `"<exe 전체 경로>"`(큰따옴표 포함).
- 체크 상태의 기준은 레지스트리다. AppSettings / settings.json에 필드를 추가하지 않는다.
- 레지스트리 쓰기가 실패해도 설정 저장은 성공한다. 응답에는 실제 상태를 돌려준다.
- POST는 기존 `/api/settings`와 같이 `IsLocalOrigin` 검사를 한다.
- 문구(ko 원문): 체크 "Windows 시작 시 자동 실행", 안내 "프로그램 폴더를 옮기면 자동 실행이 안 될 수 있어요. 옮긴 뒤에는 새 위치에서 한 번 직접 실행해 주세요."
- en: "Start with Windows" / "If you move the program folder, it may stop starting with Windows. After moving it, run it once from the new place."
- ja: "Windows の起動時に自動で実行" / "プログラムのフォルダーを移動すると、自動で起動しなくなることがあります。移動したら、新しい場所から一度手動で起動してください。"

## Review Focus

1. 경로에 공백이 있는 폴더(`C:\My Tools\...`) — 값을 따옴표로 감싸야 Windows가 실행한다. → Task 1 `Enable_QuotesPath`.
2. 폴더를 옮긴 뒤 새 위치에서 실행 — Run 값이 새 경로로 바뀌어야 한다. → Task 1 `Repair_RewritesMovedPath`.
3. 자동 실행이 꺼진 상태에서 실행 — `Repair()`가 값을 새로 만들면 안 된다. → Task 1 `Repair_DoesNothingWhenOff`.
4. 다른 사이트가 사용자의 브라우저에서 `/api/startup`에 POST — 403이어야 한다. → Task 2 `Startup_RejectsForeignOrigin`.
5. 잘못된 JSON 본문 — 400, 상태는 그대로. → Task 2 `Startup_BadJson_400`.

---

### Task 1: StartupEntry

**Files:**
- Create: `src/MusicNowPlayingWidget/Settings/StartupEntry.cs`
- Test: `tests/MusicNowPlayingWidget.Tests/StartupEntryTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class StartupEntry(string keyPath, string valueName, string exePath)` — 키는 항상 `Registry.CurrentUser` 아래.
  - `public static StartupEntry ForCurrentUser(string exePath)` — Global Constraints의 키·값 이름.
  - `public bool IsEnabled { get; }` — 값이 있으면 true.
  - `public bool Set(bool on)` — 켜면 `"\"" + exePath + "\""`를 쓰고, 끄면 값을 지운다(없어도 예외 없음). 예외는 삼키고 **실제 상태**(`IsEnabled`)를 돌려준다.
  - `public bool Repair()` — 값이 있고 지금 경로와 다르면(대소문자 무시) 고쳐 쓰고 true. 값이 없거나 같으면 false. 예외는 삼키고 false.

- [ ] **Step 1: 실패하는 테스트 작성** — 각 테스트는 `Software\MusicNowPlayingWidgetTests\<Guid>` 키를 쓰고 Dispose에서 `DeleteSubKeyTree`.
  - `StartsDisabled`: 새 키 → `IsEnabled == false`.
  - `Enable_QuotesPath`: exe `C:\My Tools\w.exe`, `Set(true)` → true, 레지스트리 값 == `"\"C:\\My Tools\\w.exe\""`.
  - `Disable_RemovesValue`: Set(true) 후 Set(false) → false, 값 없음. 값이 없을 때 Set(false)도 예외 없이 false.
  - `Repair_RewritesMovedPath`: 옛 경로로 Set(true) 후, 새 경로 `StartupEntry`의 `Repair()` → true, 값 == 새 경로(따옴표).
  - `Repair_DoesNothingWhenOff`: 값 없음 → `Repair()` false, 값은 여전히 없음.
  - `Repair_SamePath_NoWrite`: 같은 경로 → false.
- [ ] **Step 2: 실행해 실패 확인** — `dotnet test --filter StartupEntryTests` → 컴파일 오류(클래스 없음).
- [ ] **Step 3: 구현** — `Registry.CurrentUser.CreateSubKey(keyPath)` / `OpenSubKey(keyPath, writable)`.
- [ ] **Step 4: 실행해 통과 확인** — 6개 PASS.
- [ ] **Step 5: 커밋** `feat: StartupEntry keeps the Windows Run entry`

### Task 2: /api/startup 과 Program 연결

**Files:**
- Modify: `src/MusicNowPlayingWidget/Web/WebServer.cs` (생성자 끝에 `StartupEntry? startup = null` 추가, 라우트 2개)
- Modify: `src/MusicNowPlayingWidget/Program.cs` (서버 시작 전 `ForCurrentUser(Environment.ProcessPath!)` 만들고 `Repair()`, WebServer에 전달)
- Test: `tests/MusicNowPlayingWidget.Tests/WebServerTests.cs`

**Interfaces:**
- Consumes: Task 1 `StartupEntry`.
- Produces: `GET /api/startup` → `{"enabled":bool}`; `POST /api/startup` 본문 `{"enabled":bool}` → `Set()` 결과로 `{"enabled":bool}`. `startup`이 null이면 둘 다 404.

- [ ] **Step 1: 실패하는 테스트 작성** — 테스트 서버에 테스트 키 `StartupEntry`를 넘긴다(기존 테스트들은 그대로 통과해야 함).
  - `Startup_DefaultsOff`: GET → `{"enabled":false}`.
  - `Startup_PostOn_ThenGetOn`: POST `{"enabled":true}` → `{"enabled":true}`, 이어서 GET도 true.
  - `Startup_RejectsForeignOrigin`: `Origin: https://evil.example` POST → 403, GET 결과 false.
  - `Startup_BadJson_400`: 본문 `not json` → 400.
  - `Startup_NotInSettingsJson`: POST on 후 `GET /api/settings` 본문에 `startup`/`startWithWindows` 문자열 없음.
- [ ] **Step 2: 실패 확인** — 404 / 컴파일 오류.
- [ ] **Step 3: 구현** — JSON은 `JsonNode.Parse(...)?["enabled"]?.GetValue<bool>()`; 값이 없거나 bool이 아니면 400.
- [ ] **Step 4: 전체 테스트 통과 확인** — `dotnet test` 전부 PASS.
- [ ] **Step 5: 커밋** `feat: /api/startup and repair the Run path on launch`

### Task 3: 설정 페이지 체크와 문구

**Files:**
- Modify: `src/MusicNowPlayingWidget/wwwroot/settings.html` (앱 목록 fieldset 뒤에 새 `<fieldset>`: legend `settings.program`, `<label class="switch"><input type="checkbox" id="startWithWindows" aria-describedby="startWithWindowsHint">…`, `<p class="hint" id="startWithWindowsHint" data-i18n="settings.startWithWindowsHint">`)
- Modify: `src/MusicNowPlayingWidget/wwwroot/settings.js` (시작 시 GET으로 체크 채우기; submit에서 settings 저장 성공 후 POST `/api/startup`, 응답값으로 체크 다시 채우기; `readForm()`에는 넣지 않음)
- Modify: `src/MusicNowPlayingWidget/wwwroot/i18n/{ko,en,ja}.json` — 키 `settings.program`("프로그램" / "Program" / "プログラム"), `settings.startWithWindows`, `settings.startWithWindowsHint` (Global Constraints 문구)
- Modify: `tests/MusicNowPlayingWidget.Tests/StringsTests.cs` `RequiredKeys`에 위 3개 추가

- [ ] **Step 1:** RequiredKeys에 3개 추가 → `dotnet test --filter StringsTests` FAIL 확인.
- [ ] **Step 2:** i18n 3개 파일에 추가 → PASS.
- [ ] **Step 3:** html/js 수정, 빌드 후 앱을 다시 켜서 브라우저로 확인: 처음 체크 꺼짐 → 체크+저장 → `reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v MusicNowPlayingWidget`에 따옴표 경로 → 새로고침해도 체크 유지 → 끄고 저장 → 값 없음. 언어 3개에서 문구 확인.
- [ ] **Step 4: 커밋** `feat: Start with Windows checkbox on the settings page`

### Task 4: 설명서와 배포 파일

**Files:**
- Modify: `README.md`, `README.ko.md`, `README.ja.md` (기능 목록에 한 줄)
- Modify: `docs/manual/manual.{ko,en,ja}.html` (설정 표에 한 행: 체크 이름 + 안내 문구 뜻), PDF 3개 다시 출력(Noto Sans KR, 기존 방법)
- 실행: `scripts/package.ps1` (실행 중인 앱을 끄고 패키징 후 다시 켜고 시작 시각 확인)

- [ ] **Step 1:** 문서 수정, PDF 재출력, 페이지 수·글꼴 확인.
- [ ] **Step 2:** 패키징, zip 안에 PDF 3개 포함 확인, 앱 재시작 확인.
- [ ] **Step 3: 커밋** `docs: Start with Windows in READMEs and manuals`
