#!/bin/bash

pkill -f "[d]otnet Brotherman"
pkill -f "Lavalink.jar"

echo "Processes killed, restarting..."

sudo systemctl restart brothermanbill.service

echo "brothermanbill.service restarted."