# GuardianOS - Documentación Completa del Proyecto

## 📌 Resumen Global de la Aplicación

**GuardianOS** es una aplicación de escritorio WPF (.NET) para Destiny 2 que proporciona:
- Autenticación OAuth con la API de Bungie
- Dashboard con información del jugador
- Vista de personajes (Titán, Cazador, Hechicero)
- **Visor 3D del Guardián** con modelo real del personaje
- Sistema de inventario

### Stack Tecnológico
- **Frontend**: WPF + XAML
- **Backend**: C# .NET 10
- **3D Viewer**: Three.js + WebView2 (navegador embebido)
- **API**: Bungie.net Destiny 2 API
- **Proxy Local**: ASP.NET Core Kestrel (puerto 5050)

---

## 🎯 Objetivo Actual: Vista de Personaje con Visor 3D

Cuando el usuario entra en la vista de un personaje, queremos:
1. **Cargar el modelo 3D real del guardián** con su armadura equipada
2. **Aplicar los colores del shader** que el jugador tiene equipado
3. Mostrar texturas correctas y geometría completa

---

## ✅ Lo Que Hemos Conseguido

### 1. Modelo 3D Funcionando
- ✅ Geometría del guardián cargando (vértices, triángulos, normales)
- ✅ Texturas de las armaduras aplicadas (gearstack textures)
- ✅ Múltiples meshes combinados (casco, brazos, pecho, piernas, capa)
- ✅ Colores `default_dyes` de cada armadura aplicados

### 2. Extracción de Datos de Shaders
- ✅ Obtenemos los `itemInstanceId` de cada pieza de armadura
- ✅ Solicitamos componentes 205, 300, 302, 304, 305, 307 de la API
- ✅ Extraemos los `plugHash` de los sockets (el shader equipado)
- ✅ Los `shaderHashes` llegan correctamente al visor JavaScript
- ✅ Consola muestra: `[Guardian3D] Shaders: Array(5) Non-zero: 5`

---

## ❌ El Problema Actual: Colores de Shaders

### Estado del Problema
Los colores que se muestran son los **default_dyes de las armaduras** (colores originales), NO los colores del shader equipado que el jugador ha seleccionado.

### ¿Cómo Deberían Funcionar los Colores?

Según la investigación del código de **lowlines (destiny-tgx-loader)**:

```
ARMADURA:
  └── gear JSON → tiene default_dyes, custom_dyes, locked_dyes

SHADER APLICADO:
  └── gear JSON → tiene custom_dyes con los colores del shader

COMBINACIÓN (parseGearDyes):
  1. Tomar default_dyes de la ARMADURA
  2. Sobrescribir con custom_dyes del SHADER ← ESTO ES LO QUE NO FUNCIONA
  3. Respetar locked_dyes de exóticos
```

### Estructura de Datos Esperada
```json
// Gear JSON de la ARMADURA (lo que tenemos):
{
  "default_dyes": [
    { 
      "slot_type_index": 0,
      "material_properties": {
        "primary_albedo_tint": [R, G, B, A],
        "secondary_albedo_tint": [R, G, B, A],
        "worn_albedo_tint": [R, G, B, A]
      }
    }
  ],
  "custom_dyes": [],  // ← VACÍO porque no viene del shader
  "locked_dyes": []
}

// Gear JSON del SHADER (lo que necesitamos):
{
  "default_dyes": [],
  "custom_dyes": [
    { 
      "slot_type_index": 0,
      "material_properties": {
        "primary_albedo_tint": [0.5, 0.1, 0.8, 1.0],
        "secondary_albedo_tint": [0.2, 0.2, 0.9, 1.0]
      }
    }
  ],
  "locked_dyes": []
}
```

---

## 🔍 Intentos Realizados y Resultados

### Intento 1: getGearAsset(shaderHash) en proxy local
**Código**: Consultar base de datos SQLite del Mobile Manifest
**Resultado**: ❌ Retorna `null`
**Motivo**: Mobile Manifest solo tiene `DestinyGearAssetsDefinition` con armaduras/armas, NO shaders.

### Intento 2: API de Bungie directamente
**Código**: `/api/shader/{hash}` → `https://www.bungie.net/Platform/Destiny2/Manifest/DestinyGearAssetsDefinition/{shaderHash}/`
**Resultado**: ❌ `ErrorCode: 1` (Success) pero SIN datos
**Motivo**: Shaders NO tienen entradas en `DestinyGearAssetsDefinition`.

