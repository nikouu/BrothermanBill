#!/bin/bash

pkill -f "[d]otnet Brotherman"
pkill -f "Lavalink.jar"

echo "Process killed" #stops STDOUT from hanging and causing error

# https://superuser.com/questions/513159/how-to-remove-systemd-services

if systemctl list-unit-files | grep brothermanbill.service; then 

	if systemctl is-active --quiet brothermanbill.service; then
		sudo systemctl stop brothermanbill.service
	fi

	if systemctl is-enabled --quiet brothermanbill.service; then
		sudo systemctl disable brothermanbill.service
	fi
fi

if [ -f "/etc/systemd/system/brothermanbill.service" ] ; then
	rm "/etc/systemd/system/brothermanbill.service"
fi

if [ -f "/usr/lib/systemd/system/brothermanbill.service" ] ; then
	rm "/usr/lib/systemd/system/brothermanbill.service"
fi

sudo systemctl daemon-reload
sudo systemctl reset-failed