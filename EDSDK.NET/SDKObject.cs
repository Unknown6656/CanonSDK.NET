// #define STRICT_SETGET_4_BYTES_ONLY

using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Drawing;
using System.Linq;
using System.Text;
using System;

using EDSDK.Native;

namespace EDSDK.NET;


public readonly partial struct SDKProperty(uint id)
    : IEquatable<SDKProperty>
{
    private static readonly Dictionary<SDKProperty, string?> _names = [];
    private static readonly SDKProperty[] _settingslist_supported = [AEModeSelect, ISOSpeed, Av, Tv, MeteringMode, ExposureCompensation];

    private readonly uint _id = id;

    #region KNOWN PROPERTIES

    /*----------------------------------
     Camera Setting Properties
    ----------------------------------*/
    public static SDKProperty Unknown { get; } = new(0x0000ffff);
    public static SDKProperty ProductName { get; } = new(0x00000002);
    public static SDKProperty BodyIDEx { get; } = new(0x00000015);
    public static SDKProperty OwnerName { get; } = new(0x00000004);
    public static SDKProperty MakerName { get; } = new(0x00000005);
    public static SDKProperty DateTime { get; } = new(0x00000006);
    public static SDKProperty FirmwareVersion { get; } = new(0x00000007);
    public static SDKProperty BatteryLevel { get; } = new(0x00000008);
    public static SDKProperty CFn { get; } = new(0x00000009);
    public static SDKProperty SaveTo { get; } = new(0x0000000b);
    public static SDKProperty CurrentStorage { get; } = new(0x0000000c);
    public static SDKProperty CurrentFolder { get; } = new(0x0000000d);
    public static SDKProperty BatteryQuality { get; } = new(0x00000010);

    /*----------------------------------
     Image Properties
    ----------------------------------*/
    public static SDKProperty ImageQuality { get; } = new(0x00000100);
    public static SDKProperty Orientation { get; } = new(0x00000102);
    public static SDKProperty ICCProfile { get; } = new(0x00000103);
    public static SDKProperty FocusInfo { get; } = new(0x00000104);
    public static SDKProperty WhiteBalance { get; } = new(0x00000106);
    public static SDKProperty ColorTemperature { get; } = new(0x00000107);
    public static SDKProperty WhiteBalanceShift { get; } = new(0x00000108);
    public static SDKProperty ColorSpace { get; } = new(0x0000010d);
    public static SDKProperty PictureStyle { get; } = new(0x00000114);
    public static SDKProperty PictureStyleDesc { get; } = new(0x00000115);
    public static SDKProperty PictureStyleCaption { get; } = new(0x00000200);

    /*----------------------------------
     Capture Properties
    ----------------------------------*/
    public static SDKProperty AEMode { get; } = new(0x00000400);
    public static SDKProperty AEModeSelect { get; } = new(0x00000436);
    public static SDKProperty DriveMode { get; } = new(0x00000401);
    public static SDKProperty ISOSpeed { get; } = new(0x00000402);
    public static SDKProperty MeteringMode { get; } = new(0x00000403);
    public static SDKProperty AFMode { get; } = new(0x00000404);
    public static SDKProperty Av { get; } = new(0x00000405);
    public static SDKProperty Tv { get; } = new(0x00000406);
    public static SDKProperty ExposureCompensation { get; } = new(0x00000407);
    public static SDKProperty FocalLength { get; } = new(0x00000409);
    public static SDKProperty AvailableShots { get; } = new(0x0000040a);
    public static SDKProperty Bracket { get; } = new(0x0000040b);
    public static SDKProperty WhiteBalanceBracket { get; } = new(0x0000040c);
    public static SDKProperty LensName { get; } = new(0x0000040d);
    public static SDKProperty AEBracket { get; } = new(0x0000040e);
    public static SDKProperty FEBracket { get; } = new(0x0000040f);
    public static SDKProperty ISOBracket { get; } = new(0x00000410);
    public static SDKProperty NoiseReduction { get; } = new(0x00000411);
    public static SDKProperty FlashOn { get; } = new(0x00000412);
    public static SDKProperty RedEye { get; } = new(0x00000413);
    public static SDKProperty FlashMode { get; } = new(0x00000414);
    public static SDKProperty LensStatus { get; } = new(0x00000416);
    public static SDKProperty Artist { get; } = new(0x00000418);
    public static SDKProperty Copyright { get; } = new(0x00000419);

    /*----------------------------------
        EVF Properties
    ----------------------------------*/
    public static SDKProperty Evf_OutputDevice { get; } = new(0x00000500);
    public static SDKProperty Evf_Mode { get; } = new(0x00000501);
    public static SDKProperty Evf_WhiteBalance { get; } = new(0x00000502);
    public static SDKProperty Evf_ColorTemperature { get; } = new(0x00000503);
    public static SDKProperty Evf_DepthOfFieldPreview { get; } = new(0x00000504);

    // EVF IMAGE DATA Properties
    public static SDKProperty Evf_Zoom { get; } = new(0x00000507);
    public static SDKProperty Evf_ZoomPosition { get; } = new(0x00000508);
    public static SDKProperty Evf_ImagePosition { get; } = new(0x0000050B);
    public static SDKProperty Evf_HistogramStatus { get; } = new(0x0000050C);
    public static SDKProperty Evf_AFMode { get; } = new(0x0000050E);
    public static SDKProperty Evf_HistogramY { get; } = new(0x00000515);
    public static SDKProperty Evf_HistogramR { get; } = new(0x00000516);
    public static SDKProperty Evf_HistogramG { get; } = new(0x00000517);
    public static SDKProperty Evf_HistogramB { get; } = new(0x00000518);
    public static SDKProperty Evf_CoordinateSystem { get; } = new(0x00000540);
    public static SDKProperty Evf_ZoomRect { get; } = new(0x00000541);

    public static SDKProperty Record { get; } = new(0x00000510);

    /*----------------------------------
     Image GPS Properties
    ----------------------------------*/
    public static SDKProperty GPSVersionID { get; } = new(0x00000800);
    public static SDKProperty GPSLatitudeRef { get; } = new(0x00000801);
    public static SDKProperty GPSLatitude { get; } = new(0x00000802);
    public static SDKProperty GPSLongitudeRef { get; } = new(0x00000803);
    public static SDKProperty GPSLongitude { get; } = new(0x00000804);
    public static SDKProperty GPSAltitudeRef { get; } = new(0x00000805);
    public static SDKProperty GPSAltitude { get; } = new(0x00000806);
    public static SDKProperty GPSTimeStamp { get; } = new(0x00000807);
    public static SDKProperty GPSSatellites { get; } = new(0x00000808);
    public static SDKProperty GPSStatus { get; } = new(0x00000809);
    public static SDKProperty GPSMapDatum { get; } = new(0x00000812);
    public static SDKProperty GPSDateStamp { get; } = new(0x0000081D);

    /*----------------------------------
    DC Properties
    ----------------------------------*/
    public static SDKProperty DC_Zoom { get; } = new(0x00000600);
    public static SDKProperty DC_Strobe { get; } = new(0x00000601);
    public static SDKProperty LensBarrelStatus { get; } = new(0x00000605);
    public static SDKProperty TempStatus { get; } = new(0x01000415);
    public static SDKProperty Evf_RollingPitching { get; } = new(0x01000544);
    public static SDKProperty FixedMovie { get; } = new(0x01000422);
    public static SDKProperty MovieParam { get; } = new(0x01000423);
    public static SDKProperty Evf_ClickWBCoeffs { get; } = new(0x01000506);
    public static SDKProperty ManualWhiteBalanceData { get; } = new(0x01000204);
    public static SDKProperty MirrorUpSetting { get; } = new(0x01000438);
    public static SDKProperty MirrorLockUpState { get; } = new(0x01000421);
    public static SDKProperty UTCTime { get; } = new(0x01000016);
    public static SDKProperty TimeZone { get; } = new(0x01000017);
    public static SDKProperty SummerTimeSetting { get; } = new(0x01000018);
    public static SDKProperty AutoPowerOffSetting { get; } = new(0x0100045e);

#endregion

    public static SDKProperty[] SDKStateEvents { get; } = [.. Enum.GetValues<StateEvent>().Select(v => new SDKProperty((uint)v))];

    public static SDKProperty[] SDKObjectEvents { get; } = [.. Enum.GetValues<EdsEvent>().Select(v => new SDKProperty((uint)v))];

    public static SDKProperty[] SDKErrors { get; } = [.. Enum.GetValues<SDKError>().Select(v => new SDKProperty((uint)v))];

    public readonly string Name => (_names.TryGetValue(this, out string? name) ? name : null) ?? "[UNKNOWN]";


    static SDKProperty()
    {
        foreach (PropertyInfo prop in typeof(SDKProperty).GetProperties(BindingFlags.Public | BindingFlags.Static))
            if (prop.PropertyType == typeof(SDKProperty))
                _names[(SDKProperty)prop.GetValue(null)!] = prop.Name;

        foreach (SDKProperty prop in SDKStateEvents)
            _names[prop] = Enum.GetName((StateEvent)prop._id);

        foreach (SDKProperty prop in SDKObjectEvents)
            _names[prop] = Enum.GetName((EdsEvent)prop._id);

        foreach (SDKProperty prop in SDKErrors)
            _names[prop] = Enum.GetName((SDKError)prop._id);
    }

    public override readonly string ToString() => $"{Name} ({_id}, 0x{_id:x8})";

    public override readonly int GetHashCode() => (int)_id;

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is SDKProperty prop && Equals(prop);

    public readonly bool Equals(SDKProperty other) => _id == other._id;


    public readonly List<int> GetAsList(SDKObject @object)
    {
        if (_settingslist_supported.Contains(this))
        {
            @object.SDK.Error = EDSDK_API.GetPropertyDesc(@object, this, out EdsPropertyDesc des);

            return des.PropDesc.Take(des.NumElements).ToList();
        }
        else
            throw new InvalidOperationException("Settings lists are not supported for this property.");
    }

    public readonly uint Get(SDKObject @object) => @object.Get(this);

    public readonly T Get<T>(SDKObject @object) where T : unmanaged => @object.Get<T>(this);

    public readonly string GetAsString(SDKObject @object) => @object.GetAsString(this);

    public readonly T GetAsStruct<T>(SDKObject @object) where T : struct => @object.GetAsStruct<T>(this);

    public readonly unsafe void Set<T>(SDKObject @object, T value) where T : unmanaged => @object.Set(this, value);

    public readonly void Set(SDKObject @object, uint value) => @object.Set(this, value);

    public readonly void Set(SDKObject @object, DateTime value) => @object.Set(this, value);

    public readonly void Set(SDKObject @object, string value) => @object.Set(this, value);

    public readonly void SetAsStruct<T>(SDKObject @object, T value) where T : struct => @object.SetAsStruct(this, value);


    public static SDKProperty FromID(uint id) => Find(id, _names.Keys) ?? new(id);

    private static SDKProperty? Find(uint id, IEnumerable<SDKProperty> properties)
    {
        foreach (SDKProperty prop in properties)
            if (prop._id == id)
                return prop;

        return null;
    }

    public static SDKProperty? FromSDKError(SDKError error) => Find((uint)error, SDKErrors);

    public static SDKProperty? FromSDKObjectEvent(EdsEvent @event) => Find((uint)@event, SDKObjectEvents);

    public static SDKProperty? FromSDKStateEvent(StateEvent @event) => Find((uint)@event, SDKStateEvents);

    public static SDKProperty? FromName(string name)
    {
        //// be sure to make case sensitive comparison first before.
        //foreach ((SDKProperty prop, string? n) in _names)
        //    if (name.Equals(n))
        //        return prop;

        foreach ((SDKProperty prop, string? n) in _names)
            if (name.Equals(n, StringComparison.OrdinalIgnoreCase))
                return prop;

        return null;
    }

    // public static implicit operator SDKProperty(uint id) => new(id);

    public static bool operator ==(SDKProperty left, SDKProperty right) => left.Equals(right);

    public static bool operator !=(SDKProperty left, SDKProperty right) => !(left == right);
}

