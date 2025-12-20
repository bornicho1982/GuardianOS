# 🛡️ Composición de Armadura: Last Discipline Vest (Hash 2752429099)

Esta es la "verdad" de cómo está construida tu armadura, extraída directamente de la base de datos de tu sistema. Los programas externos (Charm/DCG) fallan porque Bungie cambió la estructura, ¡pero aquí tenemos los datos reales!

## 📐 Modelos 3D (Geometría)
La armadura usa archivos diferentes dependiendo de si tu personaje es hombre o mujer:

*   **👨 Versión Masculina**: `1b0cd59d2bb6186e741a1bf77fdbee94.tgxm.bin`
*   **👩 Versión Femenina**: `6a3c796f2a9ccd458b935cc6e39cec23.tgxm.bin`

---

## 🎨 Texturas y Capas
El juego junta estas 10 texturas para crear el aspecto final. Las más importantes son las últimas 3 (7, 8 y 9), que definen el modelo base:

| Índice | Hash de Textura | Función Sugerida |
| :--- | :--- | :--- |
| **7** | `c9d356747a3eea...` | **Albedo (Color Base)** |
| **8** | `abffe7eedcdc...` | **Normal Map (Relieve)** |
| **9** | `6a3c796f2a9c...` | **Stack Map (Metal/Brillo)** |

### 🎭 Capas de Tinte (Dye Masks)
Las texturas del **0 al 6** son máscaras de color. Son las que le dicen al juego: "Aquí va el color celeste del shader" o "Aquí va el cuero". Por eso, cuando ves la textura base, parece gris; el color real se "inyecta" usando estas capas.

---

## 🛠️ ¿Por qué fallaban las herramientas?
1.  **Charm**: Intenta leer toda la API de Bungie para mostrarte nombres bonitos, pero esa base de datos es tan grande que satura la memoria y cierra el programa.
2.  **DCG**: Es una herramienta antigua que busca una tabla llamada `DestinyGearAssetDefinition` dentro del archivo de 300MB. Bungie ha movido esa tabla a un archivo **separado** (el que yo acabo de consultar por ti).

> [!TIP]
> Ahora que tenemos los hashes reales, si alguna vez necesitas ver uno en específico, puedes buscarlos por ese código largo en la pestaña **TEXTURES** de Charm (que es más ligera) en lugar de usar la API lenta.
