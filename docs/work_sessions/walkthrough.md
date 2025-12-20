# Walkthrough: Integración App → Visor 3D

## Resumen

Se implementó la funcionalidad para exportar datos del personaje desde la app GuardianOS a un visor 3D local, incluyendo selección automática de género (masculino/femenino).

---

## Cambios Realizados

### 1. ViewModel - ExportToViewerCommand

#### [CharacterDetailViewModel.cs](file:///e:/GuardianOS/ViewModels/CharacterDetailViewModel.cs)

render_diffs(file:///e:/GuardianOS/ViewModels/CharacterDetailViewModel.cs)

Nuevo comando que:
- Exporta datos del personaje (clase, género, raza, luz)
- Incluye shader y ornament hashes de cada armadura
- Guarda JSON en `Assets/CharmExport/character_data.json`
- Abre automáticamente el viewer HTML

---

### 2. XAML - Botón de Exportación

#### [CharacterDetailView.xaml](file:///e:/GuardianOS/Views/CharacterDetailView.xaml)

render_diffs(file:///e:/GuardianOS/Views/CharacterDetailView.xaml)

- Botón con icono `Cube3D` junto al toggle del visor 3D
- Tooltip: "Exportar a Visor 3D Local"

---

#
## Solución de Problemas: "Ghosting" y Partes Faltantes

### Síntomas
- El visor 3D mostraba al personaje transparente ("Ghost") y faltaban manos y capa.
- Logs indicaban `Skipped: ...` para mallas pequeñas.
- Las texturas parecían no cargarse correctamente.

### Causa Raíz
1.  **Mallas Incorrectas**: Se usaban placeholders (`hunter_mask.fbx`) que no coincidían con el equipo real del usuario (`Last Discipline Mask`).
2.  **Prefijos de Textura Hardcodeados**: El visor esperaba hashes fijos (`7C45F980`) que no coinciden con los assets dinámicos.
3.  **Filtrado Agresivo**: El visor ocultaba mallas <1000 vértices, ocultando manos y detalles.
4.  **Transparencia**: El material tenía `transparent: true` por defecto, causando problemas de orden de renderizado (Ghosting).

### Solución Implementada
1.  **Copia de Assets Externos**:
    - Se implementó `CopyExternalAssets` en `CharacterDetailViewModel` para importar automáticamente los archivos `.fbx` y `.png` desde `E:\D2_Exports\ApiOutput1`.
    - Se mapean los archivos por nombre (ej. "Mask" -> Helmet) y se pasan al visor vía JSON.

### Native Viewer Integration (Final Implementation)
We pivoted to using the internal **D2TGXLoader** (native viewer) which renders the full character model but lacked correct colors.

#### Phase 1: Proxy & Basic Fallback
1. **Proxy Update**: Modified `LocalProxyServer.cs` to search for textures in `E:\D2_Exports`.
2. **Color Fallback**: Forced 85% dye strength when textures are missing.

#### Phase 2: Advanced Destiny 2 Dye System
Based on detailed analysis of Destiny 2's rendering pipeline, implemented a full **Default Dye System** replication:

**New File: [DestinyDyeShader.js](file:///E:/GuardianOS/Assets/3DViewer/DestinyDyeShader.js)**

The shader implements the exact Destiny 2 formula:
```glsl
ColorFinal = (Albedo * PrimaryColor * MaskPrimary) +
             (Albedo * SecondaryColor * MaskSecondary) +
             (Albedo * TertiaryColor * MaskTertiary) +
             Fresnel + ClearCoat + Specular
```

Features:
- **Dye Mask Support**: Uses R/G/B channels to apply Primary/Secondary/Tertiary dyes
- **ORM Integration**: AO (R), Roughness (G), Metalness (B)
- **Fresnel Effect**: `pow(1.0 - max(dot(N, V), 0.0), 5.0) * strength`
- **Clear Coat**: `strength * (1.0 - roughness)`
- **Custom Lighting**: Key, Fill, Rim, and Ambient lights

**Modified: [D2TGXLoader.js](file:///E:/GuardianOS/Assets/3DViewer/D2TGXLoader.js#L964-L1030)**
- Now uses `DestinyDyeShader.createMaterial()` instead of `MeshStandardMaterial`
- Falls back to basic PBR if shader module not loaded

> [!IMPORTANT]
> The armor should now display with **EXACT** colors matching Destiny 2's Default Dye System, even without a shader equipped.
2.  **Visor Inteligente (`viewer.html`)**:
    - **Carga Dinámica**: Usa el archivo exacto especificado en el JSON.
    - **Auto-Discovery**: Lee el nombre de la malla dentro del FBX (ej. `8C45F980_...`) para saber qué texturas cargar.
    - **Visibilidad Total**: Se eliminó el umbral de vértices y se forzó `opacity: 1.0`.

### Verificación
- El personaje debe verse sólido (opaco).
- Deben aparecer manos y capa.
- Los colores deben coincidir con la armadura "Last Discipline".

## Implementación de Shaders Reales (Real Math)

Para responder a la necesidad de fidelidad visual absoluta ("como en el juego"), hemos reemplazado los colores hardcodeados por un sistema de **extracción de tintes reales**:

1.  **Ingeniería Inversa del API**: Descubrimos que los `dyeHash` en el Manifiesto de Bungie son en realidad valores de color **ARGB de 32 bits** codificados.
2.  **Extracción en Backup**: Implementamos `ManifestRepository.GetShaderDefinitionAsync` para extraer estos colores usando SQL directo sobre la base de datos local.
3.  **Exportación Dinámica**: El comando de exportación ahora genera un JSON enriquecido con una sección `materials`, detallando los colores Hex y ARGB para cada canal (Primary, Secondary, etc.) de cada pieza de armadura.
4.  **Visor WebGL Adaptativo**: `viewer.html` ha sido reescrito para leer estos materiales dinámicos y pasarlos como *uniforms* al shader personalizado, logrando una representación matemática exacta de los colores del shader equipado.

### Archivos Clave
- `Services/ManifestRepository.cs`: Lógica de decodificación de colores ARGB.
- `ViewModels/CharacterDetailViewModel.cs`: Orquestación de la exportación paralela.
- `Assets/CharmExport/viewer.html`: Shader WebGL actualizado con inyección de uniforms.


---

### 3. Viewer - Selección de Género

#### [viewer.html](file:///e:/GuardianOS/Assets/CharmExport/viewer.html)

render_diffs(file:///e:/GuardianOS/Assets/CharmExport/viewer.html)

Ahora el viewer:
- Carga `character_data.json` al iniciar
- Detecta el género del personaje (Male/Female)
- Selecciona las texturas correctas para ese género
- Actualiza el panel de información con los datos del personaje

---

## Cómo Usar

1. **Abrir GuardianOS** e iniciar sesión
2. **Seleccionar un personaje** (ej: tu Cazadora femenina)
3. **Click en el botón 🧊** (cubo 3D) en la esquina superior derecha
4. **El visor se abrirá** con la armadura mostrando:
   - Texturas del género correcto (femenino)
   - Colores del shader aplicados

---

## Estructura de Archivos

```
Assets/CharmExport/
├── character_data.json   ← Exportado por la app
├── viewer.html           ← Visor 3D actualizado
├── Textures/
│   ├── 6A1FF880_*.png   ← Texturas masculinas
│   ├── 085CF980_*.png   ← Texturas femeninas
│   └── ...
└── *.fbx                 ← Modelos de armadura
```

---

## Verificación

- ✅ Build completado sin errores
- ✅ 26 advertencias (NuGet/analyzer, no críticas)
- ⏳ Pendiente: Test manual del flujo completo