public interface ISDKObject<T>
    where T : class, ISDKObject<T>
{
    /// <summary>
    /// The internal SDK object handle.
    /// </summary>
    public nint Handle { get; }

    public SDKWrapper SDK { get; }


    public static abstract T? FromHandle(SDKWrapper sdk, nint handle);
}

public abstract class SDKObject
    : ISDKObject<SDKObject>
    , IEquatable<SDKObject>
{
    private static readonly ConcurrentDictionary<Type, bool> _validated = new();


    public nint Handle { get; }

    public SDKWrapper SDK { get; }

    public uint this[SDKProperty property]
    {
        get => Get(property);
        set => Set(property, value);
    }


    public SDKObject(SDKWrapper sdk, nint handle)
    {
        Type type = GetType();

        if (!_validated.ContainsKey(type))
            if (type.GetInterfaces().Any(iface => iface.IsGenericType
                                               && iface.GetGenericTypeDefinition() == typeof(ISDKObject<>)
                                               && iface.GetGenericArguments()[0] == type))
                _validated[type] = true;
            else
                throw new InvalidProgramException($"The type '{type}' is not a valid '{typeof(SDKObject)}' type. It must implement '{typeof(ISDKObject<>)}' and have '{type}' as its first generic argument.");

        if (handle == 0)
            throw new ArgumentNullException(nameof(handle));

        SDK = sdk;
        Handle = handle;
    }

    #region BASIC OBJECT FUNCTIONS (COMPARISON, REF. COUNTER, etc.)

    public T As<T>() where T : class, ISDKObject<T> => FromHandle<T>(SDK, Handle) ?? throw new InvalidOperationException($"The internal object handle must not be null or zero.");

    public void Retain() => SDK.Error = EDSDK_API.Retain(this);

    public void Release() => SDK.Error = EDSDK_API.Release(this);

    public override string ToString() => $"{GetType().Name}:0x{Handle:x8}";

    public override int GetHashCode() => (int)Handle;

    public override bool Equals(object? obj) => obj is SDKObject @object && Equals(@object);

    public bool Equals(SDKObject? other) => other is not null && Handle == other.Handle;

    #endregion
    #region OBJECT PROPERTIY FUNCTIONS

    //public List<int> GetAsList(SDKProperty property)
    //{
    //    if (_settingslist_supported.Contains(this))
    //    {
    //        @object.SDK.Error = EDSDK_API.GetPropertyDesc(@object, this, out EdsPropertyDesc des);
    //
    //        return des.PropDesc.Take(des.NumElements).ToList();
    //    }
    //    else
    //        throw new InvalidOperationException("Settings lists are not supported for this property.");
    //}

    public uint Get(SDKProperty property) => Get<uint>(property);

    public unsafe T? Get<T>(SDKProperty property)
#if STRICT_SETGET_4_BYTES_ONLY
        where T : struct
#endif
    {
#if STRICT_SETGET_4_BYTES_ONLY
        if (sizeof(T) != sizeof(uint))
            throw new ArgumentException($"The type {typeof(T)} must have a size of {sizeof(uint)} bytes - not {sizeof(T)} bytes.", nameof(T));
#else
        SDK.Error = EDSDK_API.GetPropertyData(this, property, out T? value);
        value ??= default;
#endif
        LogProperty(property, false, value);

        return value;
    }

    public string? GetAsString(SDKProperty property)
    {
        SDK.Error = EDSDK_API.GetPropertyData(this, property, out string? value);
        LogProperty(property, false, value);

        return value;
    }

    public T GetAsStruct<T>(SDKProperty property)
        where T : struct
    {
        //get type and size of struct
        Type structureType = typeof(T);
        int bufferSize = Marshal.SizeOf(structureType);
        nint ptr = Marshal.AllocHGlobal(bufferSize);

        SDK.Error = EDSDK_API.GetPropertyData(this, property, bufferSize, ptr);

        try
        {
            //convert pointer to managed structure
            T data = (T)Marshal.PtrToStructure(ptr, structureType);

            LogProperty(property, false, data);

            return data;
        }
        finally
        {
            if (ptr != 0)
            {
                //free the allocated memory
                Marshal.FreeHGlobal(ptr);
                ptr = 0;
            }
        }
    }

    public unsafe void Set<T>(SDKProperty property, T value)
#if STRICT_SETGET_4_BYTES_ONLY
        where T : struct
#endif
    {
        LogProperty(property, true, value);

#if STRICT_SETGET_4_BYTES_ONLY
        if (sizeof(T) != sizeof(uint))
            throw new ArgumentException($"The type {typeof(T)} must have a size of {sizeof(uint)} bytes - not {sizeof(T)} bytes.", nameof(T));

        Set(property, *(uint*)&value);
#else
        SDK.Error = EDSDK_API.SetPropertyData(this, property, sizeof(T), value);
#endif
    }

    public void Set(SDKProperty property, uint value)
    {
        LogProperty(property, true, value);

        SDK.SendSDKCommand(delegate
        {
            SDK.Error = EDSDK_API.GetPropertySize(this, property, out EdsDataType type, out int size);
            SDK.Error = EDSDK_API.SetPropertyData(this, property, size, value);
        }, sdk_action: nameof(EDSDK_API.SetPropertyData));
    }

    public void Set(SDKProperty property, DateTime value) =>
        SetAsStruct(property, new EdsTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Millisecond));

    public void Set(SDKProperty property, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value is not [.., '\0'])
            value += '\0';

        LogProperty(property, true, value);

        //convert string to byte array
        byte[] buffer = Encoding.ASCII.GetBytes(value);

        if (buffer.Length > 32)
            throw new ArgumentOutOfRangeException(nameof(value), "The provided value must be shorter than 32 bytes (including the zero-terminator).");

        SDK.SendSDKCommand(() => SDK.Error = EDSDK_API.SetPropertyData(this, property, 32, buffer), sdk_action: nameof(EDSDK_API.SetPropertyData));
    }

    public void SetAsStruct<T>(SDKProperty property, T value)
        where T : struct
    {
        LogProperty(property, true, value);

        SDK.SendSDKCommand(() => SDK.Error = EDSDK_API.SetPropertyData(this, property, Marshal.SizeOf(typeof(T)), value), sdk_action: nameof(EDSDK_API.SetPropertyData));
    }

    private void LogProperty(SDKProperty property, bool set, object? value)
    {
        string repr = value switch
        {
            null => "(null)",
            string s => $"\"{s}\" ({s.Length})",
            char c => $"'{c}' ({(int)c}, 0x{(int)c:x4})",
            byte or sbyte => $"0x{value:x2} ({value})",
            short or ushort => $"0x{value:x4} ({value})",
            int or uint => $"0x{value:x8} ({value})",
            nint or nuint or long or ulong => $"0x{value:x16} ({value})",
            DateTime or TimeSpan => $"{value:yyyy-MM-dd HH:mm:ss.ffffff}",
            IEnumerable collection => new Func<string>(delegate
            {
                string[] array = collection.Cast<object?>().Select(o => o?.ToString() ?? "(null)").ToArray();

                return $"{array.Length}: [{string.Join(", ", array)}]";
            })(),
            float or double or decimal or bool => value?.ToString() ?? "(null)",
            _ when value.ToString() is string tostr &&
                   value.GetType() is Type type &&
                   tostr != type.ToString() => $"{tostr} ({type.Name})",
            _ => value.ToString() ?? "(null)",
        };

        SDK.Logger.LogInfo($"{(set ? "SET" : "GET")} {this}.{property} {(set ? "<-" : "->")} {repr}");
    }

    #endregion

    public void Download(ulong size, SDKStream destination) => SDK.Error = EDSDK_API.Download(this, size, destination);

    public void DownloadComplete() => SDK.Error = EDSDK_API.DownloadComplete(this);

    public void DownloadCancel() => SDK.Error = EDSDK_API.DownloadCancel(this);

    public static T? FromHandle<T>(SDKWrapper sdk, nint handle) where T : class, ISDKObject<T> => T.FromHandle(sdk, handle);

    internal static SDKObject? FromHandle(SDKWrapper sdk, nint handle) => new __unspecified_SDK_object__(sdk, handle);

    static SDKObject? ISDKObject<SDKObject>.FromHandle(SDKWrapper sdk, nint handle) => FromHandle(sdk, handle);


    private sealed class __unspecified_SDK_object__(SDKWrapper sdk, nint handle) : SDKObject(sdk, handle);
}

