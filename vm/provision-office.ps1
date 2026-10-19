# Installs Classic Outlook only via the Office Deployment Tool. Run as administrator on the guest.
$work = 'C:\odt'
New-Item -ItemType Directory -Force $work | Out-Null
Invoke-WebRequest -UseBasicParsing -Uri 'https://officecdn.microsoft.com/pr/wsus/setup.exe' -OutFile "$work\setup.exe"
Copy-Item "$PSScriptRoot\office-config.xml" "$work\configuration.xml" -Force
Start-Process "$work\setup.exe" -ArgumentList "/configure $work\configuration.xml" -Wait
