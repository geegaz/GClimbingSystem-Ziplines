# GClimbingSystem - Ziplines
An addon for [GClimbingSystem](https://github.com/geegaz/GClimbingSystem), adding an original zipline system for VRChat

![](.img/Ingame_Jump.gif)

## Features

- 4 types of ziplines: 
  - **Simple:** a straight line from the start to the end point
  - **Weighted:** a line curved down by gravity
  - **Curved:** a bezier curve with 2 handles
  - **Swing:** a circular arc using the start point as a pivot
- Editor tools to visualize the lines & the trajectories of the players
- Prefabs with animations, including variants with material for:
  - [Silent's Filamented shaders](https://gitlab.com/s-ilent/filamented)
  - [Orels1's ORL shaders](https://github.com/orels1/orels-Unity-Shaders)

### Limitations
- No splines (only single Bezier curves)
- No constant speed for **Weighted** & **Curved** zipline types

![](.img/Editor_Gizmos.png)

## Installation

**This depends on [GClimbingSystem](https://github.com/geegaz/GClimbingSystem) to work properly ! Make sure you have it installed first.**

Download the repository, then **unpack it in the Assets folder** of your Unity project.
You must have **UdonSharp** installed in your project for this package to work.

UdonSharp has been integrated into the Worlds SDK - if it's not available in your project, check out their documentation for the installation steps: https://udonsharp.docs.vrchat.com/setup

## Prefabs

Name | Description | Path
---|---|---
**Line** | Simple BoosterLine with a line renderer set up | [```/Elements/Line/Line```](./Elements/Line/)
**Booster** | Complete example of what's possible with the Booster | [```/Elements/Booster/Booster```](./Elements/Booster/)
**Pulley Handle** | Version of the Booster prefab more adapted to BoosterLines of type `Simple` | [```/Elements/Handle/Pulley Handle```](./Elements/Handle/)
**Swing Handle** | Version of the Booster more adapted to BoosterLines of type `Swing` | [```/Elements/Handle/Swing Handle```](./Elements/Handle/)
**Booster Flame, Booster Impact, Booster Smoke** | Various particle effects used by the Booster prefab | [```/Models/Booster Ball/Effects/Booster Flame```](./Models/Booster%20Ball/Effects/)<br>[```/Models/Booster Ball/Effects/Booster Impact```](./Models/Booster%20Ball/Effects/)<br>[```/Models/Booster Ball/Effects/Booster Smoke```](./Models/Booster%20Ball/Effects/)

## How to Use

1. Drag the Line prefab in the scene
2. Drag the Booster prefab on the Line (so that it's a child of the Line)
3. Drag the Climbing System from the scene in the corresponding field of the Booster
4. Select the Line and select its type between `Simple, Weighted, Curved or Swing`
5. Move the points of the line to give it the shape you want
6. If needed, increase the baked points precision for longer Lines
6. Ingame, **grab on the Booster to get moved along the Line**

*Instead of the Booster prefab, the Pulley Handle looks great for `Simple` Lines and the Swing Handle for `Swing` Lines !*