/// <summary>
/// A container for camera related information
/// </summary>
public sealed class SDKCamera
    : SDKObject
    , ISDKObject<SDKCamera>
{
    /// <summary>
    /// Information about this camera
    /// </summary>
    public EdsDeviceInfo Info { get; }

    public EvfOutputDevice ViewfinderOutputDevice
    {
        set => Set(SDKProperty.Evf_OutputDevice, value);
        get => Get<EvfOutputDevice>(SDKProperty.Evf_OutputDevice);
    }

    public EdsSaveTo ImageSaveTarget
    {
        set => Set(SDKProperty.SaveTo, value);
        get => Get<EdsSaveTo>(SDKProperty.SaveTo);
    }

    public EdsStateEventHandler? StateEventHandler
    {
        set => SetStateEventHandler(StateEvent.All, value);
    }

    public EdsObjectEventHandler? ObjectEventHandler
    {
        set => SetObjectEventHandler(EdsEvent.All, value);
    }

    public EdsPropertyEventHandler? PropertyEventHandler
    {
        set => SetPropertyEventHandler(PropertyEvent.All, value);
    }


    /// <summary>
    /// Creates a new instance of the Camera class
    /// </summary>
    /// <param name="handle">Pointer to the SDK camera object</param>
    public SDKCamera(SDKWrapper sdk, nint handle)
        : base(sdk, handle)
    {
        sdk.Error = EDSDK_API.EdsGetDeviceInfo(handle, out EdsDeviceInfo info);
        Info = info;
    }

    public void SetStateEventHandler(StateEvent options, EdsStateEventHandler? callback)
    {
        SDK.Logger.LogInfo($"Setting state event handler for {this} to {callback?.Method?.ToString() ?? "(null)"}.");
        SDK.Error = EDSDK_API.SetCameraStateEventHandler(this, options, callback);
    }

    public void SetObjectEventHandler(EdsEvent options, EdsObjectEventHandler? callback)
    {
        SDK.Logger.LogInfo($"Setting object event handler for {this} to {callback?.Method?.ToString() ?? "(null)"}.");
        SDK.Error = EDSDK_API.SetObjectEventHandler(this, options, callback);
    }

    public void SetPropertyEventHandler(PropertyEvent options, EdsPropertyEventHandler? callback)
    {
        SDK.Logger.LogInfo($"Setting property event handler for {this} to {callback?.Method?.ToString() ?? "(null)"}.");
        SDK.Error = EDSDK_API.SetPropertyEventHandler(this, options, callback);
    }

    public void StartLiveView() => ViewfinderOutputDevice = EvfOutputDevice.PC;

    public void StopLiveView(bool LVoff) => ViewfinderOutputDevice = LVoff ? EvfOutputDevice.Off : EvfOutputDevice.TFT;

    public void SetTFTEvf() => ViewfinderOutputDevice = EvfOutputDevice.TFT;

    public void SetSaveToHost() => ImageSaveTarget = EdsSaveTo.Host;

    public void OpenSession() => SDK.Error = EDSDK_API.OpenSession(this);

    public void CloseSession() => SDK.Error = EDSDK_API.CloseSession(this);

    public void SendCommand(CameraCommand command, int param)
    {
        SDK.Logger.LogInfo($"Sending the command '{command}' to the camera with the param 0x{param:x8} ({param}).");
        SDK.Error = EDSDK_API.SendCommand(this, command, param);
    }

    public void SendStatusCommand(CameraState state, int param)
    {
        SDK.Logger.LogInfo($"Sending the status command '{state}' to the camera with the param 0x{param:x8} ({param}).");
        SDK.Error = EDSDK_API.SendStatusCommand(this, state, param);
    }

    public static new SDKCamera? FromHandle(SDKWrapper sdk, nint handle) => handle == 0 ? null : new(sdk, handle);
}

