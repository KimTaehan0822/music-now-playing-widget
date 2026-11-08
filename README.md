# OBS Now Playing widget

**English** | [한국어](README.ko.md) | [日本語](README.ja.md)

Shows the song you are listening to (title, artist, album art) on your stream.
It works with any player: Spotify, YouTube Music, Melon, Apple Music, TIDAL and more.
It reads the "now playing" info that Windows already knows. That is the same info that pops up when you press a volume key.

- Windows 10 (1809 or later) / Windows 11 only
- Nothing to install: just run one exe
- Three styles: card, pill and big text
- Change colors, what is shown, and the language (한국어/English/日本語) on a settings page

## How to use

1. Download `ObsNowPlaying-v0.1.0.zip` and unzip it anywhere.
2. Double-click `ObsNowPlaying.exe`.
   - **If a blue "Windows protected your PC" window appears**, click `More info` → `Run anyway`.
     The warning appears because this free, personal project has no paid Microsoft certificate.
   - On first launch the settings page opens in your browser. After that, open it from the green music-note icon at the bottom right of the taskbar.
3. In OBS, choose `Add Source (+)` → `Browser`, and put this in the URL field:

   ```
   http://localhost:8976/widget
   ```

   | Style | URL | Suggested width × height |
   |---|---|---|
   | The default style from settings | `http://localhost:8976/widget` | depends on style |
   | Card | `http://localhost:8976/widget?style=A` | 430 × 110 |
   | Pill | `http://localhost:8976/widget?style=B` | 450 × 70 |
   | Big text | `http://localhost:8976/widget?style=C` | 560 × 140 |

   Use a different URL in each scene to get a different style per scene. The background is transparent.
4. Play some music and the widget appears. Pause and it fades out after a moment. Play again and it comes back.

> It's fine to start this app after OBS. The widget reconnects by itself.

## Settings page

Tray icon → `Open settings` (or `http://localhost:8976/settings` in a browser)

- **Style**: the style used by widget URLs without `?style=`
- **Colors**: text, background and accent (progress bar and so on)
- **Album art / progress bar** on or off
- **Apps to show**: every app that has played something shows up here. Untick one and its songs never appear on the widget.
- **Language**: automatic (Windows language), 한국어, English, 日本語

Saving updates your OBS widgets right away, with no refresh needed.

### Why browsers start turned off

Browsers such as Chrome, Edge and Whale also report YouTube videos as "playing". So they start turned off, to keep video titles off your stream.
**If you listen to YouTube Music in a browser**, turn that browser on under Settings → Apps to show.

Tabs inside the same browser can't be told apart, so a YouTube Music tab and a regular YouTube tab count as the same app. Windows only reports which browser is playing, not which tab.

## Player status

| Player | Status |
|---|---|
| Spotify (desktop app) | ✅ Confirmed |
| YouTube Music (browser) | ⏳ Not confirmed yet |
| Melon (desktop app) | ⏳ Not confirmed yet |
| Apple Music (Windows app) | ⏳ Not confirmed yet |
| TIDAL (desktop app) | ⏳ Not confirmed yet |

Any player that publishes to Windows' "now playing" info should work. If the song shows up when you press a volume key, it will almost certainly work.
Tell us how it went and we'll update this table.

## Troubleshooting

- **"Port 8976 is already used by another program"**:
  Open `%APPDATA%\ObsNowPlaying\settings.json` in Notepad and change the number in `"port": 8976` (for example to 8977). Change the number in your OBS URL to match.
- **The widget doesn't show**: check that music is actually playing, and that the player is turned on under Settings → Apps to show.
- **Quit**: tray icon → `Quit`

## License

MIT. The bundled Pretendard font is under the SIL Open Font License 1.1 (`licenses/Pretendard-OFL.txt`).
