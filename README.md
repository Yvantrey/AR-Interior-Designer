# AR Interior Design & Furniture Visualization Platform

## Description

This project is my mission capstone prototype. It helps users preview how furniture could look and fit in their rooms before purchasing.

Using a compatible Android phone, users can scan real-world surfaces, select virtual furniture, place it in their space, and adjust it. The project focuses on making furniture visualization simple and accessible for users in Kigali, Rwanda.

## Project Links

- GitHub repository: https://github.com/Yvantrey/AR-Interior-Designer 
- Figma prototype: 
- Video demonstration: 

## Current Features

- Detect real-world surfaces using AR Foundation.
- Browse and select furniture.
- Tap a detected surface to place furniture.
- Select placed furniture.
- Move, rotate, and scale furniture.
- Delete individual furniture items.
- Reset the scene.
- Access a main menu and instructions.

## Tools and Requirements

### Development Tools
- Unity 6 — exact version: 6000.6.0f1
- C#
- AR Foundation
- ARCore XR Plugin
- Android Build Support, including SDK, NDK, and OpenJDK
- Git and GitHub
- Figma for interface design and prototyping

### Testing Hardware
- An ARCore-supported Android phone with a camera.
- A USB cable for installing and testing the app.

## Environment and Project Setup

1. Clone the repository:

   git clone https://github.com/Yvantrey/AR-Interior-Designer 

2. Install Unity Hub and the Unity Editor version listed above.
3. In Unity Hub, install Android Build Support for that Editor version, including the Android SDK, NDK, and OpenJDK.
4. In Unity Hub, select Add and choose the cloned project folder.
5. Open the project and allow Unity to import its assets and restore packages.
6. Check that AR Foundation and ARCore XR Plugin are installed in Package Manager.
7. In XR Plug-in Management, confirm that ARCore is enabled for Android.
8. Open the MainMenu scene.

### Build and Run on Android

1. Connect a compatible Android phone through USB.
2. Enable Developer Options and USB debugging on the phone.
3. Open Unity's Build Profiles and select Android.
4. Confirm that MainMenu and the AR gameplay scene are included, with MainMenu first.
5. Select the connected device and choose Build and Run.
6. Allow camera access when the app requests it.

Real-world tracking and placement should be tested on a supported phone.

## How to Use

1. Open the app and select Start AR.
2. Move the phone slowly to detect a surface.
3. Select an item from the furniture catalog.
4. Tap a detected surface to place it.
5. Select the placed furniture to access its controls.
6. Move, rotate, or resize the furniture.
7. Use Delete to remove an item or Reset to clear the scene.

Resizing changes the preview size. Use the original model dimensions when assessing the furniture's actual fit.

## Interface Design and Navigation

Figma prototype: [Add Figma link]

### Design Images and App Screenshots



## Assets and Their Purpose

| Asset | Purpose |
|---|---|
| 3D furniture models | Allow users to preview furniture in their rooms |
| Materials and textures | Show the appearance of furniture |
| Catalog thumbnails | Help users identify and select items |
| Placement indicator | Show where furniture can be placed |
| UI buttons and icons | Support navigation and furniture controls |

Asset sources and licences: [Add sources, creators, and licence details]

Furniture included in this version: [List your actual furniture models]

## Hardware Interaction

The phone camera provides the real-world view, while AR Foundation and ARCore support surface detection and tracking.

Users move the phone to scan their surroundings and use the touchscreen to select, place, and adjust furniture. A VR headset or separate controller is not required.

## Project Structure

- Assets/ — scenes, scripts, furniture models, prefabs, materials, and UI.
- Packages/ — package dependencies.
- ProjectSettings/ — Unity project configuration.
- Docs/ — design exports, screenshots, and supporting documentation.
- README.md — project overview and setup instructions.

## Deployment Plan

The initial version will be distributed as an Android APK for supervisor feedback and testing on compatible phones.

After feedback, I plan to improve navigation, placement stability, and performance, and expand the furniture catalog. Further testing will support the planned pilot study in Kigali.


## Current Limitations and Next Steps

This is an initial prototype.

Known issues: [Describe any actual problems or unfinished features]

Planned improvements:
- Refine the interface using feedback.
- Expand the furniture catalog.
- Improve model scale accuracy and placement stability.
- Test performance across compatible Android devices.
- Conduct the planned usability evaluation.

## Author

Yvan Rugamba  