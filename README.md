# CleanMacUSB v1.0

**Utilidad gratuita y propietaria (Freeware) para Windows desarrollada por Josan Sánchez.**

**CleanMacUSB** elimina de forma recursiva e instantánea todos los archivos y carpetas residuales ocultos que macOS genera en memorias USB y unidades externas (`._*`, `.DS_Store`, `.Spotlight-V100`, `.Trashes`, `.fseventsd`, `.TemporaryItems`, `.VolumeIcon.icns`, `.apdisk`, `.AppleDouble`, etc.) y permite desmontar y expulsar físicamente el dispositivo USB de forma segura mediante las APIs nativas de Windows (`setupapi.dll` / `cfgmgr32.dll` / `CM_Request_Device_EjectW`).

---

## Estructura del Proyecto

- `assets/`
  - `CleanMacUSB.ico`: Icono oficial multi-resolución (`16x16`, `32x32`, `48x48`, `64x64`, `256x256`) inspirado en el Macintosh clásico (1984) con pantalla azulada, escoba de limpieza y conector USB integrado.
  - `logo.png` y `logo.svg`: Logotipo oficial en alta resolución y formato vectorial.
  - `wizard_image.bmp` y `wizard_small.bmp`: Recursos visuales para el asistente de instalación Inno Setup estilo *InstallShield Wizard*.
- `tools/`
  - `Generate-Assets.ps1`: Generador por código (GDI+ de alta precisión) del icono multi-resolución `.ico` y recursos gráficos.
- `src/CleanMacUSB/`
  - `CleanMacUSB.csproj`: Proyecto nativo C# (.NET 8 - WPF con estética Windows 11 / Fluent UI).
  - `MainWindow.xaml` / `MainWindow.xaml.cs`: Interfaz principal, selector de idioma en tiempo real (`ES`/`EN`), detección en caliente de USB (`WM_DEVICECHANGE`), segundo plano en bandeja del sistema (*System Tray*) y panel de registro (Log) en tiempo real.
  - `Views/AboutWindow.xaml` / `Views/AboutWindow.xaml.cs`: Ventana modal *Acerca de / About* con créditos de autoría y visor del acuerdo de licencia EULA.
  - `Services/LocalizationService.cs`: Motor de internacionalización dinámico en tiempo real (`es-ES` y `en-US`) sin necesidad de reiniciar.
  - `Services/MacJunkCleanerService.cs`: Motor asíncrono de escaneo y eliminación recursiva con soporte para atributos `Hidden`, `System` y `ReadOnly`.
  - `Services/UsbDriveService.cs`: Interoperabilidad nativa Win32 (`CreateFile`, `DeviceIoControl`, `SetupDiGetClassDevs`, `CM_Request_Device_EjectW`) para detección y expulsión segura de hardware USB.
- `installer/`
  - `CleanMacUSB_Setup.iss`: Script de Inno Setup 6 con flujo completo estilo *InstallShield Wizard* (Selector de idioma, verificación de .NET 8 Desktop Runtime en el registro, bienvenida personalizada, detección condicional de versión previa con desinstalador integrado, lectura obligatoria hasta el final del EULA con bloqueo de scroll, selector de instalación para todos los usuarios o usuario actual, registro en Panel de Control y lanzamiento final).
  - `EULA_ES.txt` y `EULA_EN.txt`: Contratos de Licencia de Usuario Final (*Freeware*) en Español e Inglés.
- `build.ps1` / `build.bat`: Pipeline automatizado que genera los iconos, compila la aplicación en modo `Release` y empaqueta el instalador `dist\CleanMacUSB_Setup_v1.0.exe`.

---

## Compilación Rápida

Ejecuta desde PowerShell o haciendo doble clic en `build.bat`:

```powershell
.\build.ps1
```

El instalador final se genera automáticamente en:
`dist\CleanMacUSB_Setup_v1.0.exe`

---

© 2026 **Josan Sánchez**. Todos los derechos reservados.