### Intento 3: API externa lowlidev.com.au
**Código**: `https://lowlidev.com.au/destiny/api/gearasset/${shaderHash}?destiny2`
**Resultado**: ❌ Error 500 Internal Server Error
**Motivo**: Servidor caído o API deshabilitada.

---

## 📂 Dónde Podrían Estar los Datos de Shaders

### Opción A: DestinyInventoryItemDefinition
Shaders son items. Sus colores podrían estar en:
- `plug.previewItemOverrideHash`
- Propiedades de `preview`
- Referencias a `DyeReference`

### Opción B: Gear JSON del shader en bungie.net
Ruta: `https://www.bungie.net/common/destiny2_content/geometry/gear/{shaderGearHash}.js`
Problema: NO tenemos forma de obtener `shaderGearHash`.

### Opción C: Manifest Completo
Descargar manifest SQLite completo con `DestinyInventoryItemDefinition`.

---

## 📁 Archivos Clave Modificados

### C# (Backend)
- `CharacterDetailViewModel.cs` - Extrae `ShaderHash` de sockets
- `CharacterDetailView.xaml.cs` - Envía `shaderHashes` a JavaScript
- `LocalProxyServer.cs` - Endpoint `/api/shader/{hash}`
- `GearAssetService.cs` - Consulta SQLite Mobile Manifest
- `BungieApiService.cs` - Logging para debug

### JavaScript (Visor 3D)
- `D2TGXLoader.js` - `loadShaderFromLowlidev()`, `extractDyeColors()`
- `viewer.js` - `loadGuardian()` con shaderHashes

---

## 🔬 Flujo Completo de Datos

```
1. CharacterDetailViewModel.LoadEquipmentAsync()
   → API GetProfileAsync (componentes 205,300,302,304,305,307)
   → Extrae plugHash del socket shader
   → Guarda en item.ShaderHash
   ↓
2. CharacterDetailView.SendGuardianDataToViewer()
   → Construye shaderHashes[]
   → Envía JSON al WebView
   ↓
3. D2TGXLoader.load()
   → getGearAsset(itemHash) ✅ OK
   → getGearAsset(shaderHash) ❌ NULL
   → loadShaderFromLowlidev(shaderHash) ❌ FALLA
   → Fallback: usa default_dyes de armadura
   ↓
4. Modelo renderiza con colores de ARMADURA no de SHADER
```

---

## 🎯 Preguntas para Investigar

1. **¿Dónde están los custom_dyes de los shaders en Destiny 2?**
2. **¿Los shaders tienen su propio gear JSON? ¿Cuál es la ruta?**
3. **¿Los colores están en DestinyInventoryItemDefinition?**
4. **¿Hay endpoint de API que devuelva dyes de shaders?**
5. **¿Cómo lo hace Spasm (herramienta oficial de Bungie)?**
6. **¿Existe base de datos comunitaria de colores de shaders?**

### Términos de Búsqueda Sugeridos
- `Destiny 2 API shader custom_dyes gear JSON`
- `Bungie API DestinyInventoryItemDefinition shader dye colors`
- `Destiny 2 TGX shader dye material_properties primary_albedo_tint`
- `destiny-tgx-loader shader gear asset`
- `Spasm library shader dye application`
- `Destiny 2 shader color database`
- `DIM shader preview colors implementation`

---

## 📊 Logs de Consola

### Lo que vemos (fallando):
```
[Guardian3D] Shaders: Array(5) Non-zero: 5
[D2TGXLoader] Loading shader dyes for hash: 3122197216
[D2TGXLoader] Trying proxy for shader: 3122197216
[D2TGXLoader] Shader API response: {ErrorCode: 1} ← SIN DATOS
```

### Lo que necesitamos ver:
```
[D2TGXLoader] Shader gearAsset: ["gear", "content"]
[D2TGXLoader] Loading shader gear from: http://localhost:5050/api/gear/XXXXX.js
[D2TGXLoader] Got shader dyes! {0: {primary: [...], secondary: [...]}}
```

---

## 🔗 Referencias

- **lowlines destiny-tgx-loader**: https://github.com/lowlines/destiny-tgx-loader
- **Bungie API Docs**: https://bungie-net.github.io/multi/index.html
- **DIM**: https://github.com/DestinyItemManager/DIM
- **Bungie 3D Wiki**: https://github.com/Bungie-net/api/wiki/Obtaining-Destiny-Imagery-and-3D-Content

---

*Documento: 2025-12-16 - Para investigación de colores de shaders*
