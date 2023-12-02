using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Reflection;
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

    /// <summary>
    /// Gets the list of possible values for the current camera to set.
    /// Only the PropertyIDs "AEModeSelect", "ISO", "Av", "Tv", "MeteringMode" and "ExposureCompensation" are allowed.
    /// </summary>
    /// <returns>A list of available values for the given property ID</returns>
    public readonly List<int> GetAsList(SDKWrapper wrapper)
    {
        if (_settingslist_supported.Contains(this))
        {
            wrapper.Error = EDSDK_API.GetPropertyDesc(wrapper.MainCamera, this, out EdsPropertyDesc des);

            return des.PropDesc.Take(des.NumElements).ToList();
        }
        else
            throw new InvalidOperationException("Settings lists are not supported for this property.");
    }

    /// <summary>
    /// Gets the current setting of given property ID as an uint
    /// </summary>
    /// <returns>The current setting of the camera</returns>
    public readonly uint Get(SDKWrapper wrapper) => Get<uint>(wrapper);

    /// <summary>
    /// Gets the current setting of given property ID as an uint
    /// </summary>
    /// <returns>The current setting of the camera</returns>
    public readonly unsafe T Get<T>(SDKWrapper wrapper)
        where T : unmanaged
    {
        if (sizeof(T) != sizeof(uint))
            throw new ArgumentException($"The type {typeof(T)} must have a size of {sizeof(uint)} bytes - not {sizeof(T)} bytes.", nameof(T));

        wrapper.Error = EDSDK_API.GetPropertyData(wrapper.MainCamera, this, out uint value);

        return *(T*)&value;
    }

    /// <summary>
    /// Gets the current setting of given property ID as a string
    /// </summary>
    /// <param name="property">The property ID</param>
    /// <returns>The current setting of the camera</returns>
    public readonly string GetAsString(SDKWrapper wrapper)
    {
        wrapper.Error = EDSDK_API.GetPropertyData(wrapper.MainCamera, this, out string value);

        return value;
    }

    /// <summary>
    /// Gets the current setting of given property ID as a struct
    /// </summary>
    /// <param name="property">The property ID</param>
    /// <typeparam name="T">One of the EDSDK structs</typeparam>
    /// <returns>The current setting of the camera</returns>
    public readonly T GetAsStruct<T>(SDKWrapper wrapper)
        where T : struct
    {
        //get type and size of struct
        Type structureType = typeof(T);
        int bufferSize = Marshal.SizeOf(structureType);

        //allocate memory
        nint ptr = Marshal.AllocHGlobal(bufferSize);
        //retrieve value
        wrapper.Error = EDSDK_API.GetPropertyData(wrapper.MainCamera, this, bufferSize, ptr);

        try
        {
            //convert pointer to managed structure
            T data = (T)Marshal.PtrToStructure(ptr, structureType);
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

    /// <summary>
    /// Sets an uint value for the given property ID
    /// </summary>
    /// <param name="property">The property ID</param>
    /// <param name="value">The value which will be set</param>
    public readonly unsafe void Set<T>(SDKWrapper wrapper, T value)
        where T : unmanaged
    {
        if (sizeof(T) != sizeof(uint))
            throw new ArgumentException($"The type {typeof(T)} must have a size of {sizeof(uint)} bytes - not {sizeof(T)} bytes.", nameof(T));

        Set<uint>(wrapper, *(uint*)&value);
    }

    /// <summary>
    /// Sets an uint value for the given property ID
    /// </summary>
    /// <param name="property">The property ID</param>
    /// <param name="value">The value which will be set</param>
    public readonly void Set(SDKWrapper wrapper, uint value)
    {
        LogSetProperty(wrapper, value);

        wrapper.SendSDKCommand(delegate
        {
            Error = EDSDK_API.GetPropertySize(wrapper.MainCamera, this, out EdsDataType type, out int size);
            Error = EDSDK_API.SetPropertyData(wrapper.MainCamera, this, size, value);
        }, sdk_action: nameof(EDSDK_API.SetPropertyData));
    }

    /// <summary>
    /// Sets a DateTime value for the current property.
    /// </summary>
    /// <param name="Value">The value which will be set</param>
    public readonly void Set(SDKWrapper wrapper, DateTime value) =>
        SetAsStruct(wrapper, new EdsTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Millisecond));

    /// <summary>
    /// Sets a string value for the given property ID
    /// </summary>
    /// <param name="value">The value which will be set</param>
    public readonly void Set(SDKWrapper wrapper, string value)
    {
        LogSetProperty(wrapper, value);

        if (value is null)
            throw new ArgumentNullException(nameof(value));

        //convert string to byte array
        byte[] propertyValueBytes = Encoding.ASCII.GetBytes(value + '\0');
        int propertySize = propertyValueBytes.Length;

        //check size of string
        if (propertySize > 32)
            throw new ArgumentOutOfRangeException("Value must be smaller than 32 bytes");

        //set value
        wrapper.SendSDKCommand(() => Error = EDSDK_API.SetPropertyData(MainCamera.Handle, property, 32, propertyValueBytes), sdk_action: nameof(EDSDK_API.SetPropertyData));
    }

    /// <summary>
    /// Sets a struct value for the given property ID
    /// </summary>
    /// <param name="value">The value which will be set</param>
    public readonly void SetAsStruct<T>(SDKWrapper wrapper, T value)
        where T : struct
    {
        LogSetProperty(wrapper, value);

        wrapper.SendSDKCommand(delegate
        {
            Error = EDSDK_API.SetPropertyData(wrapper.MainCamera, this, Marshal.SizeOf(typeof(T)), value);
        }, sdk_action: nameof(EDSDK_API.SetPropertyData));
    }

    private readonly void LogSetProperty(SDKWrapper wrapper, object? value)
    {
        string repr = value switch
        {
            string s => $"\"{s}\"",
            char c => $"'{c}' ({(int)c}, 0x{(int)c:x4})",
            byte or sbyte => $"0x{value:x2} ({value})",
            short or ushort => $"0x{value:x4} ({value})",
            int or uint => $"0x{value:x8} ({value})",
            nint or nuint or long or ulong => $"0x{value:x16} ({value})",
            IEnumerable collection => new Func<string>(delegate
            {
                string[] array = collection.Cast<object?>().Select(o => o?.ToString() ?? "(null)").ToArray();

                return $"{array.Length}: [{string.Join(", ", array)}]";
            })(),
            float or double or decimal or bool or null or _ => value?.ToString() ?? "(null)",
        };

        wrapper.LogInfo($"Setting property: {this} ==> {repr}");
    }

    //public readonly void LogPropertyValue(SDKWrapper wrapper, uint value) => LogInfo($"Camera_SDKPropertyEvent. Property {propertyName} changed to 0x{propertyValue:X}");

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

public abstract class SDKObject
{
    /// <summary>
    /// The internal SDK object handle.
    /// </summary>
    public nint Handle { get; }


    public SDKObject(nint handle)
    {
        if (handle == 0)
            throw new ArgumentNullException(nameof(handle));

        Handle = handle;
    }
}

/// <summary>
/// A container for camera related information
/// </summary>
public sealed class Camera
    : SDKObject
{
    /// <summary>
    /// Information about this camera
    /// </summary>
    public EdsDeviceInfo Info { get; }

    /// <summary>
    /// Handles errors that happen with the SDK
    /// </summary>
#pragma warning disable CA1822 // Mark members as static
    public SDKError Error
#pragma warning restore CA1822
    {
     // get => EdsError.OK;
        set
        {
            if (value != SDKError.OK)
                throw new Exception("SDK Error: " + value);
        }
    }


    /// <summary>
    /// Creates a new instance of the Camera class
    /// </summary>
    /// <param name="handle">Pointer to the SDK camera object</param>
    public Camera(nint handle)
        : base(handle)
    {
        Error = EDSDK_API.EdsGetDeviceInfo(handle, out EdsDeviceInfo dinfo);
        Info = dinfo;
    }
}
