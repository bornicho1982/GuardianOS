# Unity HDRP Viewer Migration Plan

## Goal
Replace the Three.js-based 3D viewer with Unity HDRP for maximum visual fidelity, replicating Destiny 2's exact rendering pipeline.

---

## Phase 1: Cleanup (Current Project)

### Files to Remove/Deprecate
- `Assets/3DViewer/D2TGXLoader.js` - Three.js loader (deprecated)
- `Assets/3DViewer/DestinyDyeShader.js` - Three.js shader (deprecated)
- `Assets/3DViewer/three.tgxmaterial.js` - Three.js material (deprecated)
- `Assets/3DViewer/viewer.js` - Three.js viewer (deprecated)
- `Assets/3DViewer/index.html` - Three.js HTML (deprecated)

### Files to Keep
- `Services/LocalProxyServer.cs` - Still needed for API proxying
- `ViewModels/CharacterDetailViewModel.cs` - Will communicate with Unity
- `Views/CharacterDetailView.xaml` - Will embed Unity viewer

---

## Phase 2: Unity HDRP Project Structure

```
E:\GuardianOS\UnityViewer\
├── Assets/
│   ├── Materials/
│   │   └── DestinyDyeMaterial.mat
│   ├── Shaders/
│   │   ├── DestinyDyeShader.shadergraph
│   │   └── DestinyDyeShader.hlsl (subgraph functions)
│   ├── Scripts/
│   │   ├── ViewerAPI.cs          # External API for WPF
│   │   ├── CharacterLoader.cs    # Loads GLTF/FBX models
│   │   ├── DyeController.cs      # Applies dye colors
│   │   ├── CameraController.cs   # Orbit camera
│   │   └── IPCManager.cs         # Named pipes / WebSocket for WPF
│   ├── Scenes/
│   │   └── ViewerScene.unity
│   ├── Textures/
│   │   └── (loaded at runtime)
│   └── Models/
│       └── (loaded at runtime)
├── Packages/
│   └── manifest.json (HDRP, GLTFast)
└── ProjectSettings/
```

---

## Phase 3: Destiny 2 Dye Shader (Shader Graph)

### Inputs
| Name | Type | Description |
|------|------|-------------|
| BaseMap | Texture2D | Albedo (sRGB) |
| NormalMap | Texture2D | Normal (Linear) |
| MaskMap | Texture2D | ORM: R=AO, G=Rough, B=Metal, A=Smoothness |
| DyeMaskMap | Texture2D | R=Primary, G=Secondary, B=Tertiary |
| DyePrimary | Color | Primary dye color |
| DyeSecondary | Color | Secondary dye color |
| DyeTertiary | Color | Tertiary dye color |
| ClearCoatStrength | Float | 0-1 |
| FresnelStrength | Float | 0-1 |

### Formula (HLSL)
```hlsl
// sRGB to Linear
float3 albedo = pow(BaseMap.rgb, 2.2);

// Dye mixing
float3 dyedColor = 
    albedo * DyePrimary * DyeMask.r +
    albedo * DyeSecondary * DyeMask.g +
    albedo * DyeTertiary * DyeMask.b;

// Fallback for unmapped areas
float totalMask = DyeMask.r + DyeMask.g + DyeMask.b;
if (totalMask < 0.1) dyedColor = albedo * DyePrimary * 0.8;

// HDRP handles: AO, Roughness, Metalness, Normal, Fresnel, Clear Coat
```

---

## Phase 4: WPF Integration Options

### Option A: Embedded Window (Recommended for Desktop)
- Unity builds as standalone `.exe`
- WPF embeds Unity window using `WindowsFormsHost` or `HwndHost`
- Communication via Named Pipes or WebSocket

### Option B: WebGL Build
- Unity builds to WebGL
- WPF loads in WebView2
- Communication via JavaScript interop

### Option C: Render to Texture
- Unity renders to shared texture
- WPF displays texture in D3DImage
- Most complex but smoothest integration

---

## Phase 5: API Contract (IPC)

### Commands (WPF → Unity)
```json
{ "action": "loadModel", "path": "E:/D2_Exports/Helmet.fbx" }
{ "action": "setDyes", "slot": 0, "primary": [0.8,0.2,0.1], "secondary": [0.1,0.1,0.1] }
{ "action": "setCamera", "distance": 3.0, "height": 1.5 }
{ "action": "rotate", "angle": 45.0 }
```

### Events (Unity → WPF)
```json
{ "event": "modelLoaded", "success": true }
{ "event": "error", "message": "Failed to load texture" }
```

---

## Phase 6: Implementation Order

1. [ ] **Cleanup**: Archive/remove deprecated Three.js files
2. [ ] **Unity Setup**: Create HDRP project with packages
3. [ ] **Shader**: Create Destiny Dye Shader Graph
4. [ ] **Scripts**: CharacterLoader, DyeController, ViewerAPI
5. [ ] **Scene**: Setup lighting, camera, post-processing
6. [ ] **IPC**: Implement Named Pipes communication
7. [ ] **WPF Integration**: Embed Unity window in CharacterDetailView
8. [ ] **Testing**: Verify full pipeline works
9. [ ] **Optimization**: LOD, texture streaming, startup time

---

## Required Unity Packages

```json
{
  "dependencies": {
    "com.unity.render-pipelines.high-definition": "16.0.5",
    "com.unity.shadergraph": "16.0.5",
    "com.atteneder.gltfast": "6.0.1",
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  }
}
```

---

## User Review Required

> [!IMPORTANT]
> Before proceeding, please confirm:
> 1. Do you have **Unity 2022.3 LTS or newer** installed?
> 2. Do you prefer **Embedded Window** or **WebGL** integration?
> 3. Should I **delete** the old Three.js viewer files or **archive** them?
