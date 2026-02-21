#!/bin/bash

sudo cp /home/pi/BrothermanBill/brothermanbill.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable brothermanbill.service

echo "brothermanbill.service installed and enabled."
