#!/bin/bash
# Builds build/unattend.iso (Autounattend.xml, setup scripts, VirtIO arm64 drivers) from the templates and credentials.
set -euo pipefail
cd "$(dirname "$0")"
GT_ISO="${GT_ISO:-$HOME/Downloads/scanattach-vm/utm-guest-tools.iso}"
IMAGENAME="${IMAGENAME:-Windows 11 Pro}"
KEYBOARD="${KEYBOARD:-0407:00000407}"
ADMINPW=$(sed -n 's/^scanadmin.*password: //p' credentials.local.txt)
USERPW=$(sed -n 's/^scanuser.*password: //p' credentials.local.txt)
PUBKEY=$(cat vm_ed25519.pub)
rm -rf build && mkdir -p build/stage/Drivers && umask 077
sed -e "s|@ADMINPW@|$ADMINPW|g" -e "s|@USERPW@|$USERPW|g" -e "s|@IMAGENAME@|$IMAGENAME|" -e "s|@KEYBOARD@|$KEYBOARD|" \
  autounattend.template.xml > build/stage/Autounattend.xml
sed -e "s|@PUBKEY@|$PUBKEY|" setup.ps1 > build/stage/setup.ps1
cat > build/stage/SetupComplete.cmd <<'CMD'
schtasks /create /tn ScanVMSetup /sc onstart /ru SYSTEM /rl highest /f /tr "powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Windows\Setup\Scripts\setup.ps1"
schtasks /run /tn ScanVMSetup
CMD
MNT=$(mktemp -d)
hdiutil attach -readonly -nobrowse -mountpoint "$MNT" "$GT_ISO" >/dev/null
for d in NetKVM vioserial viogpudo vioinput viorng Balloon pvpanic; do
  mkdir -p "build/stage/Drivers/$d/w11" && cp -R "$MNT/Drivers/$d/w11/ARM64" "build/stage/Drivers/$d/w11/"
done
cp "$MNT"/utm-guest-tools-*.exe build/stage/
hdiutil detach "$MNT" >/dev/null
hdiutil makehybrid -iso -joliet -default-volume-name SCANUNATTEND -o build/unattend.iso build/stage
ls -la build/unattend.iso
