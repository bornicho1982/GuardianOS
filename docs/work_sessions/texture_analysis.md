# Análisis Completo de Texturas y Sistema de Colores de Destiny 2

## 📁 Estructura de Archivos de ColladaGenerator

Cuando extraes una armadura, ColladaGenerator produce:

```
TextureCache/{itemHash}/DestinyModel0/
├── model.dae                    # Modelo 3D en formato Collada
├── Male-_diffuse_0.png          # Textura difusa (colores base)
├── Male-_normal_0.png           # Normal map (relieve 3D)
├── Male-_gearstack_0.png        # Datos de material (MRC)
├── Male-_dyeslot_0.png          # ⭐ MÁSCARA DE COLOR (CRÍTICO)
├── Female-_*                    # Mismas texturas para modelo femenino
├── Raws/
│   └── Male-/0-render_metadata.js    # Metadatos del modelo
├── Shaders/
│   └── Blender/.py              # ⭐ SHADER DE BLENDER CON COLORES
└── Textures/
    └── *.png                    # Texturas de detalle adicionales
```

---

## 🎨 Sistema de 6 Slots de Color

El sistema de Destiny 2 tiene **6 slots** de color (no 3):

| # | Slot | Descripción | Material por Defecto |
|---|------|-------------|---------------------|
| 1 | **Armor Primary** | Metal principal (casco, hombreras) | Metálico (1.0) |
| 2 | **Armor Secondary** | Metal secundario (detalles) | Metálico (1.0) |
| 3 | **Cloth Primary** | Tela principal (capas, trapos) | No metálico (0) |
| 4 | **Cloth Secondary** | Tela secundaria | No metálico (0) |
| 5 | **Suit Primary** | Traje/undersuit (material flexible) | Ligeramente metálico (0.1) |
| 6 | **Suit Secondary** | Detalle de traje | Ligeramente metálico (0.1) |

Cada slot tiene versión **WORN** (desgastada).

---

## 📊 Propiedades de Cada Slot de Color

Ejemplo de datos extraídos del shader de Blender:

```python
# Armor Primary Slot
armorprimarydyecolor = (0.486687, 0.339099, 0.297911, 1.0)  # Color RGB+A
armorprimaryroughnessremapX = 1.084525
armorprimaryroughnessremapY = -1.013094
armorprimaryroughnessremapZ = 0
armorprimaryroughnessremapW = 0.756757
armorprimarywearremapX = -4
armorprimarywearremapY = 8
armorprimarydetaildiffuseblend_raw = 0.592342
armorprimarydetailnormalblend_raw = 0.783784
armorprimarydetailroughnessblend_raw = 0.504505
armorprimarymetalness = 1                  # ⭐ 100% metálico
armorprimaryiridescence = -1               # Sin iridiscencia
armorprimaryfuzz = 0                       # Sin efecto pelusa
armorprimarytransmission = 0               # Sin transparencia
armorprimaryemissioncolor = (1, 0.090909, 0.487603, 1.0)

# Cloth Primary - diferente!
clothprimarymetalness = 0                  # ⭐ NO metálico
```

---

## 🗺️ La Textura Dyeslot (CRÍTICA)

El archivo `dyeslot_0.png` es una **máscara RGB** que indica qué slot aplicar:

| Canal | Valor | Significado |
|-------|-------|-------------|
| **R** | Alto | Armor (metálico) |
| **G** | Alto | Cloth (tela) |
| **B** | Alto | Suit (traje) |
| **Alpha** | Valor | Primary vs Secondary |

Interpretación:
- Si píxel = (255, 0, 0) → **Armor Primary**
- Si píxel = (128, 0, 0) → **Armor Secondary**
- Si píxel = (0, 255, 0) → **Cloth Primary**
- Si píxel = (0, 128, 0) → **Cloth Secondary**
- Si píxel = (0, 0, 255) → **Suit Primary**
- etc.

---

## 🎯 El Problema Actual

Nuestro shader actual hace:
1. ✅ Carga colores de la API (pero solo 3 slots: 0, 1, 2)
2. ❌ **NO usa la textura dyeslot** como máscara
3. ❌ **NO distingue** Armor/Cloth/Suit (son materiales diferentes)
4. ❌ **NO aplica** metalness/roughness por slot
5. ❌ El blend mode es incorrecto

---

## 🔧 Solución Propuesta

### Opción A: Usar las texturas extraídas localmente
1. Servir `dyeslot_0.png` desde TextureCache
2. Samplear el dyeslot en el shader
3. Usar los canales RGB para determinar qué color aplicar
4. Aplicar metalness según el slot

### Opción B: Replicar el shader de Blender en Three.js
- Traducir el script de Blender a WebGL/GLSL
- Implementar los 6 slots completos
- Usar roughness remap, wear remap, etc.

---

## 📝 Siguiente Paso

Necesitamos:
1. **Cargar la textura dyeslot** correctamente
2. **Modificar el shader** para usar dyeslot como máscara de selección de color
3. **Aplicar metalness diferente** para áreas Armor vs Cloth vs Suit

El color azul vibrante del juego viene de aplicar el `dyeColor` con el `metalness = 1` correctamente.
