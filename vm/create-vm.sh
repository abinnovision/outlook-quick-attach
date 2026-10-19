#!/bin/bash
# Creates the ScanAttach-Win11 UTM VM (QEMU arm64, 8 GB RAM, 4 cores, 80 GB NVMe, TPM 2.0, UEFI) with both ISOs attached.
set -euo pipefail
cd "$(dirname "$0")"
WIN_ISO="${WIN_ISO:-$HOME/Downloads/scanattach-vm/Win11_arm64_en-us.iso}"
ID=$(osascript <<OSA
tell application "UTM"
  set vm to make new virtual machine with properties {backend:qemu, configuration:{name:"ScanAttach-Win11", architecture:"aarch64", machine:"virt", memory:8192, cpu cores:4, uefi:true, hypervisor:true, drives:{{removable:true, interface:USB}, {removable:true, interface:USB}, {guest size:81920, interface:NVMe}}, network interfaces:{{hardware:"virtio-net-pci", mode:shared}}, displays:{{hardware:"virtio-ramfb"}}}}
  return id of vm
end tell
OSA
)
B="$HOME/Library/Containers/com.utmapp.UTM/Data/Documents/ScanAttach-Win11.utm"
cp -c "$WIN_ISO" "$B/Data/win11.iso"
cp -c build/unattend.iso "$B/Data/unattend.iso"
plutil -replace Drive.0.ImageName -string win11.iso "$B/config.plist"
plutil -replace Drive.1.ImageName -string unattend.iso "$B/config.plist"
plutil -replace QEMU.TPMDevice -bool true "$B/config.plist"
# QMP/VNC on localhost for unattended key input and screenshots during install
plutil -replace QEMU.AdditionalArguments -json '["-qmp","tcp:127.0.0.1:4444,server=on,wait=off","-vnc","127.0.0.1:7"]' "$B/config.plist"
osascript -e "tell application \"UTM\" to tell virtual machine id \"$ID\" to reload configuration"
echo "$ID"
