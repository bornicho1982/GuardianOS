# GuardianOS Unity HDRP Viewer

High-fidelity 3D character viewer for Destiny 2 using Unity HDRP rendering pipeline.

## Requirements

- Unity 2022.3 LTS or newer
- HDRP (High Definition Render Pipeline)
- Windows 10/11

## Installation

1. Install Unity Hub from https://unity.com/download
2. Install Unity 2022.3 LTS with HDRP template
3. Open Unity Hub → Add → Browse to `E:\GuardianOS\UnityViewer`
4. Open the project
5. When prompted, select "HDRP" as the render pipeline

## Project Structure

```
UnityViewer/
├── Assets/
│   ├── Scripts/
│   │   ├── ViewerAPI.cs          # IPC communication with WPF
│   │   ├── CharacterLoader.cs    # GLTF/FBX model loading
│   │   ├── DyeController.cs      # Destiny dye system
│   │   ├── CameraController.cs   # Orbit camera
│   │   └── MainThreadDispatcher.cs
│   ├── Shaders/
│   │   ├── DestinyDyeFunctions.hlsl
│   │   └── ShaderGraph_Setup.md
│   ├── Materials/
│   │   └── DestinyDyeMaterial.mat (create in Unity)
│   └── Scenes/
│       └── ViewerScene.unity (create in Unity)
├── Packages/
│   └── manifest.json             # HDRP, GLTFast, JSON packages
└── ProjectSettings/
```

## First-Time Setup in Unity

### 1. Create HDRP Scene
1. File → New Scene → Basic Indoors (HDRP)
2. Save as `Assets/Scenes/ViewerScene.unity`

### 2. Setup Lighting
1. Add a Sky and Fog Volume
2. Add an HDRI Sky component
3. Set up Exposure (Auto, 0-16 EV range)

### 3. Create Shader
1. Follow instructions in `Assets/Shaders/ShaderGraph_Setup.md`
2. Create material and assign shader

### 4. Scene Hierarchy
```
ViewerScene
├── Directional Light (Sun)
├── Sky and Fog Volume
├── Main Camera (with CameraController)
├── ViewerManager (GameObject)
│   ├── ViewerAPI.cs
│   ├── CharacterLoader.cs
│   └── DyeController.cs
└── ModelParent (empty transform)
```

### 5. Build Settings
1. File → Build Settings
2. Platform: Windows
3. Architecture: x64
4. Build to: `E:\GuardianOS\UnityViewer\Build\`

## IPC API

The viewer communicates with GuardianOS WPF via Named Pipes.

### Commands (WPF → Unity)

```json
{"action": "loadModel", "path": "E:/D2_Exports/Helmet.gltf"}
{"action": "setDyes", "slot": 0, "primary": [0.8,0.2,0.1], "secondary": [0.1,0.1,0.1]}
{"action": "setCamera", "distance": 3.0, "height": 1.5}
{"action": "rotate", "angle": 45.0}
{"action": "screenshot", "path": "E:/Screenshots/char.png"}
{"action": "ping"}
```

### Events (Unity → WPF)

```json
{"event": "ready", "data": {"version": "1.0"}}
{"event": "modelLoaded", "data": {"success": true, "path": "..."}}
{"event": "dyesApplied", "data": {"slot": 0}}
{"event": "pong", "data": {"timestamp": "..."}}
{"event": "error", "data": {"message": "..."}}
```

## Integration with GuardianOS WPF

After building, update `CharacterDetailView.xaml` to embed the Unity viewer:

1. Remove WebView2 control
2. Add WindowsFormsHost with Unity window handle
3. Create UnityBridge.cs for pipe communication

## Troubleshooting

### Pink/Magenta Materials
- Shader not compiled for HDRP
- Re-open Shader Graph and save

### Model Not Loading
- Check file path in console
- Ensure GLTF/GLB format (FBX requires additional setup)

### IPC Not Connecting
- Ensure only one instance running
- Check pipe name matches in both apps
