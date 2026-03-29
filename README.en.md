# FlowWheel

<div align="center">

 <img src="https://github.com/humanfirework/FlowWheel/raw/main/Assets_for_GitHub_Readme/1.gif" width="30%" alt="Demo 1" />
 <img src="https://github.com/humanfirework/FlowWheel/raw/main/Assets_for_GitHub_Readme/2.gif" width="30%" alt="Demo 2" />
 <img src="https://github.com/humanfirework/FlowWheel/raw/main/Assets_for_GitHub_Readme/3.gif" width="30%" alt="Demo 3" />

 <br>

 [![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
 [![Build Status](https://github.com/humanfirework/FlowWheel/actions/workflows/build.yml/badge.svg)](https://github.com/humanfirework/FlowWheel/actions)
[![Version](https://img.shields.io/badge/version-1.7.0-green.svg)](https://github.com/humanfirework/FlowWheel/releases)

</div>

---

## English

**FlowWheel** is a powerful Windows utility that brings smooth, browser-style "Middle-Click Auto-Scroll" to the entire operating system. It now features advanced productivity tools like **Inertia Scrolling**, **Multi-Screen Sync** and **Reading Mode**.

### New in v1.7.0
- **Custom Acceleration Curve**: Users can now create and adjust custom acceleration curves by dragging control points. Supports multiple curve types including Linear, Exponential, Logarithmic, Sigmoid, and fully custom curves.
- **Fixed Custom Curve Dragging**: Resolved the issue where custom curve control points could not be dragged to adjust the curve shape.

### Key Features

- **Universal Auto-Scroll**: Works in File Explorer, Word, IDEs, Discord, and almost any Windows application.
- **Inertia Physics**: Hold middle button to drag the page, release to throw it with inertia.
- **Reading Mode (Teleprompter)**: **Double-click** the middle mouse button to activate hands-free automatic scrolling. Perfect for reading long docs or logs while eating!
- **Multi-Screen Sync**: Scroll a document on your main screen, and reference documents on other screens (or side-by-side windows) will scroll in sync. Ideal for code reviews and translation.
- **Dynamic Speed**: Non-linear speed control—the further you move from the anchor, the faster it scrolls.
- **Smart Opacity**: The overlay anchor automatically fades out when your mouse is close to it or when moving fast, preventing text occlusion.
- **Modern UI**: Beautiful overlay with direction indicators and custom themes.

### Architecture

FlowWheel is built as a small set of focused modules with a clear event/data flow:

1) **Input Capture (Global Hooks)**
- `MouseHook` / `KeyboardHook`: Low-level global input hooks (User32) that receive raw OS events.

2) **Interaction Orchestration**
- `AutoScrollManager`: Interprets trigger mode (Toggle / Hold & Drag), starts/stops states, updates overlay, routes data to the engine.
- `WindowManager`: Detects the target window/process under cursor and applies blacklist/whitelist rules.

3) **Motion & Physics**
- `ScrollEngine`: Calculates scroll speed, handles inertia/reading mode, and emits wheel events via `SendInput`.
- `SyncScrollManager`: Optional multi-window/multi-monitor synchronized scrolling.

4) **UI & Feedback**
- `OverlayWindow`: Transparent overlay for anchor + direction indicators + reading mode state.
- `SettingsWindow`: User-facing configuration UI.

5) **Config & Updates**
- `ConfigManager`: Loads/saves persistent settings (`config.json`).
- `UpdateManager`: Checks latest GitHub Release and opens the release/asset download link.

**Runtime flow (simplified)**
`MouseHook/KeyboardHook` → `AutoScrollManager` → `ScrollEngine` → `SendInput (Wheel/HWheel)` → (optional) `SyncScrollManager`

### Installation

#### ~~Method 1: Scoop (Recommended)~~
You can easily install FlowWheel directly from this repository:

```powershell
scoop install https://github.com/humanfirework/FlowWheel/raw/main/flowwheel.json
```

To update:
```powershell
scoop update flowwheel
```

#### Method 2: Manual Download
**(In the settings interface, "one-click check/automatic check" update and jump download are already supported. Be careful not to use a ladder, which may show API problems)**

Download the latest `FlowWheel.exe` from the [Releases](https://github.com/humanfirework/FlowWheel/releases) page. No installation required, just run it!

### Settings & Configuration

Manage all your preferences in the new Settings dashboard:

<div align="center">
 <img src="https://github.com/humanfirework/FlowWheel/raw/main/Assets_for_GitHub_Readme/4.png" width="250" alt="FlowWheel Settings" />
</div>

- **Trigger Mode**:
    - **Click Toggle**: Classic behavior. Click to start, click to stop.
    - **Hold & Drag**: Physics mode. Hold to drag, release to throw.
- **Sensitivity**: Fine-tune scroll speed and deadzone.
- **App Filtering**:
    - **Blacklist**: Disable auto-scroll for specific games (e.g., CS:GO).
    - **Whitelist**: Only enable auto-scroll for specific apps.
    - **Drag & Drop**: Drag `.exe` files directly into the list to add them.
- **Global Hotkey**: Click the box and press your desired keys to set a toggle shortcut.

### Usage Guide

1.  **Auto-Scroll**: Click **Middle Mouse Button** once. Move mouse to scroll. Click again to stop (or release to throw if inertia is active).
2.  **Reading Mode**: **Double-click** Middle Mouse Button.
   *   Use **Mouse Wheel** to adjust reading speed on the fly.
   *   Click any button to stop.
3.  **Sync Scroll**: Enable it in Settings. Open two documents (on different screens or side-by-side). Start scrolling one, and the other follows!

---

## License

This project is licensed under the [MIT License](LICENSE).

## Buy me a coffee

If you find this project helpful, feel free to buy me a coffee! ☕

<div align="center">
 <img src="https://github.com/humanfirework/FlowWheel/raw/main/Assets/alipay_qr.png" alt="Alipay" width="180" style="max-width: 100%; height: auto;" />
 <br>
 <span>Scan to Donate with Alipay</span>
</div>
