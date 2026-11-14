# Host delivery (MSIX / App Installer or xcopy)

- The WPF host (`src/GreenHive.Wpf.UI`) runs unpackaged by default (`dotnet run` / F5). For managed distribution it can be
  wrapped in an MSIX package (Windows Application Packaging Project) and signed with a company certificate that the
  clients trust.
- MSIX packages are distributed via an `.appinstaller` file that checks for updates at startup or in the background.
- A host update always requires a restart; modules, by contrast, are updated through the ModuleLoader and swapped in
  place by the HotSwapManager.
- Package identity also enables Windows push (WNS): plug an `IPushChannelProvider` into `PushNotificationService`.
- Alternative: self-contained publish (`dotnet publish -r win-x64 --self-contained`) with an own updater. Never trim the
  host: modules are loaded at run time and need the full framework surface.
