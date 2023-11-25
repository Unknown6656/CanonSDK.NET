using System;

using EDSDK.Native;

namespace EDSDK.NET;


/// <summary>
/// A container for camera related information
/// </summary>
public sealed class Camera
{
    /// <summary>
    /// Pointer to SDK camera object
    /// </summary>
    public nint Handle { get; }

    /// <summary>
    /// Information about this camera
    /// </summary>
    public EdsDeviceInfo Info { get; }

    /// <summary>
    /// Handles errors that happen with the SDK
    /// </summary>
    public EdsError Error
    {
     // get => EdsError.OK;
        set
        {
            if (value != EdsError.OK)
                throw new Exception("SDK Error: " + value);
        }
    }

    /// <summary>
    /// Creates a new instance of the Camera class
    /// </summary>
    /// <param name="handle">Pointer to the SDK camera object</param>
    public Camera(nint handle)
    {
        if (handle == 0)
            throw new ArgumentNullException(nameof(handle));

        Handle = handle;

        Error = EDSDK_API.EdsGetDeviceInfo(handle, out EdsDeviceInfo dinfo);
        Info = dinfo;
    }
}
