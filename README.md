# RunInBackground

![Windows](https://img.shields.io/badge/Windows-11_x64-0078D4)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)

Launch an application, document or folder on another Windows virtual desktop. Its window remains available when you visit that desktop.

## Usage

```powershell
& "C:\Path\To\RunInBackground.exe" "C:\Path\To\Program.exe"
```

The default destination is the **second desktop**. An optional second argument retains the original zero-based index:

```powershell
& "C:\Path\To\RunInBackground.exe" "C:\Path\To\Document.txt" 1
```

The launcher has no console window. It uses the existing launch and window-detection implementation: start minimized, identify the application window, move it, restore it and return to the original desktop. Missing desktops are created by the original system-hotkey implementation.

## Installation

Download `RunInBackground.exe` from [Releases](https://github.com/muhammetozeski/RunInBackground/releases) into a writable folder. This self-contained edition includes the .NET runtime. The smaller `RunInBackground-FrameworkDependent-RequiresNET10.exe` requires the .NET 10 runtime for Windows x64.

The executables are digitally signed. To let Windows verify the signature, run `Install-Certificate.cmd` from `SignatureTrust.zip` once. The programs run without it; only the signature stays unverified.

## Portable settings

The program creates `UserData/settings.txt` beside its executable. `DesktopNumber=2` selects the second desktop using one-based numbering. `Language=system`, `tr` or `en` controls user-facing text. `EnableLogging` controls disk logs.

Editable language resources live in `UserData/lang.tr.xml` and `UserData/lang.en.xml`. Logs live in `AppCache/Logs`. Move the whole program folder to preserve settings and logs; paths do not depend on the terminal's working directory.

The native taskbar command is a separate [TaskbarBackground](https://github.com/muhammetozeski/TaskbarBackground) Windhawk extension.

Development knowledge starts at [Docs/İçindekiler.md](Docs/İçindekiler.md).
