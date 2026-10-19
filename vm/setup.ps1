# Runs as SYSTEM at boot (scheduled task) until OpenSSH is installed and configured.
$dir = 'C:\Windows\Setup\Scripts'
Start-Transcript -Path "$dir\setup.log" -Append | Out-Null
$marker = "$dir\setup.done"
if (Test-Path $marker) { exit 0 }

# Never sleep, no lock screen
powercfg /change standby-timeout-ac 0
powercfg /change standby-timeout-dc 0
powercfg /change monitor-timeout-ac 0
powercfg /change monitor-timeout-dc 0
powercfg /hibernate off
reg add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\Personalization' /v NoLockScreen /t REG_DWORD /d 1 /f
reg add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' /v InactivityTimeoutSecs /t REG_DWORD /d 0 /f

# VirtIO / SPICE drivers (network first)
Get-ChildItem "$dir\Drivers" -Recurse -Filter *.inf -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -match '\\w11\\ARM64\\' } |
  ForEach-Object { pnputil /add-driver $_.FullName /install }

# Wait for network
$online = $false
for ($i = 0; $i -lt 90 -and -not $online; $i++) {
  try { $online = (Invoke-WebRequest -UseBasicParsing -TimeoutSec 5 http://www.msftconnecttest.com/connecttest.txt).StatusCode -eq 200 } catch { Start-Sleep 5 }
}
Get-NetConnectionProfile | Set-NetConnectionProfile -NetworkCategory Private -ErrorAction SilentlyContinue

# File and printer sharing
Enable-NetFirewallRule -DisplayGroup 'File and Printer Sharing' -ErrorAction SilentlyContinue
Set-Service LanmanServer -StartupType Automatic -ErrorAction SilentlyContinue
Start-Service LanmanServer -ErrorAction SilentlyContinue

# OpenSSH server
if (-not (Get-Service sshd -ErrorAction SilentlyContinue)) {
  for ($i = 0; $i -lt 5 -and -not (Get-Service sshd -ErrorAction SilentlyContinue); $i++) {
    Add-WindowsCapability -Online -Name 'OpenSSH.Server~~~~0.0.1.0'
    if (-not (Get-Service sshd -ErrorAction SilentlyContinue)) { Start-Sleep 20 }
  }
}
if (-not (Get-Service sshd -ErrorAction SilentlyContinue)) { exit 1 }
Set-Service sshd -StartupType Automatic
Start-Service sshd
if (-not (Get-NetFirewallRule -Name 'sshd' -ErrorAction SilentlyContinue)) {
  New-NetFirewallRule -Name 'sshd' -DisplayName 'OpenSSH Server (sshd)' -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22 | Out-Null
}
New-ItemProperty -Path 'HKLM:\SOFTWARE\OpenSSH' -Name DefaultShell -Value 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -PropertyType String -Force | Out-Null
$ak = 'C:\ProgramData\ssh\administrators_authorized_keys'
Set-Content -Path $ak -Value '@PUBKEY@' -Encoding ascii
icacls $ak /inheritance:r /grant 'Administrators:F' /grant 'SYSTEM:F' | Out-Null

New-Item -ItemType File -Path $marker -Force | Out-Null
Unregister-ScheduledTask -TaskName ScanVMSetup -Confirm:$false -ErrorAction SilentlyContinue
