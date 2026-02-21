#!/bin/bash

if systemctl is-active --quiet brothermanbill.service; then
    sudo systemctl stop brothermanbill.service
fi

pkill -f "[d]otnet Brotherman"
pkill -f "Lavalink.jar"

echo "brothermanbill.service stopped."