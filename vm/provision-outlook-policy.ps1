# Forces Classic Outlook for scanuser by writing to its loaded (or loadable) registry hive. Run as administrator on the guest.
$sid = (New-Object Security.Principal.NTAccount('scanuser')).Translate([Security.Principal.SecurityIdentifier]).Value
$loaded = Test-Path "Registry::HKEY_USERS\$sid"
if (-not $loaded) { reg load HKU\scanuser C:\Users\scanuser\NTUSER.DAT | Out-Null; $root = 'HKU\scanuser' } else { $root = "HKU\$sid" }
reg add "$root\Software\Microsoft\Office\16.0\Outlook\Preferences" /v UseNewOutlook /t REG_DWORD /d 0 /f | Out-Null
reg add "$root\Software\Policies\Microsoft\office\16.0\outlook\options\general" /v HideNewOutlookToggle /t REG_DWORD /d 1 /f | Out-Null
reg add "$root\Software\Policies\Microsoft\office\16.0\outlook\preferences" /v DoNewOutlookAutoMigration /t REG_DWORD /d 0 /f | Out-Null
if (-not $loaded) { [gc]::Collect(); reg unload HKU\scanuser | Out-Null }
