# BronxSoft Netmetar — Changelog

## Merač 0.1.1 — 2026-11-16

### Added
- An icon of its own: `assets/merac.ico` (7 sizes, PNG inside), made by `assets/napravi-ikonicu.ps1` — the same family as the main icon (cream body, dark green screen) with three measuring bars instead of the arrows, so the helper is recognisable in Task Manager and on shortcuts.

### Changed
- The helper is a Windows application now (`WinExe`): no console window appears when the scheduled task starts it.

## v0.59.45-beta — 2026-11-15

### Added
- Exact traffic per program: a small helper `BronxSoft Netmetar Merac.exe` (own project in `Merac/`) opens one ETW kernel session for `Microsoft-Windows-Kernel-Network`, sums the transfer events per PID (TCP 10/11, 26/27 and UDP 42/43, 58/59; retransmissions are ignored) and sends `T;pid=down,up;…` to the main program every second over a named pipe.
- The main program listens on that pipe (`MeracPrograma`, `Mereni.cs`), maps PIDs to program names, keeps today's totals per program in memory and writes them to new columns `bajtova_down`/`bajtova_up` of `programi_dan` once a minute (older databases are migrated with `ALTER TABLE`).
- The connections dialog shows "▼ … ▲ … TODAY … GB" of that program when the meter is running.
- Options: "Exact traffic per program" (off by default). Turning it on starts the helper — silently when the scheduled task exists, otherwise once with UAC; turning it off stops it. The helper exits by itself when the main program closes.
- Helper switches: `--install` / `--remove` (scheduled task, highest rights), `--test` (invented numbers, no rights), `--debug` (first events to `merac.log`).
- Main program switch `--programi F` writes today's exact bytes per program to F (used for checking without the window).

### Changed
- The main project excludes `Merac/**` from its own compilation; the helper is published next to the main exe.

## v0.57.45-beta — 2026-11-15

### Added
- A click on a program (anywhere in its row except the Watch circle, on both the live screen and the full program list) opens the program's connections: the ports it listens on ("SLUŠA · PORT 51413") and every open connection as address:port, with IPv4/IPv6 marked. No DNS names are looked up. The dialog is a snapshot with the time it was taken (`VezeDijalog`, `Merenje.Pojedinosti`).
- A small dot after the name marks a program that listens on a port, so it can accept connections (server); programs without the dot only connect out (client).

### Changed
- The TCP table is read once per refresh and decodes both established connections and listening sockets (`Merenje.TcpTabela`); ports are converted from network byte order (`Port`). The old count-only helper is gone.
- The program list also shows programs that only listen (with 0 links), so a server without peers is still visible.
- `--snimak` writes `veze.png` for the program with the most connections.

## v0.55.45-beta — 2026-11-15

### Changed
- MAX now writes its unit too ("MAX 13.9 MBIT/S · 10:46"). The same number means different things in KB/s, MB/s and Mbit/s, and in KB/s mode the value switches to MB/s above 999.5 by itself, so a bare number under the graph was ambiguous.

### Added
- `NM_DIAG=1` in the environment writes one line per second to `greske.log` with the raw speeds, the link speed and the percentage (`diag down=… up=… link=… pct=…`), so the arithmetic can be checked without screenshots.

## v0.55.44-beta — 2026-11-15

### Added
- Link speed in the header, read from the adapter (`TransmitLinkSpeed`/`ReceiveLinkSpeed` via offsets in `Merenje`): "1 GBIT". If it falls below the fastest speed seen since the start (a bad cable drops 1 Gbit to 100 Mbit), the whole header text turns red.
- Link utilisation next to DOWNLOAD ("12% VEZE"): current down plus up against the negotiated link speed; adapter errors (`InErrors`/`OutErrors`, counted from the program start) are appended when there are any.
- TRANSFER in the header on the left: while more than 5 MB/s is moving, it shows how many GB have moved and how long it lasts; it ends after five seconds below 2.5 MB/s.

### Changed
- `Merac` keeps the link speed, the fastest link seen and the adapter errors next to the byte counters (an extra field in the previous-samples tuple).
- Help text describes the three new readings.

## v0.52.44-beta — 2026-11-15

### Added
- Mbit/s as a third speed unit (Options: KB/s, MB/s, Mbit/s), now the default: bits are how providers and speed tests write a speed (`Format.BrzinaPrikaz`).
- MAX under each graph: the fastest one-second sample of the day and when it happened. It is kept in the `stanje` table (`max_down`, `max_up`, `yyyy-MM-dd;bytes per second;HH:mm`), so it survives a restart and starts again at midnight (`UcitajMaksimume`, `UpisiMaksimume`, reset in `OsveziZbirove`).

