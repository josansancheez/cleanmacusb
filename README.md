<div align="center">

<img src="assets/logo.png" alt="CleanMacUSB Logo" width="140" height="140" />

# CleanMacUSB

**Utilidad nativa para Windows que erradica los archivos ocultos y residuales de macOS y Windows en memorias USB y discos externos.**

[![Plataforma](https://img.shields.io/badge/Plataforma-Windows%2010%20%2F%2011%20(64--bit)-0284C7?style=for-the-badge&logo=windows)](https://microsoft.com)
[![Framework](https://img.shields.io/badge/.NET-8.0%20WPF%20(Fluent%20UI)-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com)
[![Instalador](https://img.shields.io/badge/Instalador-InstallShield%20Wizard-16A34A?style=for-the-badge)](#asistente-de-instalaci%C3%B3n-profesional-cleanmacusb_setup_v10exe)
[![Licencia](https://img.shields.io/badge/Licencia-Freeware%20Propietario-F59E0B?style=for-the-badge)](#licencia-y-autor%C3%ADa)
[![Autor](https://img.shields.io/badge/Desarrollado%20por-Josan%20S%C3%A1nchez-0F172A?style=for-the-badge)](#licencia-y-autor%C3%ADa)

*Disponible íntegramente en **Español (ES)** e **Inglés (EN)** con cambio de idioma instantáneo en tiempo real.*

</div>

---

## 🎯 ¿Por qué existe CleanMacUSB?

Cuando conectas un pendrive o disco externo (formateado en `exFAT`, `FAT32` o `NTFS`) en un ordenador **Mac** o **Windows**, el sistema operativo genera silenciosamente decenas de carpetas y archivos ocultos de indexación, papeleras y metadatos bifurcados.

Aunque en el ordenador a veces pasan desapercibidos, **estos archivos provocan problemas reales** cuando conectas el USB en:
- 📺 **Smart TVs y reproductores multimedia** (aparecen archivos duplicados fantasma como `._Pelicula.mp4` que dan error al reproducir).
- 🚗 **Equipos de sonido de coches y radios USB** (intentan leer archivos `._Cancion.mp3` o `Thumbs.db` y bloquean la reproducción de carpetas).
- 🎧 **Equipos DJ (Pioneer CDJ / XDJ / Rekordbox)**, marcos digitales, consolas e impresoras 3D.
- 💾 **Pérdida de espacio libre real:** La carpeta oculta `.Trashes` o `$RECYCLE.BIN` retiene el espacio de archivos que creías haber borrado.

**CleanMacUSB** analiza recursivamente toda la unidad en milisegundos, desbloquea atributos `Hidden`, `System` y `ReadOnly`, elimina toda la basura residual de raíz y permite expulsar el hardware USB de forma 100 % segura.

---

## 🧹 ¿Qué archivos y carpetas elimina?

CleanMacUSB organiza la limpieza en **8 categorías configurables** (para **macOS**, **Windows**, **Linux** y **Smart TVs**):

| Origen | Categoría | Elementos eliminados |
| :--- | :--- | :--- |
| 🍎 **macOS** | **Archivos AppleDouble** | `._*` (archivos fantasma duplicados por cada fichero), `.AppleDouble`, `.AppleDB`, `.AppleDesktop` |
| 🍎 **macOS** | **Configuración de carpetas e iconos** | `.DS_Store`, `.VolumeIcon.icns`, `.apdisk`, `.com.apple.timemachine.donotpresent`, `.metadata_never_index` |
| 🍎 **macOS** | **Spotlight, Papeleras y Eventos** | `.Spotlight-V100`, `.Trashes`, `.Trash-*`, `.fseventsd`, `.TemporaryItems`, `.DocumentRevisions-V100`, `.PKInstallSandboxManager*` |
| 🪟 **Windows** | **Cachés de miniaturas y vistas** | `Thumbs.db`, `ehthumbs.db`, `ehthumbs_vista.db`, `desktop.ini`, `WPSettings.dat`, `IndexerVolumeGuid` |
| 🪟 **Windows** | **Papelera y fragmentos CHKDSK** | `$RECYCLE.BIN`, `RECYCLER`, `RECYCLED`, carpetas `FOUND.000` / `FOUND.*` y fragmentos huérfanos `*.chk` |
| 🪟 **Windows** | **Residuales `README.`, bloqueos y temporales** | `README.`, `README`, `README.txt`, bloqueos ocultos de Office (`~$*`) y temporales (`*.tmp`, `*.bak`) |
| 🐧 **Linux** | **Papeleras, vistas KDE/GNOME y swaps** | `.Trash`, `.Trash-1000`, `.Trash-*`, `.directory` (Dolphin), `.hidden`, `.fuse_hidden*`, `.nfs*`, `.goutputstream-*`, `.*.swp`, `.*.swo` |
| 📺 **Smart TV** | **Carpetas y bases de datos de TV** | `LOST.DIR`, `LG Smart TV`, `.lg_tv`, `.android_secure`, `.Samsung`, `.DEV_INFO`, `.thumbnails`, `.database_uuid`, `albumart.pamp`, `mstpvr`, carpeta `Android` vacía |

---

## ✨ Características Principales

- ⚡ **Rendimiento Nativo (.NET 8 WPF + Win32):** Interfaz moderna estilo Windows 11 (*Fluent UI*) sin dependencias pesadas.
- 🌐 **Multi-idioma en Tiempo Real (`ES` / `EN`):** Detecta el idioma del sistema operativo al iniciarse e incluye un selector desplegable (`🇪🇸 Español` / `🇬🇧 English`) que traduce toda la interfaz, logs, ajustes y menús al instante sin reiniciar.
- 💾 **Limpieza de USB, Discos Duros Internos y Carpetas Específicas:**
  - Detecta automáticamente en caliente (`WM_DEVICECHANGE`) cualquier **memoria USB o disco externo** conectado.
  - Permite activar con un clic la visualización de los **discos duros internos del ordenador (`C:\`, `D:\`...)** (protegiendo automáticamente las carpetas críticas del sistema operativo).
  - Incluye el botón **«📁 Elegir carpeta...»** para seleccionar y limpiar cualquier carpeta concreta del equipo o de un disco.
- 📋 **Registro de Actividad (Log) en Tiempo Real + Botón de Abortar:**
  - Barra de progreso y listado detallado línea por línea de cada archivo y carpeta eliminada con su tamaño exacto.
  - Botón **«⏹ Abortar limpieza»** para detener el proceso en cualquier instante conservando el resumen parcial.
  - Exportación del informe completo a `.txt` (`CleanMacUSB_Log_YYYYMMDD_HHMM.txt`).
- ⏏ **Expulsión Segura de Hardware USB:** Desmonta el volumen (`FSCTL_LOCK_VOLUME` / `FSCTL_DISMOUNT_VOLUME`) y solicita la expulsión física al bus PnP de Windows mediante `setupapi.dll` y `cfgmgr32.dll` (`CM_Request_Device_EjectW`). Incluye opción de **auto-expulsión al terminar la limpieza**.
- ⚙ **Apartado Dedicado de Ajustes y Segundo Plano (*System Tray*):**
  - Interruptor para mantener **CleanMacUSB** activo junto al reloj de Windows al cerrar la ventana, mostrando una notificación interactiva automática cada vez que se introduce un pendrive nuevo.
  - Visor integrado de versión, créditos de autoría y Contrato de Licencia de Usuario Final (*EULA*).

---

## 📦 Asistente de Instalación Profesional (`CleanMacUSB_Setup_v1.0.exe`)

El proyecto incluye el instalador único autónomo **`CleanMacUSB_Setup_v1.0.exe`** con estética clásica de **InstallShield Wizard**, que guía al usuario paso a paso:

1. **Selector de idioma inicial** (*Español* / *English*).
2. **Verificación automática de prerrequisitos** y motor `.NET 8` integrado (funciona de forma 100 % autónoma sin necesitar descargas externas ni carpetas adicionales).
3. **Pantalla de bienvenida personalizada** de **Josan Sánchez**.
4. **Detección condicional de versiones previas:** Si detecta una instalación existente de CleanMacUSB en el registro, muestra una página dedicada para desinstalarla limpiamente antes de continuar; si no existe, salta la página automáticamente.
5. **Acuerdo de Licencia (EULA) con lectura obligatoria (`GetScrollInfo`):** El botón *«Siguiente >»* y la opción *«Acepto el acuerdo»* permanecen bloqueados hasta que el usuario desplaza la barra de lectura hasta el final del contrato.
6. **Selector de alcance e instalación en unidad `C:`:** Permite elegir entre *«Instalar para todos los usuarios del equipo»* (`C:\Program Files\CleanMacUSB`) o *«Instalar solo para el usuario actual»*, pudiendo examinar cualquier otra ruta.
7. **Integración completa en Windows:** Crea accesos directos en Menú Inicio y Escritorio, registra el desinstalador oficial con su icono Macintosh en *Panel de Control > Programas y características* / *Configuración > Aplicaciones*, y permite lanzar **CleanMacUSB** al finalizar.

---

## 🗂️ Estructura del Repositorio

```text
CleanMacUSB/
├── assets/
│   ├── CleanMacUSB.ico          # Icono multi-resolución (16, 32, 48, 64, 256 px) inspirado en Macintosh 1984
│   ├── logo.png                 # Logotipo principal en alta resolución (512x512 px)
│   ├── logo.svg                 # Diseño vectorial original del logotipo
│   ├── EULA_ES.txt              # Contrato de Licencia de Usuario Final en Español
│   └── EULA_EN.txt              # End-User License Agreement in English
├── src/CleanMacUSB/
│   ├── Models/
│   │   ├── CleanLogEntry.cs     # Modelo de entrada del registro de actividad en tiempo real
│   │   └── UsbDriveItem.cs      # Modelo de unidad USB, disco interno o carpeta
│   ├── Services/
│   │   ├── LocalizationService.cs    # Diccionario reactivo multi-idioma (es-ES / en-US)
│   │   ├── MacJunkCleanerService.cs  # Motor asíncrono de limpieza recursiva (macOS, Windows, Linux y Smart TV)
│   │   └── UsbDriveService.cs        # Interoperabilidad Win32 (setupapi.dll / cfgmgr32.dll)
│   ├── Views/
│   │   ├── AboutWindow.xaml          # Ventana modal de Acerca de y Licencia EULA
│   │   └── AboutWindow.xaml.cs
│   ├── App.xaml                      # Paleta y estilos Windows 11 Fluent UI
│   ├── App.xaml.cs
│   ├── CleanMacUSB.csproj            # Configuración del proyecto .NET 8 WPF (WinExe)
│   ├── MainWindow.xaml               # Interfaz principal, Panel de Log y Panel de Ajustes
│   └── MainWindow.xaml.cs
└── CleanMacUSB_Setup_v1.0.exe   # Instalador único autónomo estilo InstallShield Wizard
```

---

## 📜 Licencia y Autoría

- **Producto:** CleanMacUSB v1.0
- **Autor y Titular de los Derechos:** **Josan Sánchez**
- **Tipo de Licencia:** Software Gratuito y Propietario (*Freeware*).
  - Permitido su uso gratuito personal y profesional.
  - Queda prohibida su venta, comercialización, modificación o redistribución con fines lucrativos sin autorización expresa del autor. Consulta los textos completos en [`assets/EULA_ES.txt`](assets/EULA_ES.txt) y [`assets/EULA_EN.txt`](assets/EULA_EN.txt).

---

<div align="center">
  <sub>Desarrollado con ❤️ por <strong>Josan Sánchez</strong> — Copyright © 2026 Todos los derechos reservados.</sub>
</div>
