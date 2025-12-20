# Configuración de Escena Unity HDRP

Sigue estos pasos en Unity Editor para configurar la escena correctamente.

## 1. Abrir el Proyecto
1. Abre Unity Hub
2. Abre el proyecto `E:\GuardianOS\UnityViewer`
3. Espera a que cargue completamente

## 2. Abrir la Escena
- `File → Open Scene`
- Navega a `Assets/Scenes/ViewerScene.unity`

## 3. Añadir Iluminación HDRP

### Luz Principal (Sun)
1. `GameObject → Light → Directional Light`
2. Selecciona la luz en Hierarchy
3. En Inspector:
   - Rotation: `(50, -30, 0)`
   - Intensity: `100000` lux
   - Color Temperature: `6500` K

### Luz de Relleno
1. `GameObject → Light → Point Light`
2. En Inspector:
   - Position: `(2, 2, 2)`
   - Intensity: `10000` lumens

## 4. Añadir HDRI Sky
1. Selecciona `ViewerManager` en Hierarchy
2. `Component → Add → Volume (si no existe)`
3. Click en `New` para crear Volume Profile
4. `Add Override → Sky → HDRI Sky`
5. `Add Override → Tonemapping`

## 5. Añadir Objeto de Prueba
1. `GameObject → 3D Object → Cube`
2. Position: `(0, 0, 0)`
3. Scale: `(1, 1, 1)`
4. En el Material del cubo, pon un color visible (azul, rojo, etc.)

## 6. Verificar Cámara
1. Selecciona `Main Camera` en Hierarchy
2. Verifica que tenga el script `CameraController`
3. Position: `(0, 1, -3)`
4. Mira hacia el cubo (Rotation Y = 0)

## 7. Probar en Editor
1. Pulsa ▶️ Play en Unity
2. Deberías ver el cubo iluminado
3. Si ves negro, revisa las luces

## 8. Hacer Build
1. `File → Build Settings`
2. Asegúrate de que `ViewerScene` está en la lista
3. Platform: `Windows`
4. Architecture: `x86_64`
5. Click `Build`
6. Selecciona: `E:\GuardianOS\UnityViewer\Build`
7. Nombre: `UnityViewer`

## 9. Copiar Build a WPF
Ejecuta en PowerShell:
```powershell
Copy-Item -Path "E:\GuardianOS\UnityViewer\Build" -Destination "E:\GuardianOS\bin\Debug\net10.0-windows\UnityViewer\Build" -Recurse -Force
```

## 10. Probar
1. Ejecuta GuardianOS
2. Abre un personaje
3. Click en botón verde (Unity)
4. Debería aparecer el cubo 3D
