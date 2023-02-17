#!/bin/bash

pkill -f "[d]otnet Brotherman"
pkill -f "Lavalink.jar"

echo "Process killed" #stops STDOUT from hanging and causing error

sudo systemctl start brothermanbill.service