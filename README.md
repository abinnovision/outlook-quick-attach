# Outlook Quick Attach

Classic Outlook add-in that lists the newest files of a configured folder in the compose window and attaches the
selected file with one click.

![Quick Attach menu in the compose window](docs/images/quick-attach-menu.png)

## Install

Only 64-bit Classic Outlook is supported. The new Outlook is not.

1. Get `QuickAttach.msi` from the link or share your IT team provides, or from the latest
   [GitHub Release](https://github.com/abinnovision/outlook-quick-attach/releases/latest).
2. Close Outlook completely.
3. Double-click `QuickAttach.msi`. No admin rights are needed.
4. Start Outlook and open a New Email.
5. On the Message tab, find the **Recent Files** button (your IT team may have renamed it).
6. Click it and select a file to attach it.

**If the button does not appear:** go to File > Options > Add-ins. Next to Manage, choose COM Add-ins, click Go...
and tick **Outlook Quick Attach**. If it is not listed, choose Disabled Items under Manage, click Go... and enable it.

**Update:** automatic at sign-in when your IT team provides updates. Otherwise install the newer MSI the same way.

**Uninstall:** Settings > Apps > Installed apps > Outlook Quick Attach.

## Configuration

The add-in reads its settings from a central `config.json`, usually next to the MSI on a share.
`deploy/config.example.json` is a template. Reference with every supported key:

```json
{
  "defaults": {
    "FolderPath": "\\\\filer01\\files\\%USERNAME%",
    "AdditionalPaths": ["\\\\filer01\\shared\\templates"],
    "GroupLabel": "Quick Attach",
    "GalleryLabel": "Recent Files",
    "GalleryScreentip": "Attach recent files from the configured folder",
    "TargetTab": "Message",
    "MaxItems": 10,
    "MaxAgeDays": 0,
    "FileFilter": ["*.pdf", "*.jpg", "*.png", "*.tif"],
    "ShowThumbnails": true,
    "AllowUserOverride": false
  },
  "users": {
    "jdoe": { "FolderPath": "\\\\filer01\\files\\accounting" }
  },
  "update": {
    "MsiPath": "\\\\filer01\\it$\\quickattach\\QuickAttach.msi",
    "MinVersion": "0.1.0",
    "MsiSha256": "<sha256 of QuickAttach.msi from SHA256SUMS>"
  }
}
```

Built-in defaults apply to keys that are not set, except `FolderPath` and `AdditionalPaths`, which are empty by default
(the values above are examples).

| Key                 | Type    | Default                                          | Meaning                                                                 |
| ------------------- | ------- | ------------------------------------------------ | ----------------------------------------------------------------------- |
| `FolderPath`        | string  | empty                                            | Folder to list. Environment variables such as `%USERNAME%` are expanded |
| `AdditionalPaths`   | list    | empty                                            | More folders. Environment variables are expanded                        |
| `GroupLabel`        | string  | `Quick Attach`                                   | Ribbon group label                                                      |
| `GalleryLabel`      | string  | `Recent Files`                                   | Menu button label                                                       |
| `GalleryScreentip`  | string  | `Attach recent files from the configured folder` | Menu button tooltip                                                     |
| `TargetTab`         | string  | `Message`                                        | Ribbon tab for the button: `Message` or `Insert`                        |
| `MaxItems`          | integer | `10`                                             | Number of files, clamped to 1 to 30                                     |
| `MaxAgeDays`        | integer | `0`                                              | Hide files older than this many days; `0` shows all                     |
| `FileFilter`        | list    | `*.pdf`, `*.jpg`, `*.png`, `*.tif`               | File name patterns                                                      |
| `ShowThumbnails`    | boolean | `true`                                           | Show file type icons                                                    |
| `AllowUserOverride` | boolean | `false`                                          | Enables "Settings..." and the user override layer                       |
| `users`             | object  | none                                             | Per-user keys, matched by user name (case-insensitive)                  |
| `update.MsiPath`    | string  | none                                             | MSI the logon updater installs                                          |
| `update.MinVersion` | string  | none                                             | Minimum version; older installs disable the menu                        |
| `update.MsiSha256`  | string  | none                                             | Expected SHA-256 of the MSI; the updater skips an MSI that differs      |

Lists (`AdditionalPaths`, `FileFilter`) accept a `;`-separated string or a JSON array. Booleans accept `true`/`false`
or `1`/`0`.

### How the effective configuration is built

The path of `config.json` is stored in `HKCU\Software\abigroup\QuickAttach\ConfigPath`. The MSI sets it from the
`CONFIGPATH` property and keeps it across upgrades.

Each value is taken from the first source that sets it, highest priority first:

| Priority | Source                                                                                   |
| -------- | ---------------------------------------------------------------------------------------- |
| 1        | Policy: `HKCU\Software\Policies\abigroup\QuickAttach`                                    |
| 2        | User override: `HKCU\Software\abigroup\QuickAttach\User` (only with `AllowUserOverride`) |
| 3        | `config.json`: `users[<USERNAME>]`                                                       |
| 4        | `config.json`: `defaults`                                                                |
| 5        | MSI properties: `HKCU\Software\abigroup\QuickAttach\Install`                             |
| 6        | Built-in defaults                                                                        |

Example: `defaults` sets `FolderPath` and `GroupLabel`, and `users.jdoe` overrides only `FolderPath`. User `jdoe` gets
the overriding folder with the default group label. Everyone else gets the default folder and label. Keys set nowhere use the
built-in default.

The last good `config.json` is cached in `%LocalAppData%\abigroup\QuickAttach\config.cache.json` and used when the
share is not reachable.

Changes apply as follows:

- Folder, filter, limits and the menu contents: every time the menu opens. After "Folder not reachable", the menu
  shows that result for up to 30 seconds while the folder is checked again in the background.
- Labels: within 60 seconds (`config.json` is polled and read in the background, HKCU keys are watched).
- `TargetTab`: when Outlook starts.

## Deployment (IT)

Copy `QuickAttach.msi`, `config.json` and `install.cmd` to a share such as `\\filer01\it$\quickattach\` (users read,
IT writes). Users run `install.cmd`, or a logon script calls it. It skips the install when the same or a newer version
is already installed and passes the `config.json` next to it as `CONFIGPATH`. No admin rights are needed. Users restart
Outlook afterwards.

Direct install with MSI properties:

```cmd
msiexec /i QuickAttach.msi /qn CONFIGPATH="\\filer01\it$\quickattach\config.json"
```

| Property       | Effect                                                                    |
| -------------- | ------------------------------------------------------------------------- |
| `CONFIGPATH`   | Path of the central `config.json`, stored as `...\QuickAttach\ConfigPath` |
| `FOLDERPATH`   | Fallback `FolderPath`, stored under `...\QuickAttach\Install`             |
| `GROUPLABEL`   | Fallback `GroupLabel`, stored under `...\QuickAttach\Install`             |
| `GALLERYLABEL` | Fallback `GalleryLabel`, stored under `...\QuickAttach\Install`           |

Updates: copy a newer MSI to `update.MsiPath`. The updater installs it at the next logon, before Outlook starts.
`update.MinVersion` above the installed version disables the menu with "Please install the update".

To pin the update, verify the release with `gh attestation verify` (see [Verify a download](#verify-a-download)), then
put its SHA-256 from `SHA256SUMS` into `update.MsiSha256`. The updater copies the MSI to `%TEMP%`, checks the hash of
that copy and installs it only on a match. Without `update.MsiSha256` it installs any newer MSI.

## Builds

- Stable releases: GitHub Releases, with `QuickAttach.msi`, `config.example.json`, `install.cmd` and `SHA256SUMS`.
- Dev builds: every pull request, merge group and push to `main` uploads the MSI as the workflow artifact
  `QuickAttach-<version>`, versioned `<package.json version>.<run number>` (e.g. `0.1.0.57`). A newer dev build of the
  same release upgrades in place.

Each build ships a `SHA256SUMS` file and a GitHub build provenance attestation. Builds are not code-signed, so Outlook
shows the add-in's publisher as `<None>` and SmartScreen may warn when the MSI is downloaded from the internet.

### Verify a download

Compare the hash with the matching line in `SHA256SUMS`:

```powershell
(Get-FileHash QuickAttach.msi -Algorithm SHA256).Hash.ToLower()
```

Check that the file was built by this repository's CI ([GitHub CLI](https://cli.github.com/)):

```sh
gh attestation verify QuickAttach.msi -R abinnovision/outlook-quick-attach
```

## Development

| Path                           | Target         | Purpose                                                   |
| ------------------------------ | -------------- | --------------------------------------------------------- |
| `src/QuickAttach.Core`         | netstandard2.0 | Config merge, central config, file listing, version rules |
| `src/QuickAttach`              | net48          | COM add-in shell: ribbon, registry, watcher, file icons   |
| `src/QuickAttach.Updater`      | net48          | Logon updater, installs a newer MSI from the share        |
| `tests/QuickAttach.Core.Tests` | net10.0        | Unit tests for Core                                       |
| `installer/`                   | WiX v6         | Per-user MSI, built on Windows only                       |

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

## Troubleshooting

- Log file: `%LocalAppData%\abigroup\QuickAttach\log.txt` (add-in and updater). Above 1 MB it moves to `log.old.txt`,
  which replaces the previous one.
- An invalid `config.json` (or cache) is logged with the file path and the parse error; the add-in falls back to the
  cached copy. An unreachable `config.json` is logged once until it can be read again.
- Language: menu entries, messages and the settings dialog follow the Windows display language (English or German).
  Configurable labels such as `GroupLabel` are used as set.
- Add-in disabled by Outlook: check `LoadBehavior` (3) under
  `HKCU\Software\Microsoft\Office\Outlook\Addins\Abigroup.QuickAttach`. The add-in rewrites `DoNotDisableAddinList` at
  every start.

## License

Apache License 2.0, see [LICENSE](LICENSE).