### Changed
- Help text describes the new unit and the MAX line; `CLAUDE.md` says where they live.

## v0.50.44-beta — 2026-11-15

### Added
- MONTH, second click (the M key again): the days of the current billing period, day by day, the newest first; the table scrolls with the mouse wheel or the arrow keys and shows a position bar like the program list (`Istorija("mesdani")`, `IstorijaIzBaze`).
- Projection to the end of the period ("PROJEKCIJA" / "PROJECTED", top right): the traffic so far stretched over the whole period, the same idea as DU Meter's projected line.
- Export the period to CSV from the tray menu, or with `--izvoz F` for scripts: every day, the total, the average per day and the projection; semicolon separated, comma decimals, UTF-8 with BOM so Excel reads č, ć and ž (`IzveziPeriod`, `CsvRedovi`, `Izvezi`).

### Changed
- The history table shows seven rows and scrolls, because a billing period can have 31 days; the ▼ received / ▲ sent split of the whole period stands next to the table heading.
- The big number of a history screen is taken from the totals of the period, not from its first row (`Istorija.Zbir`), so a screen can list days while the total stays the period.
- Help text and `CLAUDE.md` describe the new screens; version 0.50.44 (3 new things and 1 refinement: 47+3, 43+1).

## v0.47.43-beta — 2026-11-07

### Added
- Average per day in the history: on DAYS it is the average of the seven days listed, on WEEKS and MONTHS the total divided by the days that have passed (`Istorija.Prosek`, `Prosek` in `Program.cs`).
- Keyboard: D, N and M open the days, weeks and months history and Space turns pause on and off, the same as the buttons under the display; Esc returns to the live view, F1 opens help and the arrows scroll the program list (`GlavniProzor.OnKeyDown`).

### Changed
- Version numbering is the same as in Bronxamp: the number is worked out from the history in `Izmene.cs`, never typed by hand — 0.Y.Z, Y = every new thing since the start, Z = every refinement or fix, and neither goes back to zero. The whole history was renumbered (0.9.0 became 0.47.43, 0.1.0 became 0.8.0). Each entry now has separate `Nove` and `Dorade` lists and the label (NOVO / DORADA / both) comes from them.

## v0.45.43-beta — 2026-11-05

### Added
- Program icons on the main screen and in the program list, read from the .exe; protected programs get their first letter in a circle.
- Time online today per program (column DANAS / TODAY), counted also while the windows are hidden, not during a pause; new table `programi_dan`.
- Watch (ČUVAR): a click on the circle in the program list watches a program (`cuvani_programi`). More than 20 seconds without a connection while the program runs = outage: red row, `PREKID` on the mini display and the tray tooltip, recorded in the new table `prekidi`. A closed program is not an outage.

### Changed
- The bar in the program list is gone: it showed the number of links and looked like usage.
- The program list is read every 3 seconds also while the windows are hidden (`Pracenje.cs`). Names come from the path of the process, because `Process.ProcessName` reads the whole list of processes.

## v0.41.43-beta — 2026-11-04

### Added
- Mini display transparency, set in Options (0, 20, 40 or 60 %, default 40; `mini_providnost`). The case loses that much, the screen half as much, the numbers and labels stay solid. All of it is solid while the mouse is over it.

### Changed
- Mini display is 8 units lower: the empty space above the labels now equals the space below the numbers (it was 15 px against 9 px).

## v0.38.42-beta — 2026-11-04

### Changed
- Download is drawn at the same size and height as upload: digits, arrow, unit and graph.

## v0.38.41-beta — 2026-11-04

### Changed
- The main window is a layered window: only the device and a soft shadow are visible, the dark rectangle around it is gone. It is drawn by `Osvezi` (`UpdateLayeredWindow`), not by `OnPaint`.
- Resizing keeps the proportions of the device (`WM_SIZING`), so no transparent strip appears next to it.
- The layered-window code of the main window, mini display and dialogs is now in one place (`Sloj`).

## v0.38.38-beta — 2026-11-04

### Changed
- Bottom button row is laid out per language (`RasporediDugmad`): widths follow the labels, the row spans the content of the screen (69-1117), icon and label are centered in the action buttons. The fixed widths were made for the Serbian labels: OPTIONS was tight, HELP and ABOUT had empty space.

## v0.38.35-beta — 2026-11-04

### Added
- Help window (`PomocDijalog`, text in `Pomoc.cs`): what each part does and why, scrollable, both languages. Opened by the new POMOĆ / HELP button, F1 or the menu.
- `LcdDijalog`: common base of the About and Help windows (`Dijalozi.cs`); About moved there from `Prozori.cs`.