public abstract class SDKProgressableObject(SDKWrapper sdk, nint handle)
    : SDKObject(sdk, handle)
{
    public void SetProgressCallback(EdsProgressOption option, EdsProgressCallback callback, SDKObject? context) => SDK.Error = EDSDK_API.SetProgressCallback(this, option, callback, context);
}

public sealed class SDKImage(SDKWrapper sdk, nint handle)
    : SDKProgressableObject(sdk, handle)
    , ISDKObject<SDKImage>
{
    public static new SDKImage? FromHandle(SDKWrapper sdk, nint handle) => handle == 0 ? null : new(sdk, handle);

    public static SDKImage FromStream(SDKStream stream)
    {
        stream.SDK.Error = EDSDK_API.CreateImageRef(stream, out SDKImage image);

        return image;
    }
}

public sealed class SDKElectronicViewfinderImage(SDKWrapper sdk, nint handle)
    : SDKObject(sdk, handle)
    , ISDKObject<SDKElectronicViewfinderImage>
{
    public EdsRect ZoomPosition => GetAsStruct<EdsRect>(SDKProperty.Evf_ZoomPosition);
    // EDSDK_API.GetPropertyData(this, SDKProperty.Evf_ZoomPosition, out EdsRect rect);

    public EdsSize EVFCoordinateSystem => GetAsStruct<EdsSize>(SDKProperty.Evf_CoordinateSystem);

    // EDSDK_API.GetPropertyData(this, SDKProperty.Evf_CoordinateSystem, out EdsSize size);


    public EdsPoint GetEVFPoint(SDKProperty property) => GetAsStruct<EdsPoint>(property);

    public static new SDKElectronicViewfinderImage? FromHandle(SDKWrapper sdk, nint handle) => handle == 0 ? null : new(sdk, handle);
}

