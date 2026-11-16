using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using CleanMacUSB.Models;
using Microsoft.Win32.SafeHandles;

namespace CleanMacUSB.Services;

/// <summary>
/// Servicio nativo Win32 para detección de memorias USB / discos externos USB
/// y expulsión segura mediante setupapi.dll, cfgmgr32.dll (CM_Request_Device_EjectW) y DeviceIoControl.
/// </summary>
public static class UsbDriveService
{
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;

    private const uint FSCTL_LOCK_VOLUME = 0x00090018;
    private const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
    private const uint IOCTL_STORAGE_MEDIA_REMOVAL = 0x002D4804;
    private const uint IOCTL_STORAGE_EJECT_MEDIA = 0x002D4808;
    private const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080;
    private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;

    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DIGCF_DEVICEINTERFACE = 0x00000010;
    private const int CR_SUCCESS = 0x00000000;

    private static readonly Guid GUID_DEVINTERFACE_DISK = new("53f56307-b6bf-11d0-94f2-00a0c91efb8b");

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_DEVICE_NUMBER
    {
        public int DeviceType;
        public int DeviceNumber;
        public int PartitionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_PROPERTY_QUERY
    {
        public int PropertyId;
        public int QueryType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_DESCRIPTOR_HEADER
    {
        public uint Version;
        public uint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PREVENT_MEDIA_REMOVAL
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool PreventMediaRemoval;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public uint cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushFileBuffers(SafeFileHandle hFile);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid ClassGuid,
        IntPtr Enumerator,
        IntPtr hwndParent,
        uint Flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr DeviceInfoSet,
        IntPtr DeviceInfoData,
        ref Guid InterfaceClassGuid,
        uint MemberIndex,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr DeviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
        IntPtr DeviceInterfaceDetailData,
        uint DeviceInterfaceDetailDataSize,
        out uint RequiredSize,
        ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    [DllImport("cfgmgr32.dll", SetLastError = true)]
    private static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

    [DllImport("cfgmgr32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int CM_Request_Device_EjectW(
        uint dnDevInst,
        out int pVetoType,
        StringBuilder pszVetoName,
        int ulNameLength,
        uint ulFlags);

    /// <summary>
    /// Obtiene todas las unidades extraíbles (Removable) y discos externos conectados por bus USB.
    /// </summary>
    public static List<UsbDriveItem> GetConnectedUsbDrives()
    {
        var list = new List<UsbDriveItem>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;

                // No incluir nunca la unidad del sistema operativo
                var sysRoot = Path.GetPathRoot(Environment.SystemDirectory);
                if (string.Equals(drive.Name, sysRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isUsbOrRemovable = drive.DriveType == DriveType.Removable ||
                                        (drive.DriveType == DriveType.Fixed && IsDriveOnUsbBus(drive.Name[0]));

                if (!isUsbOrRemovable) continue;

                list.Add(new UsbDriveItem
                {
                    DriveRoot = drive.Name,
                    DriveLetter = char.ToUpperInvariant(drive.Name[0]),
                    VolumeLabel = SafeGetVolumeLabel(drive),
                    FileSystem = SafeGetDriveFormat(drive),
                    TotalBytes = drive.TotalSize,
                    FreeBytes = drive.AvailableFreeSpace
                });
            }
            catch
            {
                // Ignorar unidades inaccesibles o desconectadas durante la consulta
            }
        }

        return list;
    }

    private static string SafeGetVolumeLabel(DriveInfo drive)
    {
        try { return drive.VolumeLabel; }
        catch { return string.Empty; }
    }

    private static string SafeGetDriveFormat(DriveInfo drive)
    {
        try { return drive.DriveFormat; }
        catch { return "USB"; }
    }

    /// <summary>
    /// Consulta mediante IOCTL_STORAGE_QUERY_PROPERTY si una unidad pertenece al bus USB (BusTypeUsb = 7).
    /// </summary>
    private static bool IsDriveOnUsbBus(char driveLetter)
    {
        try
        {
            string volumePath = $@"\\.\{driveLetter}:";
            using SafeFileHandle handle = CreateFile(
                volumePath,
                0,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid) return false;

            var query = new STORAGE_PROPERTY_QUERY
            {
                PropertyId = 0, // StorageDeviceProperty
                QueryType = 0,  // PropertyStandardQuery
                AdditionalParameters = new byte[1]
            };

            int querySize = Marshal.SizeOf<STORAGE_PROPERTY_QUERY>();
            IntPtr inBuffer = Marshal.AllocHGlobal(querySize);
            IntPtr outBuffer = Marshal.AllocHGlobal(1024);

            try
            {
                Marshal.StructureToPtr(query, inBuffer, false);
                if (DeviceIoControl(handle, IOCTL_STORAGE_QUERY_PROPERTY, inBuffer, (uint)querySize, outBuffer, 1024, out uint returned, IntPtr.Zero) &&
                    returned >= 32)
                {
                    // En STORAGE_DEVICE_DESCRIPTOR, el campo BusType (byte/enum) se encuentra en el offset 28
                    byte busType = Marshal.ReadByte(outBuffer, 28);
                    return busType == 7; // BusTypeUsb = 0x07
                }
            }
            finally
            {
                Marshal.FreeHGlobal(inBuffer);
                Marshal.FreeHGlobal(outBuffer);
            }
        }
        catch
        {
            // Ignorar errores de acceso
        }

        return false;
    }

    /// <summary>
    /// Expulsa de forma segura la unidad USB indicada combinando:
    /// 1) CM_Request_Device_EjectW sobre el nodo de dispositivo USB padre (setupapi.dll + cfgmgr32.dll)
    /// 2) Bloqueo, desmontaje y expulsión de volumen (FSCTL_LOCK_VOLUME + FSCTL_DISMOUNT_VOLUME + IOCTL_STORAGE_EJECT_MEDIA).
    /// </summary>
    public static bool SafelyEjectDrive(char driveLetter, out string errorDetail)
    {
        errorDetail = string.Empty;
        driveLetter = char.ToUpperInvariant(driveLetter);

        try
        {
            int deviceNumber = GetStorageDeviceNumber(driveLetter);

            // 1. Intentar vaciar buffers, bloquear y desmontar el volumen
            bool volumeEjected = TryDismountAndEjectVolume(driveLetter, out string volError);

            // 2. Solicitar expulsión física del nodo USB al Administrador de Configuración PnP
            if (deviceNumber >= 0)
            {
                uint devInst = FindDiskDevInstByDeviceNumber(deviceNumber);
                if (devInst != 0)
                {
                    int cr = CM_Get_Parent(out uint parentDevInst, devInst, 0);
                    uint targetInst = (cr == CR_SUCCESS && parentDevInst != 0) ? parentDevInst : devInst;

                    var vetoName = new StringBuilder(512);
                    int ejectRes = CM_Request_Device_EjectW(targetInst, out int vetoType, vetoName, vetoName.Capacity, 0);
                    if (ejectRes == CR_SUCCESS)
                    {
                        return true;
                    }

                    // Intentar directamente en devInst si el padre falló
                    ejectRes = CM_Request_Device_EjectW(devInst, out vetoType, vetoName, vetoName.Capacity, 0);
                    if (ejectRes == CR_SUCCESS)
                    {
                        return true;
                    }

                    if (!volumeEjected)
                    {
                        errorDetail = vetoName.Length > 0
                            ? $"PnP Veto ({vetoType}): {vetoName}"
                            : $"CM_Request_Device_Eject code 0x{ejectRes:X}";
                    }
                }
            }

            if (volumeEjected)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(errorDetail))
            {
                errorDetail = string.IsNullOrWhiteSpace(volError) ? "Device busy / Dispositivo en uso" : volError;
            }

            return false;
        }
        catch (Exception ex)
        {
            errorDetail = ex.Message;
            return false;
        }
    }

    private static int GetStorageDeviceNumber(char driveLetter)
    {
        string volumePath = $@"\\.\{driveLetter}:";
        using SafeFileHandle handle = CreateFile(
            volumePath,
            0,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid) return -1;

        int size = Marshal.SizeOf<STORAGE_DEVICE_NUMBER>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (DeviceIoControl(handle, IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, buffer, (uint)size, out _, IntPtr.Zero))
            {
                var sdn = Marshal.PtrToStructure<STORAGE_DEVICE_NUMBER>(buffer);
                return sdn.DeviceNumber;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return -1;
    }

    private static uint FindDiskDevInstByDeviceNumber(int targetDeviceNumber)
    {
        Guid diskGuid = GUID_DEVINTERFACE_DISK;
        IntPtr hDevInfo = SetupDiGetClassDevs(ref diskGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (hDevInfo == IntPtr.Zero || hDevInfo == new IntPtr(-1))
            return 0;

        try
        {
            uint index = 0;
            var interfaceData = new SP_DEVICE_INTERFACE_DATA
            {
                cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>()
            };

            while (SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref diskGuid, index, ref interfaceData))
            {
                index++;

                var devInfoData = new SP_DEVINFO_DATA
                {
                    cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
                };

                SetupDiGetDeviceInterfaceDetail(hDevInfo, ref interfaceData, IntPtr.Zero, 0, out uint requiredSize, ref devInfoData);
                if (requiredSize == 0) continue;

                IntPtr detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    // cbSize para SP_DEVICE_INTERFACE_DETAIL_DATA en 64-bit es 8, en 32-bit es 6
                    Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);

                    if (SetupDiGetDeviceInterfaceDetail(hDevInfo, ref interfaceData, detailBuffer, requiredSize, out _, ref devInfoData))
                    {
                        IntPtr pDevicePath = IntPtr.Add(detailBuffer, 4);
                        string? devicePath = Marshal.PtrToStringAuto(pDevicePath);
                        if (!string.IsNullOrEmpty(devicePath))
                        {
                            using SafeFileHandle hDisk = CreateFile(
                                devicePath,
                                0,
                                FILE_SHARE_READ | FILE_SHARE_WRITE,
                                IntPtr.Zero,
                                OPEN_EXISTING,
                                0,
                                IntPtr.Zero);

                            if (!hDisk.IsInvalid)
                            {
                                int sdnSize = Marshal.SizeOf<STORAGE_DEVICE_NUMBER>();
                                IntPtr sdnPtr = Marshal.AllocHGlobal(sdnSize);
                                try
                                {
                                    if (DeviceIoControl(hDisk, IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, sdnPtr, (uint)sdnSize, out _, IntPtr.Zero))
                                    {
                                        var sdn = Marshal.PtrToStructure<STORAGE_DEVICE_NUMBER>(sdnPtr);
                                        if (sdn.DeviceNumber == targetDeviceNumber)
                                        {
                                            return devInfoData.DevInst;
                                        }
                                    }
                                }
                                finally
                                {
                                    Marshal.FreeHGlobal(sdnPtr);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detailBuffer);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(hDevInfo);
        }

        return 0;
    }

    private static bool TryDismountAndEjectVolume(char driveLetter, out string error)
    {
        error = string.Empty;
        string volumePath = $@"\\.\{driveLetter}:";

        using SafeFileHandle handle = CreateFile(
            volumePath,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            int err = Marshal.GetLastWin32Error();
            error = $"Win32 Error {err}";
            return false;
        }

        FlushFileBuffers(handle);

        bool locked = false;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (DeviceIoControl(handle, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
            {
                locked = true;
                break;
            }
            Thread.Sleep(150);
        }

        if (!locked)
        {
            int err = Marshal.GetLastWin32Error();
            error = $"FSCTL_LOCK_VOLUME (Win32 {err})";
            return false;
        }

        if (!DeviceIoControl(handle, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
        {
            int err = Marshal.GetLastWin32Error();
            error = $"FSCTL_DISMOUNT_VOLUME (Win32 {err})";
            return false;
        }

        // Permitir extracción de medio
        var pmr = new PREVENT_MEDIA_REMOVAL { PreventMediaRemoval = false };
        int pmrSize = Marshal.SizeOf<PREVENT_MEDIA_REMOVAL>();
        IntPtr pmrPtr = Marshal.AllocHGlobal(pmrSize);
        try
        {
            Marshal.StructureToPtr(pmr, pmrPtr, false);
            DeviceIoControl(handle, IOCTL_STORAGE_MEDIA_REMOVAL, pmrPtr, (uint)pmrSize, IntPtr.Zero, 0, out _, IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeHGlobal(pmrPtr);
        }

        // Expulsar medio
        DeviceIoControl(handle, IOCTL_STORAGE_EJECT_MEDIA, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
        return true;
    }
}