### Fixed (code review, `greske.log` and tests on the .exe)
- Two copies could run at once and count the same traffic: the pipe opens only after start-up, so two quick starts both missed it (seen in `greske.log`). Now a named mutex is taken first; the second copy waits up to 5 s for the pipe.
- Rajdhani was missing in about every fifth start: GDI+ accepted it and left it out of the family list of the shared collection, so all labels fell back to DSEG14. It now has its own collection (0 of 40 starts failed, retry kept as a safety net).
- `InvariantGlobalization` made WinForms throw on every keyboard language change (`CultureNotFoundException`, seen in `greske.log`). Removed; the culture is set to invariant in `Main` instead.
- The big window's place and size were never saved: at `ApplicationExit` the window is already gone. Saved when it hides and on exit.
- A close request from Windows disposed the window while the program kept running, and later "Open" failed. Visible window: hides; hidden window or Windows shutdown: the program ends cleanly.
- Up to the last minute of traffic was lost on Windows shutdown; the data is now written then too.
- Dialogs opened from the menu stayed invisible when the main window was minimized; opening a second dialog from the menu is prevented.
- Mini display kept the old language of "DANAS GB" until restart.
- Program no longer disappears silently if the database cannot be opened: message and log entry.
- A segment number longer than its places (over 999 links) was spaced wrongly.
- Mini display standing on the taskbar went back to the corner after a restart.
- Tray icon was redrawn pixel by pixel every second; a few variants are now made once, at the system icon size.

## v0.35.29-beta — 2026-11-03

### Added
- PROGRAMS screen (`Rezim = "programi"`): every program that is online, not only the top 4; opened by clicking the "ONLINE NOW" heading of the live screen, closed by clicking its title or Esc. Same layout as the history table (links as a number and a bar), mouse wheel / Up / Down scroll.

### Notes
- Only the number of links per program. Speed and today's traffic per program: planned, see CLAUDE.md ("Plan za budućnost").

## v0.32.29-beta — 2026-11-03

### Changed
- A label that contains a letter is drawn completely in Rajdhani (letters, digits, signs); pure numbers stay in DSEG. Date rows of the history table use Rajdhani too (`Crtac.F14T`).
- Programs online: full name, never cut (was 16 characters), written properly: Microsoft Edge, Cross Device Service, Claude. A name wider than the row is shrunk.

## v0.31.28-beta — 2026-11-03

### Changed
- Letters of the LCD labels use Rajdhani SemiBold (OFL, embedded); digits and punctuation stay in DSEG14/DSEG7. DSEG14 drew Y, W, X, M... badly.
- Serbian diacritics restored on the labels (PROŠLA, OBRAČUNSKI, JUČE, ČET, JOŠ); DSEG14 has none.
- If the font cannot be loaded, the labels fall back to DSEG14.

## v0.31.25-beta — 2026-11-03

### Added
- English translation, chosen in Settings ("Jezik / Language"; saved as `jezik` in podesavanja.json). Covers the windows, buttons, history, mini display, tray menu and tooltip, settings and the version list.
- `Jezik.cs`: `Jez.T(sr, en)`; version lines in `Izmene.cs` are written as "srpski | english".

### Changed
- About window title uses the same font as the main header (DSEG14 draws X badly).

## v0.30.24-beta — 2026-11-03

### Changed
- Name UnderGRAND replaced by BronxSoft: window title, About window, settings, tray, exe (`BronxSoft Netmetar.exe`), shortcuts, assembly info.
- Data folder is now `%APPDATA%\BronxSoft Netmetar`; the old `UnderGRAND Netmetar` folder is moved there on first start (kept in place if the move fails).
- Old startup shortcut is deleted and replaced; `Napravi precicu.bat` removes the old desktop shortcut.

### Notes
- Close the old program (tray -> Izlaz) before the first start, so the data folder can be moved. The app icon is unchanged.

## v0.29.22-beta — 2026-11-03

### Changed
- OPCIJE and O PROGRAMU buttons match the mockup: drawn gear and info icons, larger Segoe UI labels, wider buttons (the old glyphs came from a font without them and looked tiny).

## v0.29.20-beta — 2026-11-03

### Changed
- About window is compact (about 55 % of the main window width, like the approved mockup).
- "ŠTA JE NOVO" lists every version with date, label and the items done in it (scrollable).
- `Izmene.cs` entries now carry a list of short lines instead of one description.

## v0.29.18-beta — 2026-11-03

### Fixed
- About window is a layered (per-pixel alpha) window: no dark rectangular corners around the rounded case.
- About window can be dragged with the mouse (everything except the X and ZATVORI buttons).
- About window scales down (70-100 %) when the main window is small, so it does not stick out.

## v0.28.16-beta — 2026-11-03

