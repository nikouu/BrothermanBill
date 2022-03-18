#!/bin/bash


sudo cp /home/pi/BrothermanBill/brothermanbill.service /etc/systemd/system/

sudo systemctl enable brothermanbill.service
sudo systemctl start brothermanbill.service