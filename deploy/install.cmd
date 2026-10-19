@echo off
rem Installs or updates QuickAttach for the current user. Safe to run at every logon.
setlocal
set "MSI=%~dp0QuickAttach.msi"

rem Skip when the installed version is the same as or newer than the MSI.
set "CHECK=$i=New-Object -ComObject WindowsInstaller.Installer;$d=$i.GetType().InvokeMember('OpenDatabase','InvokeMethod',$null,$i,@($env:MSI,0));$q=$d.GetType().InvokeMember('OpenView','InvokeMethod',$null,$d,@('SELECT Value FROM Property WHERE Property=''ProductVersion'''));[void]$q.GetType().InvokeMember('Execute','InvokeMethod',$null,$q,$null);$r=$q.GetType().InvokeMember('Fetch','InvokeMethod',$null,$q,$null);$m=[version]$r.GetType().InvokeMember('StringData','GetProperty',$null,$r,1);$k=Get-ItemProperty 'HKCU:\Software\abigroup\QuickAttach' -Name InstalledVersion -ErrorAction SilentlyContinue;if($k -and [version]$k.InstalledVersion -ge $m){exit 0};exit 1"
powershell -NoProfile -ExecutionPolicy Bypass -Command "%CHECK%"
if %ERRORLEVEL% equ 0 exit /b 0

msiexec /i "%MSI%" /qn /norestart REBOOT=ReallySuppress CONFIGPATH="%~dp0config.json" /l*v "%TEMP%\QuickAttach-install.log"
exit /b %ERRORLEVEL%