public unsafe sealed class SDKStream(SDKWrapper sdk, nint handle)
    : SDKProgressableObject(sdk, handle)
    , ISDKObject<SDKStream>
{
    public nint Pointer
    {
        get
        {
            SDK.Error = EDSDK_API.GetPointer(this, out nint pointer);

            return pointer;
        }
    }

    public ulong Length
    {
        get
        {
            SDK.Error = EDSDK_API.GetLength(this, out ulong length);

            return length;
        }
    }

    public ulong Position
    {
        get
        {
            SDK.Error = EDSDK_API.GetPosition(this, out ulong position);

            return position;
        }
    }


    public void Seek(EdsSeekOrigin origin) => Seek(0, origin);

    public void Seek(long offset, EdsSeekOrigin origin) => SDK.Error = EDSDK_API.Seek(this, offset, origin);

    public SDKStream Duplicate(ulong size)
    {
        SDK.Error = EDSDK_API.CreateMemoryStream(SDK, size, out SDKStream stream);

        CopyTo(size, stream);

        return stream;
    }

    public void CopyTo(ulong size, SDKStream stream) => SDK.Error = EDSDK_API.CopyData(this, size, stream);

    public ulong Read(ulong size, nint buffer)
    {
        SDK.Error = EDSDK_API.Read(this, size, buffer, out ulong count);

        return count;
    }

    public byte[] Read(ulong size)
    {
        byte[] bytes = new byte[size];
        ulong count;

        fixed (byte* ptr = bytes)
            count = Read(size, (nint)(void*)ptr);

        if (count != size)
            Array.Resize(ref bytes, (int)count);

        return bytes;
    }

    public uint Write(ulong size, nint buffer)
    {
        SDK.Error = EDSDK_API.Write(this, size, buffer, out uint count);

        return count;
    }

    public uint Write(byte[] bytes)
    {
        fixed (byte* ptr = bytes)
            return Write((ulong)bytes.LongLength, (nint)(void*)ptr);
    }


    public static SDKStream CreateFileStream(SDKWrapper sdk, string filename, EdsFileCreateDisposition disposition, EdsAccess access)
    {
        sdk.Error = filename.Any(c => c <= 0xff)
                  ? EDSDK_API.CreateFileStreamA(sdk, filename, disposition, access, out SDKStream stream)
                  : EDSDK_API.CreateFileStreamW(sdk, filename, disposition, access, out stream);

        return stream;
    }

    public static SDKStream CreateMemoryStream(SDKWrapper sdk, ulong size)
    {
        sdk.Error = EDSDK_API.CreateMemoryStream(sdk, size, out SDKStream stream);

        return stream;
    }

    public static SDKStream CreateMemoryStream(SDKWrapper sdk, nint buffer, ulong size)
    {
        sdk.Error = EDSDK_API.CreateMemoryStreamFromPointer(sdk, buffer, size, out SDKStream stream);

        return stream;
    }

    public static new SDKStream? FromHandle(SDKWrapper sdk, nint handle) => handle == 0 ? null : new(sdk, handle);
}

