# Music Now Playing Widget

**English** | [한국어](README.ko.md) | [日本語](README.ja.md)

Shows the song you are listening to (title, artist, album art) on your stream.
It works with any player: Spotify, YouTube Music, Melon, Apple Music, TIDAL and more.
It reads the "now playing" info that Windows already knows. That is the same info that pops up when you press a volume key.

- Windows 10 (1809 or later) / Windows 11 only
- Nothing to install: just run one exe
- Three styles: card, pill and big text
- Change colors, what is shown, and the language (한국어/English/日本語) on a settings page

## How to use

1. Download `MusicNowPlayingWidget-v0.1.0.zip` and unzip it anywhere.
2. Double-click `MusicNowPlayingWidget.exe`.
   - **If a blue "Windows protected your PC" window appears**, click `More info` → `Run anyway`.
     The warning appears because this free, personal project has no paid Microsoft certificate.
   - On first launch the settings page opens in your browser. After that, open it from the green music-note icon at the bottom right of the taskbar.
3. On the settings page, under **URLs for OBS**, press `Copy` next to the style you want. You can also use tray icon → `Copy widget URL`.
   The address looks like `file:///C:/Users/.../MusicNowPlayingWidget/widget/widget.html?style=A`. It points to a widget file saved on your PC.
4. In OBS, choose `Add Source (+)` → `Browser`, and paste the address into the URL field.

   | Style | End of the address | Suggested width × height |
   |---|---|---|
   | Card | `?style=A` | 430 × 110 |
   | Pill | `?style=B` | 450 × 70 |
   | Big text | `?style=C` | 560 × 140 |

   Use a different address in each scene to get a different style per scene. The background is transparent.
5. Play some music and the widget appears. Pause and it fades out after a moment. Play again and it comes back.

> You can start OBS and this app in any order. The widget waits for the app and connects by itself.
> If you used `http://localhost:8976/widget` with the very first v0.1.0, switch to the new address above. The old one only works when the app is already running.

## Settings page

Tray icon → `Open settings` (or `http://localhost:8976/settings` in a browser)

- **Style**: the style used by widget URLs without `?style=`
- **Colors**: text, background and accent (progress bar and so on)
- **Change colors with each song**: when on, the widget fades to colors taken from the album art at every song change. Text always gets a readable color. Songs without album art use the colors above, and the big text style (C) only changes the accent.
- **Album art / progress bar / play time (1:12 / 3:20)** on or off
- **Apps to show**: every app that has played something shows up here. Untick one and its songs never appear on the widget.
- **Language**: automatic (Windows language), 한국어, English, 日本語

Saving updates your OBS widgets right away, with no refresh needed.

### Why browsers start turned off

Browsers such as Chrome, Edge and Whale also report YouTube videos as "playing". So they start turned off, to keep video titles off your stream.

Windows only reports which browser is playing, not which site. So if you turn Chrome on, everything you play in Chrome shows up, including live streams and videos, not just YouTube Music.

### Show only YouTube Music (without video or stream sound)

If you **install YouTube Music as a Chrome app**, Windows reports it separately from regular Chrome. Then you can turn on YouTube Music and leave Chrome off.

1. Open `music.youtube.com` in Chrome.
2. Click the **install icon** at the right end of the address bar (a monitor with a down arrow), then **Install**.
   If you don't see it, open the **⋮ menu at the top right → Cast, save, and share → Install page as app**.
3. Play a song in the new **YouTube Music window**. From now on, open it from the **YouTube Music icon** on your desktop or Start menu.
4. When **YouTube Music (Chrome app)** appears under Settings → Apps to show, turn it on, leave **Chrome** off, and **Save**.

Songs you play in the YouTube Music window then show up, even ones without album info such as covers, while streams and videos in regular Chrome tabs don't.

## Player status

| Player | Status |
|---|---|
| Spotify (desktop app) | ✅ Confirmed |
| YouTube Music (installed as a Chrome app) | ✅ Confirmed |
| Melon (desktop app) | ⏳ Not confirmed yet |
| Apple Music (Windows app) | ⏳ Not confirmed yet |
| TIDAL (desktop app) | ⏳ Not confirmed yet |

Any player that publishes to Windows' "now playing" info should work. If the song shows up when you press a volume key, it will almost certainly work.
Tell us how it went and we'll update this table.

## Troubleshooting

- **"Port 8976 is already used by another program"**:
  Open `%APPDATA%\MusicNowPlayingWidget\settings.json` in Notepad and change the number in `"port": 8976` (for example to 8977). Then restart the app. The file address in OBS can stay as it is.
- **Use `localhost`, not `127.0.0.1`.** Without admin rights, Windows only lets the app answer on `localhost`.
- **The widget doesn't show**: check that music is actually playing, and that the player is turned on under Settings → Apps to show.
- **Something looks off**: tray icon → `Restart`. OBS widgets reconnect by themselves.
- **Quit**: tray icon → `Quit`

## License

MIT. Licenses for bundled components are in the `licenses` folder.
- Pretendard font: SIL Open Font License 1.1 (`licenses/Pretendard-OFL.txt`)
- .NET runtime: MIT (`licenses/dotnet-LICENSE.txt`, `licenses/dotnet-THIRD-PARTY-NOTICES.txt`)

## Notice

This is a free program made by an individual. It is not affiliated with, made by, or endorsed by the OBS Project, Spotify, Google (YouTube Music), Kakao Entertainment (Melon), Apple or TIDAL.
OBS, Spotify, YouTube Music, Melon, Apple Music and TIDAL are trademarks of their respective owners.
