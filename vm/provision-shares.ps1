# Creates SMB shares and scanuser SSH key login. Run as administrator on the guest. Argument: public key text.
param([Parameter(Mandatory)][string]$PubKey)
New-Item -ItemType Directory -Force C:\shares\files\scanuser, C:\shares\it\quickattach | Out-Null
icacls C:\shares\files /grant 'scanuser:(OI)(CI)M' | Out-Null
icacls C:\shares\it\quickattach /grant 'scanuser:(OI)(CI)RX' 'scanadmin:(OI)(CI)F' | Out-Null
foreach ($n in 'files', 'it$') { Remove-SmbShare -Name $n -Force -ErrorAction SilentlyContinue }
New-SmbShare -Name files -Path C:\shares\files -ChangeAccess scanuser -FullAccess scanadmin | Out-Null
New-SmbShare -Name 'it$' -Path C:\shares\it\quickattach -ReadAccess scanuser -FullAccess scanadmin | Out-Null

$ssh = 'C:\Users\scanuser\.ssh'
New-Item -ItemType Directory -Force $ssh | Out-Null
Set-Content -Path "$ssh\authorized_keys" -Value $PubKey -Encoding ascii
icacls $ssh /inheritance:r /grant 'scanuser:(OI)(CI)F' 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)F' | Out-Null
