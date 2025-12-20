# GuardianOS Development Conversation Log
## Session: Unity HDRP Viewer Migration
## Date: 2025-12-19/20

### Summary
This conversation covers the complete migration from Three.js to Unity HDRP for the GuardianOS 3D character viewer.

### Key Accomplishments

1. **Three.js Viewer Deprecated**
   - Moved all Three.js files to `_deprecated/3DViewer_ThreeJS/`
   - Files: D2TGXLoader.js, DestinyDyeShader.js, viewer.js, etc.

2. **Unity HDRP Project Created**
   - Location: `E:\GuardianOS\UnityViewer\`
   - Scripts: ViewerAPI.cs, CharacterLoader.cs, DyeController.cs, CameraController.cs
   - Shader: DestinyDyeFunctions.hlsl

3. **Unity Setup Completed**
   - Installed Unity 6.3 LTS (6000.3.2f1)
   - Configured HDRP Asset
   - Created ViewerScene
   - Set up ViewerManager with all scripts
   - Connected references between components

4. **Build Completed**
   - Platform: Windows x64
   - Output: `E:\GuardianOS\UnityViewer\Build\`

5. **WPF Integration Bridge Created**
   - `Services/UnityViewerBridge.cs` - Named Pipes IPC client

### Next Steps
- Integrate Unity viewer into WPF via WindowsFormsHost
- Test IPC communication between WPF and Unity
- Add model loading functionality
- Create Shader Graph for Destiny dye system

### Technical Notes
- Unity 6 uses HDRP 17.x instead of 16.x
- GLTFast package requires OpenUPM registry
- Named Pipes used for WPF ↔ Unity communication
