# Desktop and delivery contracts

`Program.LaunchInBackgroundDesktop` retains the user's shell interfaces and window identification algorithm. The positional desktop argument is zero-based. The setting `DesktopNumber` is one-based and is converted only when the positional argument is omitted. The default is desktop two.

Supporting startup uses `ApplicationServices.Initialize` and the outer `ExceptionBoundary.RunSafely`. The existing `Logger` is reused; log paths now originate at `AppPaths.AppCache`. Localization's embedded resource identity is `RunInBackGround.Localization.lang.en.xml`, independently of the executable name `RunInBackground.exe`.

The executable is a Windows subsystem application, so launching it does not allocate a console. Self-contained and framework-dependent single-file builds are produced by `Publish.ps1`; both keep user files outside the executable. Do not put generated settings or log files in release assets.

The native taskbar extension has its own repository and performs its desktop moves in C++, without invoking this launcher.

Runtime verification: the self-contained executable initialized settings, both language files and logs in its own portable root from a different working directory. Turkish/English/Turkish usage changes applied. Relocating that root retained its configured language. Invalid target input returned exit code two. Both publication variants compiled without warnings; their Authenticode signatures were Valid.
