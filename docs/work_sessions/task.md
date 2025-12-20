# Task: Unity HDRP Viewer Migration

## Completed
- [x] Archive Three.js viewer files to `_deprecated/3DViewer_ThreeJS/`
- [x] Create Unity project structure `UnityViewer/`
- [x] Create package manifest with HDRP, GLTFast, JSON packages
- [x] Implement ViewerAPI.cs (Named Pipes IPC)
- [x] Implement CharacterLoader.cs (GLTF/FBX loading)
- [x] Implement DyeController.cs (Destiny dye system)
- [x] Implement CameraController.cs (Orbit camera)
- [x] Implement MainThreadDispatcher.cs (Thread utility)
- [x] Create DestinyDyeFunctions.hlsl (Shader HLSL)
- [x] Create ShaderGraph_Setup.md (Shader instructions)
- [x] Create README.md (Project documentation)
- [x] Create UnityViewerBridge.cs (WPF IPC client)

## In Progress
- [/] User installing Unity Hub + Unity 2022.3 LTS

## Pending (After Unity Installed)
- [ ] Open project in Unity
- [ ] Create HDRP scene with lighting
- [ ] Create Shader Graph with dye system
- [ ] Create DestinyDyeMaterial
- [ ] Build to executable
- [ ] Integrate into CharacterDetailView.xaml
- [ ] Test full pipeline

## Files Created
- `UnityViewer/Packages/manifest.json`
- `UnityViewer/Assets/Scripts/ViewerAPI.cs`
- `UnityViewer/Assets/Scripts/CharacterLoader.cs`
- `UnityViewer/Assets/Scripts/DyeController.cs`
- `UnityViewer/Assets/Scripts/CameraController.cs`
- `UnityViewer/Assets/Scripts/MainThreadDispatcher.cs`
- `UnityViewer/Assets/Shaders/DestinyDyeFunctions.hlsl`
- `UnityViewer/Assets/Shaders/ShaderGraph_Setup.md`
- `UnityViewer/README.md`
- `Services/UnityViewerBridge.cs`

## Files Archived
- `_deprecated/3DViewer_ThreeJS/` (10 files)

## Completado
- [x] Analizar código existente de GuardianOS
- [x] Confirmar que ShaderHash ya se extrae
- [x] Añadir ExportToViewerCommand en ViewModel
- [x] Añadir botón Cube3D en CharacterDetailView.xaml
- [x] Corregir icono inválido (Cube3D -> CubeOutline)
- [x] Solucionar problema de clicks en WebView2 (mover botones al header)
- [x] Actualizar viewer.html con selección género
- [x] Añadir carga de character_data.json
- [x] Corregir falta de archivos en build (actualizar .csproj)

## En Progreso: Implementación de Shader Real
- [x] Analizar estructura JSON de colores de Shaders (dyemap/dyeHash)
- [x] Implementar `GetShaderDefinitionAsync` en repositorio
- [x] Actualizar `CharacterDetailViewModel` para exportar JSON con materiales reales
- [x] Actualizar `viewer.html` para renderizar colores dinámicos
- [x] Implementar Servidor Local (`LocalViewerServer`) para evitar error CORS
- [x] Depurar extracción de materiales ("Materials loaded: 0") y texturas ("Ghost")
- [x] Analizar exportación externa (`E:\D2_Exports\ApiOutput1`)
- [x] Implementar copia automática de assets externos (`CharacterDetailViewModel`)
- [x] Actualizar `viewer.html` para descubrimiento automático de texturas (Mesh Prefix)
- [-] Solución: Manos faltan y Ghosting (Superseded by Native Viewer)
- [x] **Integración visor nativo (Initial Request)**
    - [x] Localizar `D2TGXLoader` y `LocalProxyServer`
    - [x] Configurar Proxy para buscar texturas externas (`E:\D2_Exports`)
    - [x] Corregir fallback de shader "Sin Color" (Force Dye override)
    - [x] Verificación final

## Sistema de Shader Avanzado (Destiny 2 Default Dye System)
- [x] Crear `DestinyDyeShader.js` con vertex/fragment shaders
    - [x] Primary/Secondary/Tertiary dye mixing
    - [x] ORM (AO, Roughness, Metalness)
    - [x] Fresnel effect
    - [x] Clear Coat effect
    - [x] Custom lighting
- [x] Integrar en `index.html`
- [x] Modificar `D2TGXLoader.js` para usar `DestinyDyeShader.createMaterial()`
- [ ] Verificación visual (comparar con in-game)

## Historial
- [x] Integración App → Viewer 3D (Básico)
- [x] Corrección de bugs de UI (WebView2 z-index)
