# Destiny Dye Shader - Setup Instructions

## Overview
This shader replicates Destiny 2's dye system in Unity HDRP using Shader Graph.

## Creating the Shader Graph

### 1. Create New Shader Graph
1. Right-click in `Assets/Shaders/`
2. Create → Shader Graph → HDRP → Lit Shader Graph
3. Name it `DestinyDyeShader`

### 2. Add Properties
Add these properties to the Blackboard:

| Name | Type | Default |
|------|------|---------|
| _BaseMap | Texture2D | White |
| _NormalMap | Texture2D | Normal (blue) |
| _MaskMap | Texture2D | White |
| _DyeMaskMap | Texture2D | Red |
| _DyePrimary | Color | (0.8, 0.8, 0.8, 1) |
| _DyeSecondary | Color | (0.5, 0.5, 0.5, 1) |
| _DyeTertiary | Color | (0.3, 0.3, 0.3, 1) |
| _ClearCoatStrength | Float (0-1) | 0.2 |
| _FresnelStrength | Float (0-1) | 0.4 |
| _DyeIntensity | Float (0.5-3) | 1.5 |

### 3. Add Custom Function Node
1. Add a Custom Function node
2. Set Type to "File"
3. Set Source to `DestinyDyeFunctions.hlsl`
4. Set Function Name to `DestinyMaterial_float`

### 4. Connect Inputs
```
BaseMap (Sample) → Albedo
DyeMaskMap (Sample) → DyeMask
NormalMap (Sample) → Normal (through Normal Unpack)
View Direction → ViewDir
_DyePrimary → DyePrimary
_DyeSecondary → DyeSecondary
_DyeTertiary → DyeTertiary
MaskMap.R → AO
MaskMap.G → Roughness
MaskMap.B → Metalness
_ClearCoatStrength → ClearCoatStrength
_FresnelStrength → FresnelStrength
_DyeIntensity → DyeIntensity
```

### 5. Connect Outputs
```
BaseColor → Fragment Base Color
FinalRoughness → Fragment Smoothness (invert for HDRP)
FinalMetalness → Fragment Metallic
FinalAO → Fragment Ambient Occlusion
```

### 6. For Clear Coat (HDRP)
In the Graph Inspector:
1. Enable "Clear Coat"
2. Connect ClearCoat output to Clear Coat Mask
3. Set Clear Coat Smoothness to 0.9

### 7. Save and Create Material
1. Save Shader Graph
2. Right-click → Create → Material
3. Assign the Destiny Dye Shader
4. Save as `DestinyDyeMaterial.mat` in `Assets/Materials/`

## Texture Setup

### Base Map (Albedo)
- sRGB: Yes
- Filter: Bilinear

### Normal Map
- Texture Type: Normal Map
- sRGB: No

### Mask Map (HDRP format)
- R: Metallic
- G: AO
- B: Detail Mask
- A: Smoothness
- sRGB: No

### Dye Mask Map
- R: Primary dye area
- G: Secondary dye area
- B: Tertiary dye area
- sRGB: No

## Quick Node Graph Reference

```
[Sample _BaseMap] ──────────────────────┐
[Sample _DyeMaskMap] ───────────────────┤
[Sample _NormalMap] → [Normal Unpack] ──┤
[View Direction] ───────────────────────┤
[_DyePrimary] ──────────────────────────┤
[_DyeSecondary] ────────────────────────┤──→ [Custom Function: DestinyMaterial]
[_DyeTertiary] ─────────────────────────┤         │
[Sample _MaskMap.R] (AO) ───────────────┤         ├──→ Base Color
[Sample _MaskMap.G] (Rough) ────────────┤         ├──→ Metallic
[Sample _MaskMap.B] (Metal) ────────────┤         ├──→ Smoothness
[_ClearCoatStrength] ───────────────────┤         ├──→ AO
[_FresnelStrength] ─────────────────────┤         ├──→ ClearCoat
[_DyeIntensity] ────────────────────────┘         └──→ Emission (Fresnel * White)
```
