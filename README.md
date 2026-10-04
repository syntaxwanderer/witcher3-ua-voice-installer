# The Witcher 3 Remastered — Ukrainian Voice-Over (AI): installer source

Source code of `UA_Voice_Setup.exe`, the one-click installer shipped inside the Nexus Mods package
**Ukrainian Voice-Over (AI) - Ukrainska Ozvuchka** (The Witcher 3, mod 12946):
https://www.nexusmods.com/witcher3/mods/12946

Українська: це відкритий код інсталятора фанатської української ШІ-озвучки The Witcher 3 Remastered.
Інсталятор нічого не завантажує з інтернету. Він збирає українську озвучку з польського файлу озвучення
самого гравця, перевіряє результат контрольною сумою, робить резервні копії й додає пункт меню
«Українська (ШІ)». Видалення повертає все як було.

## What is in the mod package

| File in the zip | What it is |
|---|---|
| `ua_voice.dat` | Data only: our Wwise audio blocks (`.wem`) of the voiced lines, the lipsync blocks stored as a **XOR delta** against the player's own Polish file (CDPR's data is not redistributed), and ADX frame bodies of the 19 storybook narrator tracks. No executable code. |
| `manifest.json` | Offsets/sizes into `ua_voice.dat` plus size + CRC32 of the expected original game files and of the result (self-check). |
| `UA_Voice_Setup.exe` | The installer built from this repository (C#, .NET Framework 4.x, WinForms). |
| `source\` | The same files as in this repository. |
| `README_UA.txt` | Installation instructions (Ukrainian + English). |

## What the installer does

`Setup.cs` = logic, `W3UA.cs` = file formats (w3speech, w3strings, USM/ADX in `movies.bundle`), `Program.cs` = window + command line.

1. **Finds the game** (`Job.FindGame`): Steam path from `HKCU\Software\Valve\Steam` + `libraryfolders.vdf`,
   GOG entries under `HKLM\SOFTWARE\(WOW6432Node\)GOG.com\Games`. A folder is accepted only if it has `bin` and `content\content0`.
   The user can also pick the folder manually. The registry is only **read**.
2. **Checks** the package (size + CRC32 of `ua_voice.dat`), that the game is not running, the CRC32 of the player's
   `content\content0\plpc.w3speech` (must be the supported game version) and free disk space.
3. **Builds the voice-over** (`W3UA.PatchSpeech`): reads the player's Polish `plpc.w3speech` (never modified) and writes
   `content\content0\brpc.w3speech` — the Brazilian-Portuguese speech slot, which becomes the menu option «Українська (ШІ)».
   Replaced entries get our audio block and the player's lipsync XOR our delta; everything else is copied as is.
   Written to a temporary file, verified by CRC32, then renamed. An existing real `brpc.w3speech` is kept as `brpc.w3speech.bak_ua5`;
   an older version of this voice-over (its CRC is listed in the manifest `prev_crcs`) is replaced in place instead.
4. **Storybook narrator** (`W3UA.PatchMovie`): backs up `content\content0\bundles\movies.bundle` as `movies.bundle.bak_ua5`,
   replaces the ADX audio of the Brazilian-Portuguese channels of 19 storybook videos in place (same size), verifies CRC32.
5. **Menu label** (`Job.SetLabel`): backs up `content\content0\ua.w3strings` as `ua.w3strings.bak_ua5` and changes three strings
   (the speech-language names) to «Українська (ШІ)». All other strings stay byte-for-byte identical.
6. **Speech language** (`Job.SetSpeech`): in `Documents\The Witcher 3\dx12user.settings` / `user.settings` sets
   `SpeechLanguage=BR` (and `RequestedSpeechLanguage`); the previous value is saved in `ua_voice_prev_speech.txt`.
7. **Uninstall** (`Job.Uninstall`): deletes our `brpc.w3speech` (only if its CRC matches this or a previous version of the voice-over), restores every `.bak_ua5`
   backup and the previous speech language.

It does **not**: access the network, start or inject into the game, write to the registry, install services or scheduled tasks,
request admin rights (manifest `asInvoker`), or touch files outside the game's `content\content0` folder and the two settings
files in `Documents\The Witcher 3` (+ backups).

## How to build

Requirements: Windows 10/11 (the .NET Framework 4.x C# compiler `csc.exe` ships with Windows). No Visual Studio, NuGet or internet needed.

```
git clone https://github.com/syntaxwanderer/witcher3-ua-voice-installer.git
cd witcher3-ua-voice-installer
build.cmd
```

`build.cmd` runs:

```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /codepage:65001 /optimize+ /platform:anycpu ^
  /win32manifest:app.manifest /target:winexe /out:UA_Voice_Setup.exe ^
  /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  AssemblyInfo.cs Setup.cs Program.cs W3UA.cs
```

and the same with `/target:exe /out:UA_Voice_Setup_cli.exe` (console build used for automated tests).
Output: `UA_Voice_Setup.exe` (~30 KB). The C# compiler embeds a build timestamp/MVID, so a rebuild is not
byte-identical but is the same code. SHA-256 of the released binary: see the release notes on Nexus.

## Test without a window

```
UA_Voice_Setup_cli.exe --cli [--game "<game folder>"] [--uninstall] [--test] [--docs "<settings folder>"]
```

Put the exe next to `manifest.json` and `ua_voice.dat`. `--test` skips the "game is running" check, `--docs` points the
settings step at a test folder. Exit code 0 = OK. Every release is tested this way on a copy of the game
(install, reinstall, uninstall, install again; result CRC = manifest; Polish file untouched).

## About the mod

Fan-made, free and non-commercial. Voices are AI speech synthesis (FireRedTTS3, Apache-2.0) from the game's official
Ukrainian text; checked with Whisper (MIT). Not approved or endorsed by CD PROJEKT RED.
The Witcher 3: Wild Hunt © CD PROJEKT S.A.
