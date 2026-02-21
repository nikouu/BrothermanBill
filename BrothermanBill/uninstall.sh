#!/bin/bash

# Stop the service and processes
if systemctl is-active --quiet brothermanbill.service; then
    sudo systemctl stop brothermanbill.service
fi

pkill -f "[d]otnet Brotherman"
pkill -f "Lavalink.jar"

echo "Process killed"

# Disable and remove the service
if systemctl is-enabled --quiet brothermanbill.service 2>/dev/null; then
    sudo systemctl disable brothermanbill.service
fi

if [ -f "/etc/systemd/system/brothermanbill.service" ]; then
    sudo rm "/etc/systemd/system/brothermanbill.service"
fi

if [ -f "/usr/lib/systemd/system/brothermanbill.service" ]; then
    sudo rm "/usr/lib/systemd/system/brothermanbill.service"
fi

sudo systemctl daemon-reload
sudo systemctl reset-failed

echo "brothermanbill.service uninstalled."
