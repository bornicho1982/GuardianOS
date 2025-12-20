# Historial de Conversaciones - GuardianOS

## Índice de Sesiones

### Diciembre 2025

| Fecha | Tema | Estado |
|-------|------|--------|
| 2025-12-19/20 | Unity HDRP Viewer Migration | ✅ Completado |

---

## Sesión 2025-12-19/20: Unity HDRP

### Resumen
Migración del visor 3D de Three.js a Unity HDRP para mejor fidelidad visual.

### Logros
- ✅ Archivado visor Three.js en `_deprecated/`
- ✅ Creado proyecto Unity HDRP (`UnityViewer/`)
- ✅ Scripts: ViewerAPI, CharacterLoader, DyeController, CameraController
- ✅ Shader HLSL para sistema tintes Destiny 2
- ✅ Instalado Unity 6.3 LTS
- ✅ Configurado HDRP Asset y escena
- ✅ Build del visor creado
- ✅ Integración WPF (UnityViewerBridge.cs)
- ✅ Documentos técnicos guardados en `docs/work_sessions/`

### Pendiente
- [ ] Integrar Unity viewer en la aplicación WPF
- [ ] Crear Shader Graph completo en Unity
- [ ] Probar comunicación IPC Named Pipes
- [ ] Test visual con modelos reales

### Archivos clave
- `UnityViewer/` - Proyecto Unity completo
- `Services/UnityViewerBridge.cs` - Cliente IPC para WPF
- `docs/work_sessions/` - Documentación técnica

---

## Notas

### Ubicación conversaciones Antigravity
```
C:\Users\borni\.gemini\antigravity\conversations\
```
Formato: `.pb` (Protocol Buffer) - binario, no legible directamente.

---

*Última actualización: 2025-12-20 06:51*
