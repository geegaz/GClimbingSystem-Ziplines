# Simple Climbing
A climbing system for VRChat, made with [UdonSharp](https://udonsharp.docs.vrchat.com/)

![](.img/Editor_gizmos.png)

## Features

- Configurable climbing system
- Configurable jumping from climbed wall
- **Original zipline system with custom editor**
- Compatible VR & Desktop

## Installation

Download the repository, then **unpack it in the Assets folder** of your Unity project.
You must have **UdonSharp** installed in your project for this package to work.

If you don't have UdonSharp installed, check out their documentation for the installation steps: https://udonsharp.docs.vrchat.com/setup

This package also comes with alternative materials for [Silent's Filamented shaders](https://gitlab.com/s-ilent/filamented) and [Orels1's ORL shaders](https://github.com/orels1/orels-Unity-Shaders)

## Prefabs

Name | Description | Path
---|---|---
**Climbing System** | Climbing system already set up and ready to use | [![](.img/Folder_Icon.png) ```/Climbing System```](./)
**Line** | Simple BoosterLine with a line renderer set up | [![](.img/Folder_Icon.png) ```/Elements/Line/Line```](./Elements/Line/)
**Booster** | Complete example of what's possible with the Booster | [![](.img/Folder_Icon.png) ```/Elements/Booster/Booster```](./Elements/Booster/)
**Pulley Handle** | Version of the Booster more adapted to BoosterLines of type `Simple` | [![](.img/Folder_Icon.png) ```/Elements/Handle/Pulley Handle```](./Elements/Handle/)
**Swing Handle** | Version of the Booster more adapted to BoosterLines of type `Swing` | [![](.img/Folder_Icon.png) ```/Elements/Handle/Swing Handle```](./Elements/Handle/)
**Booster Flame, Booster Impact, Booster Smoke** | Various particle effects used by the Booster prefab | [![](.img/Folder_Icon.png) ```/Models/Booster Ball/Effects/Booster Flame```](./Models/Booster%20Ball/Effects/)<br>[![](.img/Folder_Icon.png) ```/Models/Booster Ball/Effects/Booster Impact```](./Models/Booster%20Ball/Effects/)<br>[![](.img/Folder_Icon.png) ```/Models/Booster Ball/Effects/Booster Smoke```](./Models/Booster%20Ball/Effects/)

---
![](.img/Ingame_Jump.gif)