### Added
- Scrollable version history in the About window ("ŠTA JE NOVO"): date, X.Y.Z and a computed label (Novi deo / Novo / Dorada). Mouse wheel or Up/Down scrolls.
- `Izmene.cs` is the single source of the current version (top entry); `Netmetar.csproj` must match.

### Notes
- Source change only; compile and verify on Windows.

## v0.25.16-beta — 2026-11-03

### Changed
- Tray tooltip no longer shows the program name and version; only speeds and today's total (the " GB" was being cut off by the 63-character limit).
- Version is still shown in the main window header, About window and settings.

## v0.24.15-beta — 2026-11-03

### Changed
- About window redrawn to match the approved mockup: original UnderGRAND icon, version pill, square bullets, close (X) button.
- "ŠTA JE NOVO" and the list use Segoe UI so Serbian diacritics are shown (DSEG14 has none).
- Main window is now frameless: version pill and divider in the header, own minimize and close buttons (close still hides to the tray).
- The header drags the window; the edges resize it. Right click in the header still opens the tray menu.

### Notes
- Source change only; compile and verify on Windows (`Napravi exe.bat`).

## v0.21.14-beta — 2026-11-02

### Fixed
- Replaced the standard Windows MessageBox About dialog with a custom UnderGRAND Classic-themed window.
- Removed literal `\\n` text from the About dialog by eliminating the escaped MessageBox text.
- Added a themed close button and LCD-style version/update information.

### Notes
- Build and verify on Windows.

## v0.19.13-beta — 2026-11-02

### Fixed
- Fixed the oversized action-button labels that previously overflowed their buttons.
- `OPCIJE` and `O PROGRAMU` now use compact, smaller typography and appropriate button widths.
- The application title bar now includes the current version.

### Changed
- Classic layout and the existing four central mode buttons are preserved.
- Action buttons remain outside the central button group and do not overlap it.

### Notes
- Compile and verify on Windows.

## v0.18.10-beta — 2026-11-02

### Fixed
- Tray icon preserves the original UnderGRAND logo; only the existing download/upload arrow colors pulse according to activity.
- No replacement generic arrow icon is used.

### Added
- Visible `OPCIJE` button at the lower left of the main window.
- Visible `O PROGRAMU` button at the lower right, displaying version and recent changes.

### Notes
- Compile and verify on Windows.

## v0.16.9-beta — 2026-11-02

### Fixed
- Tray tooltip no longer displays literal `\\n` sequences; compact separators are used.
- Tray tooltip is capped at the Windows NotifyIcon 63-character limit.

### Added
- Animated activity arrows in the tray icon: download pulses green and upload pulses amber independently when traffic is detected.
- Inactive arrows remain dimmed.

### Notes
- Activity threshold: 500 bytes/second per direction.
- Build and verify on Windows.

## v0.15.6-beta — 2026-11-02

### Changed
- History window's bottom summary statistics now use four equal-width columns.
- Summary labels and values are centered on the same axis for cleaner alignment.
- Existing Classic housing, palette, LCD typography, and window structure are preserved.

### Notes
- Visual polish only; no measurement, database, or period logic changes.
- Compile and verify on Windows.

## v0.14.5-beta — 2026-11-02

### Changed
- Mini display's download/upload labels and unit text are centered in their respective columns, matching the approved Classic mockup.
- Slightly increased the upper-row label size and arrow size for improved readability.
- Centered the “DANAS GB” label over its value.

### Notes
- Main display and mini display housing, colors, and large digits remain unchanged.
- Compile and verify on Windows.

## v0.14.2-beta — 2026-11-02

### Fixed
- Mini display now redraws speed-unit labels dynamically, so KB/s ↔ MB/s changes are visible without stale labels remaining in the cached background.

### Notes
- This is a source fix; compile and verify on Windows.

## v0.14.1-beta — 2026-11-02

### Fixed
- KB/s display automatically switches to MB/s when the value approaches the three-digit LCD limit.
- Download and upload choose display units independently.
- Automatic unit selection is reflected in the main window, mini display, and tray tooltip.
- Increased precision for low MB/s values (e.g. 8.75 MB/s).

### Notes
- User-selected MB/s remains fixed.
- Internal traffic measurement and database storage remain unchanged.
- Not yet compiled or runtime-tested on Windows.

## v0.11.0-beta — 2026-11-02

### Added
- Download speed graph alongside the existing upload graph.
- Settings option to display speed in KB/s or MB/s.
- Selected speed unit is used in the main window, mini display, and tray tooltip.

### Changed
- Version updated from 0.1.0-beta to 0.2.0-beta.

### Fixed
- No bug fixes in this release.

### Notes
- Traffic counters and database values remain stored in bytes.
- This source package has not been compiled or runtime-tested on Windows yet.
