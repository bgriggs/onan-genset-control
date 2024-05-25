![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/bgriggs/onan-genset-control/build.yml)

# Onan Genset Control
This service allows you autostart and stop an Onan generator using a constant run signal such as from a Victron Cerbo GX. In effect, this connects a management system, like Victron, to the generator's three-wire momentary controls.

## Diagram
![alt text](https://github.com/bgriggs/onan-genset-control/blob/main/wiring-diagram.png?raw=true)

## 3-Wire Control
The Onan generator uses a three-wire control system. Here are the states T1-T4 used in the configuration:

```
//                                  _______________
// START ___________________________|   Start T3   |_____running...______________________
//            _____________|delay T2|                                    ____________
// STOP  _____|  Prime T1  |_____________________________running...______|  Stop T4  |___
```

T1: not currently used.<br/>
T2: not currently used.<br/>
T3: Start signal to crank over the starter.<br/>
T4: Stop signal to stop the generator.<br/>

## Hardware
Raspberry Pi 3/4/5<br/>
Relay board: https://www.amazon.com/gp/product/B0BJBDWMM2/ref=ppx_yo_dt_b_search_asin_title?ie=UTF8&psc=1<br/>
Cerbo GX: https://www.amazon.com/Victron-Energy-BPP900450100-Cerbo-GX/dp/B0851KGF57/ref=sr_1_1?sr=8-1<br/>


## OS
https://www.raspberrypi.org/software/operating-systems/ <br/>
Raspberry Pi OS Lite<br/>

## Installation
1. Install the OS on the Raspberry Pi with SSH enabled.
2. Install dotnet 8.0.300, see: https://learn.microsoft.com/en-us/dotnet/iot/deployment		
3. See deploy.txt for example commands to copy over the code and run the service.