public class SDKList(SDKWrapper sdk, nint handle)
    : SDKObject(sdk, handle)
    , ISDKObject<SDKList>
    , IEnumerable<SDKObject?>
{
    public SDKList? Parent
    {
        get
        {
            SDK.Error = EDSDK_API.GetParent(this, out SDKList? parent);

            return parent;
        }
    }

    public int Count
    {
        get
        {
            SDK.Error = EDSDK_API.GetChildCount(this, out int count);

            return count;
        }
    }

    public SDKObject? this[int index]
    {
        get
        {
            SDK.Error = EDSDK_API.GetChildAtIndex(this, index, out SDKObject? child);

            return child;
        }
    }


    public new SDKList<T> As<T>() where T : class, ISDKObject<T> => base.As<SDKList<T>>();

    public T? GetItem<T>(int index) where T : class, ISDKObject<T> => this[index]?.As<T>();

    public IEnumerator<SDKObject?> GetEnumerator()
    {
        for (int i = 0; i < Count; ++i)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static new SDKList? FromHandle(SDKWrapper sdk, nint handle) => handle == 0 ? null : new(sdk, handle);

    public static SDKList<SDKCamera> GetConnectedCameras(SDKWrapper sdk)
    {
        sdk.Error = EDSDK_API.GetCameraList(sdk, out SDKList<SDKCamera> list);

        return list;
    }
}

public class SDKList<T>(SDKWrapper sdk, nint handle)
    : SDKList(sdk, handle)
    , ISDKObject<SDKList<T>>
    , IEnumerable<T?>
    where T : class, ISDKObject<T>
{
    public new SDKList<SDKList<T>>? Parent => base.Parent?.As<SDKList<T>>();

    public new T? this[int index] => GetItem<T>(index);


    public T? GetItem(int index) => GetItem<T>(index);

    public new IEnumerator<T?> GetEnumerator()
    {
        for (int i = 0; i < Count; ++i)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static new SDKList<T>? FromHandle(SDKWrapper sdk, nint handle) => handle == 0 ? null : new(sdk, handle);
}









//public abstract class CameraFilesystemEntry(SDKWrapper sdk, nint handle);

//public abstract class CameraFilesystemFile(SDKWrapper sdk, nint handle) : CameraFilesystemEntry(sdk, handle);

//public abstract class CameraFilesystemFolder(SDKWrapper sdk, nint handle) : CameraFilesystemEntry(sdk, handle);

//public abstract class CameraFilesystemVolume(SDKWrapper sdk, nint handle) : CameraFilesystemEntry(sdk, handle);



public class CameraFileEntry(SDKWrapper sdk, nint handle, CameraFileEntryTypes type, string name)
    : SDKList<CameraFileEntry>(sdk, handle)
    , ISDKObject<CameraFileEntry>
{
    

    public string Name { get; } = name;

    public CameraFileEntryTypes Type { get; } = type;

    public EdsVolumeInfo Volume { get; set; }

    public Bitmap? Thumbnail { get; set; }

    public CameraFileEntry[] SubEntries { get; private set; } = [];


    public void AddSubEntries(IEnumerable<CameraFileEntry> entries) => SubEntries = [.. SubEntries, .. entries];

    static CameraFileEntry? ISDKObject<CameraFileEntry>.FromHandle(SDKWrapper sdk, nint handle) => new(sdk, handle, CameraFileEntryTypes.File, $"(unknown 0x{handle:x8})");
}



public enum CameraFileEntryTypes
{
    Camera = 5,
    Volume = 10,
    Folder = 20,
    File = 30,
}
