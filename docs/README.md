# QuickAttach

Classic Outlook COM add-in that adds a "Letzte Dateien" gallery to the compose window and the inline reply.
The gallery lists the newest files of a configured (network) folder and attaches the selected file.

## Projects

| Path                          | Target         | Purpose                                                   |
| ----------------------------- | -------------- | --------------------------------------------------------- |
| `src/QuickAttach.Core`         | netstandard2.0 | Config merge, central config, file listing, version rules |
| `src/QuickAttach`              | net48          | COM add-in shell: ribbon, registry, watcher, thumbnails   |
| `src/QuickAttach.Updater`      | net48          | Logon updater, installs a newer MSI from the share        |
| `tests/QuickAttach.Core.Tests` | net10.0        | Unit tests for Core                                       |
| `installer/`                  | WiX v6         | Per-user MSI, built on Windows only                       |

## Build

Tools come from `.tool-versions` (mise or asdf). The Yarn scripts wrap `dotnet`:

```sh
yarn install
yarn build      # dotnet build QuickAttach.sln
yarn check      # dotnet format --verify-no-changes
yarn test-unit  # Core unit tests
yarn fix        # dotnet format
```

The net48 projects build on macOS and Linux through `Microsoft.NETFramework.ReferenceAssemblies`, so the solution
contains everything except the installer. The MSI needs Windows:

```cmd
dotnet build installer\QuickAttach.Installer.wixproj -c Release -p:Version=0.1.0
```

CI builds the MSI on every pull request (version `0.0.<run number>`) and attaches it, together with
`deploy/config.example.json` and `deploy/install.cmd`, to each GitHub release.

## Install

Only 64-bit Outlook is supported: the MSI is an x64 package and writes the COM registration to the 64-bit registry
view.

Copy `QuickAttach.msi`, `config.json` and `install.cmd` to a share such as `\\filer01\it$\quickattach\`
(users read, IT writes). Users run `install.cmd`, or a logon script calls it. It skips the install when the same or a
newer version is already installed, and passes the `config.json` next to it as `CONFIGPATH`. No admin rights are
needed. Restart Outlook afterwards.

Direct install with MSI properties:

```cmd
msiexec /i QuickAttach.msi /qn CONFIGPATH="\\filer01\it$\quickattach\config.json"
```

`CONFIGPATH` is stored in `HKCU\Software\abigroup\QuickAttach\ConfigPath` and kept across upgrades. `FOLDERPATH`,
`GROUPLABEL` and `GALLERYLABEL` are optional fallbacks written to `HKCU\Software\abigroup\QuickAttach\Install`.

Updates: copy a newer MSI to `update.MsiPath`. The updater installs it at the next logon, before Outlook starts.
`update.MinVersion` above the installed version disables the gallery with "Bitte Update installieren".

## Configuration

Sources, priority, value names and defaults are described in the spec (section 3). Summary, highest priority first:

1. `HKCU\Software\Policies\abigroup\QuickAttach`
2. `HKCU\Software\abigroup\QuickAttach\User` (only when `AllowUserOverride` is true)
3. Central `config.json`: `users[<USERNAME>]`, then `defaults`
4. `HKCU\Software\abigroup\QuickAttach\Install`
5. Built-in defaults

Each value is taken from the first source that sets it. The last good `config.json` is cached in
`%LocalAppData%\abigroup\QuickAttach\config.cache.json`. See `deploy/config.example.json` for a template.

Labels update without a restart: `config.json` is polled every 60 s and the HKCU keys are watched.
Folder, filter and limits are read on every gallery drop.

`TargetTab` (`Message` or `Insert`) is read only when Outlook starts, because the ribbon XML is fixed afterwards.

## Troubleshooting

- Log file: `%LocalAppData%\abigroup\QuickAttach\log.txt` (add-in and updater).
- Add-in disabled by Outlook: check `LoadBehavior` (3) under `HKCU\Software\Microsoft\Office\Outlook\Addins\Abigroup.QuickAttach`.
  The add-in rewrites `DoNotDisableAddinList` at every start.
