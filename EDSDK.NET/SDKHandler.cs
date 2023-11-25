using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System.Drawing.Imaging;
using System.Drawing;
using System.Linq;
using System.IO;
using System;

using EDSDK.Native;

using Microsoft.Extensions.Logging;

namespace EDSDK.NET;


public sealed record SDKProperty(string Name, uint Value, bool Matched = true)
{
    public string ValueToString() => $"0x{Value:X8}";
}

public sealed class SDKErrorEventArgs(string error, LogLevel level)
    : EventArgs
{
    public string Error { get; } = error;
    public LogLevel ErrorLevel { get; } = level;
}

/// <summary>
/// Handles the Canon SDK
/// </summary>
public sealed class SDKHandler
    : IDisposable
{
    private TaskCompletionSource<FileInfo> takePhotoCompletionSource;
    private string _imageSaveFilename;

    private readonly ILogger _logger;

    /// <summary>
    /// States if a finished video should be downloaded from the camera
    /// </summary>
    private bool DownloadVideo;

    /// <summary>
    /// For video recording, SaveTo has to be set to Camera. This is to store the previous setting until after the filming.
    /// </summary>
    private uint PrevSaveTo;

    private EdsCapacity PrevCapacity;

    private uint PrevEVFSetting;

    /// <summary>
    /// The thread on which the live view images will get downloaded continuously
    /// </summary>
    private Thread LVThread;

    /// <summary>
    /// If true, the live view will be shut off completely. If false, live view will go back to the camera.
    /// </summary>
    private bool LVoff;



    public event EventHandler<SDKErrorEventArgs> SdkError;
    public event EdsCameraAddedHandler SDKCameraAddedEvent;
    public event EdsObjectEventHandler SDKObjectEvent;
    public event EdsProgressCallback SDKProgressCallbackEvent;
    public event EdsPropertyEventHandler SDKPropertyEvent;
    public event EdsStateEventHandler SDKStateEvent;

    /// <summary>
    /// Fires if a camera is added
    /// </summary>
    public event Action CameraAdded;

    /// <summary>
    /// Fires if any process reports progress
    /// </summary>
    public event Action<int> ProgressChanged;

    /// <summary>
    /// Fires if the live view image has been updated
    /// </summary>
    public event Action<Stream> LiveViewUpdated;

    /// <summary>
    /// If the camera is disconnected or shuts down, this event is fired
    /// </summary>
    public event EventHandler CameraHasShutdown;

    /// <summary>
    /// If an image is downloaded, this event fires with the downloaded image.
    /// </summary>
    public event Action<Bitmap> ImageDownloaded;



    /// <summary>
    /// The used camera
    /// </summary>
    public Camera MainCamera { get; private set; }

    /// <summary>
    /// States if a session with the MainCamera is opened
    /// </summary>
    public bool CameraSessionOpen { get; private set; }

    /// <summary>
    /// States if the live view is on or not
    /// </summary>
    public bool IsLiveViewOn { get; private set; }

    /// <summary>
    /// States if camera is recording or not
    /// </summary>
    public bool IsFilming { get; private set; }

    /// <summary>
    /// Directory to where photos will be saved
    /// </summary>
    public string ImageSaveDirectory { get; set; }

    /// <summary>
    /// File name of next photo
    /// </summary>
    public string ImageSaveFilename
    {
        get => _imageSaveFilename;
        set
        {
            LogInfo($"Setting ImageSaveFilename. ImageSaveFilename: {value}");

            _imageSaveFilename = value;
        }
    }

    /// <summary>
    /// The focus and zoom border rectangle for live view (set after first use of live view)
    /// </summary>
    public EdsRect Evf_ZoomRect { get; private set; }

    /// <summary>
    /// The focus and zoom border position of the live view (set after first use of live view)
    /// </summary>
    public EdsPoint Evf_ZoomPosition { get; private set; }

    /// <summary>
    /// The cropping position of the enlarged live view image (set after first use of live view)
    /// </summary>
    public EdsPoint Evf_ImagePosition { get; private set; }

    /// <summary>
    /// The live view coordinate system (set after first use of live view)
    /// </summary>
    public EdsSize Evf_CoordinateSystem { get; private set; }

    public SDKProperty[] SDKObjectEvents { get; private set; }

    public SDKProperty[] SDKErrors { get; private set; }


    /// <summary>
    /// States if the Evf_CoordinateSystem is already set
    /// </summary>
    public bool IsCoordSystemSet = false;

    /// <summary>
    /// Handles errors that happen with the SDK
    /// </summary>
    public SDKError Error
    {
     // get => SDKError.OK;
        set
        {
            if (value != SDKError.OK)
            {
                SDKProperty errorProperty = SDKErrorToProperty(value);

                LogError($"SDK Error. Name: {errorProperty.Name}, Value: {errorProperty.ValueToString()}");

                if (value is SDKError.COMM_DISCONNECTED or SDKError.DEVICE_INVALID or SDKError.DEVICE_NOT_FOUND)
                {
                    string name = FindProperty(SDKErrors, value).Name;

                    OnSdkError(new SDKErrorEventArgs(name, LogLevel.Critical));
                }
            }
        }
    }



    public void SetUintSetting(string propertyName, string propertyValue)
    {
        bool error = false;

        if (!string.IsNullOrEmpty(propertyValue))
            propertyValue = propertyValue.Replace("0x", "");

        if (!uint.TryParse(propertyValue, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out uint value))
        {
            LogError($"Could not convert value {propertyValue} to uint");
            error = true;
        }

        if (!string.IsNullOrEmpty(propertyName) && propertyName.StartsWith("kEds"))
            propertyName = propertyName[4..];

        SDKProperty prop = GetSDKProperty(propertyName);

        if (!prop.Matched)
            LogWarning($"Could not find property named {propertyName}");

        if (!error)
            SetSetting(prop.Value, value);
    }

    public void DumpAllProperties()
    {
        LogInfo("=========Dumping properties=========");

        foreach (SDKProperty prop in SDKProperties)
            LogInfo($"Property: {prop.Name}, Value: 0x{GetSetting(prop.Value):X}");
    }

    #region Basic SDK and Session handling

    public SDKProperty SDKObjectEventToProperty(EdsEvent @event) => FindProperty(SDKObjectEvents, @event);

    public SDKProperty SDKErrorToProperty(SDKError error) => FindProperty(SDKErrors, error);

    private static SDKProperty FindProperty(SDKProperty[] properties, string property) => properties.FirstOrDefault(p => p.Name == property) ?? new(property, 0, false);

    private static SDKProperty FindProperty(SDKProperty[] properties, uint property) => properties.FirstOrDefault(p => p.Value == property) ?? new("[UNKNOWN]", property, false);

    public void SetSaveToHost() => SetSetting(EDSDK_API.PropID_SaveTo, (uint)EdsSaveTo.Host);

    public SDKProperty GetStateEvent(StateEvent stateEvent) => FindProperty(SDKStateEvents, stateEvent);

    public SDKProperty GetSDKProperty(string property) => FindProperty(SDKProperties, property);

    public SDKProperty GetSDKProperty(uint property) => FindProperty(SDKProperties, property);

    public SDKProperty[] SDKProperties { get; private set; }

    public SDKProperty[] SDKStateEvents { get; private set; }

    public object Value { get; private set; }

    public bool KeepAlive { get; set; }


    /// <summary>
    /// Initializes the SDK and adds events
    /// </summary>
    public SDKHandler(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SDKHandler>();

        STAThread.SetLogAction(loggerFactory.CreateLogger(nameof(STAThread)));
        STAThread.FatalError += STAThread_FatalError;

        PopulateSDKConstantStructures();

        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            LogWarning("SDKHandler created on a non-STA thread");
        }

        //initialize SDK

        try
        {
            Error = EDSDK_API.EdsInitializeSDK();
        }
        catch (Exception x)
        {
            _logger.LogError(x, "Error initialising SDK");

            throw new("Error initialising SDK", x);
            //TODO: Move to Initialise pattern instead of constructor
        }

        STAThread.Init();

        //subscribe to camera added event (the C# event and the SDK event)
        SDKCameraAddedEvent += new EdsCameraAddedHandler(SDKHandler_CameraAddedEvent);
        AddCameraHandler(() => EDSDK_API.EdsSetCameraAddedHandler(SDKCameraAddedEvent, 0), nameof(EDSDK_API.EdsSetCameraAddedHandler));

        //subscribe to the camera events (for the C# events)
        SDKStateEvent += new EdsStateEventHandler(Camera_SDKStateEvent);
        SDKPropertyEvent += new EdsPropertyEventHandler(Camera_SDKPropertyEvent);
        SDKProgressCallbackEvent += new EdsProgressCallback(Camera_SDKProgressCallbackEvent);
        SDKObjectEvent += new EdsObjectEventHandler(Camera_SDKObjectEvent);

    }

    private void STAThread_FatalError(object sender, EventArgs e)
    {
        OnSdkError(new("Execution thread error", LogLevel.Critical));
    }

    /// <summary>
    /// Call this once to initialize event listeners and thread management
    /// NOTE: Should be called from an STA thread
    /// </summary>
    private void PopulateSDKConstantStructures()
    {
#error todo fix this shite

        FieldInfo[] fields = typeof(EDSDK_API).GetFields(BindingFlags.Public | BindingFlags.Static);

        SDKStateEvents = FilterFields(fields, "StateEvent_");
        SDKObjectEvents = FilterFields(fields, "EdsEvent.");
        SDKErrors = FilterFields(fields, "EdsError.");
        SDKProperties = FilterFields(fields, "kEdsPropID_", "PropID_");
    }

    private static SDKProperty[] FilterFields(FieldInfo[] fields, string prefix, string prefix2 = null)
    {
        IEnumerable<SDKProperty> filteredFields = from f in fields
                                                  where (f.Name.StartsWith(prefix) || (prefix2 != null && f.Name.StartsWith(prefix2))) && f.IsLiteral
                                                  select new SDKProperty(f.Name, (uint)f.GetValue(null));
        return filteredFields.ToArray();
    }

    /// <summary>
    /// Get a list of all connected cameras
    /// </summary>
    /// <returns>The camera list</returns>
    public List<Camera> GetCameraList()
    {
        //get list of cameras
        Error = EDSDK_API.EdsGetCameraList(out nint camlist);

        //get each camera from camlist
        //get amount of connected cameras
        Error = EDSDK_API.EdsGetChildCount(camlist, out int c);
        List<Camera> camList = [];
        for (int i = 0; i < c; i++)
        {
            //get pointer to camera at index i
            Error = EDSDK_API.EdsGetChildAtIndex(camlist, i, out nint cptr);
            camList.Add(new Camera(cptr));
        }

        LogInfo($"Found {camList.Count} camera(s).");

        return camList;
    }

    /// <summary>
    /// Opens a session with given camera
    /// </summary>
    /// <param name="newCamera">The camera which will be used</param>
    public void OpenSession(Camera newCamera)
    {
        _logger?.LogDebug("Opening session");

        if (CameraSessionOpen)
        {
            CloseSession();
        }

        if (newCamera != null)
        {
            MainCamera = newCamera;
            //open a session
            SendSDKCommand(delegate { Error = EDSDK_API.EdsOpenSession(MainCamera.Handle); }, sdkAction: nameof(EDSDK_API.EdsOpenSession));
            //subscribe to the camera events (for the SDK)
            AddCameraHandler(() => EDSDK_API.EdsSetCameraStateEventHandler(MainCamera.Handle, StateEvent.All, SDKStateEvent, MainCamera.Handle), nameof(EDSDK_API.EdsSetCameraStateEventHandler));
            AddCameraHandler(() => EDSDK_API.EdsSetObjectEventHandler(MainCamera.Handle, EdsEvent.All, SDKObjectEvent, MainCamera.Handle), nameof(EDSDK_API.EdsSetObjectEventHandler));
            AddCameraHandler(() => EDSDK_API.EdsSetPropertyEventHandler(MainCamera.Handle, PropertyEvent.All, SDKPropertyEvent, MainCamera.Handle), nameof(EDSDK_API.EdsSetPropertyEventHandler));
            CameraSessionOpen = true;

            LogInfo($"Connected to Camera: {newCamera.Info.szDeviceDescription}");
        }
    }

    private void AddCameraHandler(Func<SDKError> action, string handlerName)
    {
        LogInfo($"Adding handler: {handlerName}");

        Error = action();
    }


    /// <summary>
    /// Closes the session with the current camera
    /// </summary>
    public void CloseSession()
    {
        _logger?.LogDebug("Closing session");
        if (CameraSessionOpen)
        {
            //if live view is still on, stop it and wait till the thread has stopped
            if (IsLiveViewOn)
            {
                StopLiveView();
                LVThread.Join(1000);
            }

            //Remove the event handler
            EDSDK_API.EdsSetCameraStateEventHandler(MainCamera.Handle, StateEvent.All, null, MainCamera.Handle);
            EDSDK_API.EdsSetObjectEventHandler(MainCamera.Handle, EdsEvent.All, null, MainCamera.Handle);
            EDSDK_API.EdsSetPropertyEventHandler(MainCamera.Handle, PropertyEvent.All, null, MainCamera.Handle);

            //close session and release camera
            SendSDKCommand(delegate { Error = EDSDK_API.EdsCloseSession(MainCamera.Handle); }, sdkAction: nameof(EDSDK_API.EdsCloseSession));
            SDKError c = EDSDK_API.Release(MainCamera.Handle);
            CameraSessionOpen = false;
        }
    }

    /// <summary>
    /// Closes open session and terminates the SDK
    /// </summary>
    public void Dispose()
    {
        //close session
        CloseSession();
        //terminate SDK
        Error = EdsTerminateSDK();
        //stop command execution thread
        STAThread.Shutdown();
    }

    #endregion
    #region Eventhandling

    /// <summary>
    /// A new camera was plugged into the computer
    /// </summary>
    /// <param name="inContext">The pointer to the added camera</param>
    /// <returns>An EDSDK errorcode</returns>
    private SDKError SDKHandler_CameraAddedEvent(nint inContext)
    {
        //Handle new camera here
        OnCameraAdded();

        return SDKError.OK;
    }

    private void OnCameraAdded() => CameraAdded?.Invoke();


    /// <summary>
    /// An Objectevent fired
    /// </summary>
    /// <param name="inEvent">The ObjectEvent id</param>
    /// <param name="inRef">Pointer to the object</param>
    /// <param name="inContext"></param>
    /// <returns>An EDSDK errorcode</returns>
    private SDKError Camera_SDKObjectEvent(EdsEvent inEvent, nint inRef, nint inContext)
    {
        SDKProperty eventProperty = SDKObjectEventToProperty(inEvent);
        //LogInfo("SDK Object Event. Name: {SDKEventName}, Value: {SDKEventHex}", eventProperty.Name, eventProperty.ValueToString());

        //handle object event here
        switch (inEvent)
        {
            case EdsEvent.All:
                break;
            case EdsEvent.DirItemCancelTransferDT:
                break;
            case EdsEvent.DirItemContentChanged:
                break;
            case EdsEvent.DirItemCreated:
                if (DownloadVideo)
                {
                    DownloadImage(inRef, ImageSaveDirectory, ImageSaveFilename, isVideo: true);
                    DownloadVideo = false;
                }
                break;
            case EdsEvent.DirItemInfoChanged:
                break;
            case EdsEvent.DirItemRemoved:
                break;
            case EdsEvent.DirItemRequestTransfer:
                DownloadImage(inRef, ImageSaveDirectory, ImageSaveFilename);
                break;
            case EdsEvent.DirItemRequestTransferDT:
                break;
            case EdsEvent.FolderUpdateItems:
                break;
            case EdsEvent.VolumeAdded:
                break;
            case EdsEvent.VolumeInfoChanged:
                break;
            case EdsEvent.VolumeRemoved:
                break;
            case EdsEvent.VolumeUpdateItems:
                break;
        }

        return SDKError.OK;
    }

    /// <summary>
    /// A progress was made
    /// </summary>
    /// <param name="inPercent">Percent of progress</param>
    /// <param name="inContext">...</param>
    /// <param name="outCancel">Set true to cancel event</param>
    /// <returns>An EDSDK errorcode</returns>
    private SDKError Camera_SDKProgressCallbackEvent(uint inPercent, nint inContext, ref bool outCancel)
    {
        //Handle progress here
        OnProgressChanged((int)inPercent);
        return SDKError.OK;
    }

    private void OnProgressChanged(int percent) => ProgressChanged?.Invoke(percent);

    /// <summary>
    /// A property changed
    /// </summary>
    /// <param name="inEvent">The PropertyEvent ID</param>
    /// <param name="inPropertyID">The Property ID</param>
    /// <param name="inParameter">Event Parameter</param>
    /// <param name="inContext">...</param>
    /// <returns>An EDSDK errorcode</returns>
    private SDKError Camera_SDKPropertyEvent(PropertyEvent inEvent, uint inPropertyID, uint inParameter, nint inContext)
    {
        if (inEvent is PropertyEvent.PropertyChanged)
            LogPropertyValue(inPropertyID, GetSetting(inPropertyID));

        if (inPropertyID == PropID_Evf_OutputDevice && IsLiveViewOn)
            DownloadEvf();

        return SDKError.OK;
    }

    /// <summary>
    /// The camera state changed
    /// </summary>
    /// <param name="inEvent">The StateEvent ID</param>
    /// <param name="inParameter">Parameter from this event</param>
    /// <param name="inContext">...</param>
    /// <returns>An EDSDK errorcode</returns>
    private SDKError Camera_SDKStateEvent(StateEvent inEvent, uint inParameter, nint inContext)
    {
        SDKProperty stateProperty = GetStateEvent(inEvent);

        LogInfo($"SDK State Event. Name: {stateProperty.Name}, Hex {stateProperty.ValueToString()}");

        //Handle state event here
        switch (inEvent)
        {
            case StateEvent.All:
            case StateEvent.JobStatusChanged:
            case StateEvent.ShutDownTimerUpdate:
                break;
            case StateEvent.CaptureError:
            case StateEvent.InternalError:
                LogError($"Error event. error: {inEvent}");

                break;
            case StateEvent.Shutdown:
                CameraSessionOpen = false;

                if (IsLiveViewOn)
                {
                    StopLiveView();
                    LVThread.Abort(); // Not supported in .NET Core. Transition to cancellation token LVThread.Abort();
#error todo fix this shite
                }

                OnCameraHasShutdown();

                break;
            case StateEvent.WillSoonShutDown:
                if (KeepAlive)
                    SendSDKCommand(() =>
                    {
                        _logger.LogDebug("Extending camera shutdown timer");

                        Error = EDSDK_API.EdsSendCommand(MainCamera.Handle, CameraCommand.ExtendShutDownTimer, 0);
                    }, sdkAction: nameof(CameraCommand.ExtendShutDownTimer));
                break;
        }

        return SDKError.OK;
    }

    private void OnCameraHasShutdown() => CameraHasShutdown?.Invoke(this, new EventArgs());

    private void OnSdkError(SDKErrorEventArgs e) => SdkError?.Invoke(this, e);

    #endregion Eventhandling
    #region Camera commands
    #region Download data

    /// <summary>
    /// Downloads an image to given directory
    /// </summary>
    /// <param name="ObjectPointer">Pointer to the object. Get it from the SDKObjectEvent.</param>
    /// <param name="directory">Path to where the image will be saved to</param>
    public void DownloadImage(nint ObjectPointer, string directory, string fileName, bool isVideo = false)
    {
        try
        {
            //get information about object
            Error = EDSDK_API.EdsGetDirectoryItemInfo(ObjectPointer, out EdsDirectoryItemInfo dirInfo);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = dirInfo.szFileName;
            }
            else
            {
                FileInfo targetInfo = new(fileName);
                FileInfo cameraInfo = new(dirInfo.szFileName);

                if (!string.Equals(targetInfo.Extension, cameraInfo.Extension, StringComparison.CurrentCultureIgnoreCase))
                {
                    fileName = $"{targetInfo.Name[..^targetInfo.Extension.Length]}{cameraInfo.Extension}";
                }
            }

            LogInfo("Downloading data. Filename: {fileName}");

            string targetImage = Path.Combine(directory, fileName);
            if (File.Exists(targetImage))
            {
                throw new NotImplementedException("Renaming files not permitted");
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            LogInfo("Downloading data {fileName} to {directory}");

            SendSDKCommand(delegate
            {
                Stopwatch stopWatch = Stopwatch.StartNew();

                //create filestream to data
                Error = EDSDK_API.EdsCreateFileStream(targetImage, EdsFileCreateDisposition.CreateAlways, EdsAccess.ReadWrite, out nint streamRef);
                //download file
                STAThread.TryLockAndExecute(STAThread.ExecLock, nameof(STAThread.ExecLock), TimeSpan.FromSeconds(30), () => DownloadData(ObjectPointer, streamRef));
                //release stream
                Error = EDSDK_API.Release(streamRef);

                stopWatch.Stop();

                FileInfo downloadFile = new(targetImage);
                double mB = downloadFile.Length / 1000.0 / 1000;

                LogInfo($"Downloaded data. Filename: {targetImage}, FileLengthMB: {mB.ToString("0.0")}, DurationSeconds: {stopWatch.Elapsed.TotalSeconds.ToString("0.0")}, MBPerSecond: {(mB / stopWatch.Elapsed.TotalSeconds).ToString("0.0")}");

                if (isVideo)
                    videoDownloadDone?.TrySetResult(targetImage);
                else
                    takePhotoCompletionSource?.TrySetResult(new FileInfo(fileName));
            }, true);
        }
        catch (Exception x)
        {
            _logger.LogError(x, "Error downloading data");
            takePhotoCompletionSource.TrySetException(x);
            videoDownloadDone?.TrySetException(x);
        }

    }


    /// <summary>
    /// Downloads a jpg image from the camera into a Bitmap. Fires the ImageDownloaded event when done.
    /// </summary>
    /// <param name="ObjectPointer">Pointer to the object. Get it from the SDKObjectEvent.</param>
    public void DownloadImage(nint ObjectPointer)
    {
        //get information about image
        EdsDirectoryItemInfo dirInfo = new();
        Error = EDSDK_API.EdsGetDirectoryItemInfo(ObjectPointer, out dirInfo);

        //check the extension. Raw data cannot be read by the bitmap class
        string ext = Path.GetExtension(dirInfo.szFileName).ToLower();

        LogInfo($"Downloading image {dirInfo.szFileName}");

        if (ext is ".jpg" or ".jpeg")
            SendSDKCommand(delegate
            {
                Bitmap bmp;

                //create memory stream
                Error = EDSDK_API.EdsCreateMemoryStream(dirInfo.Size, out nint streamRef);

                //download data to the stream
                lock (STAThread.ExecLock)
                {
                    DownloadData(ObjectPointer, streamRef);
                }
                Error = EDSDK_API.EdsGetPointer(streamRef, out nint jpgPointer);
                Error = EDSDK_API.EdsGetLength(streamRef, out ulong length);

                unsafe
                {
                    //create a System.IO.Stream from the pointer
                    using UnmanagedMemoryStream ums = new((byte*)jpgPointer.ToPointer(), (long)length, (long)length, FileAccess.Read);
                    //create bitmap from stream (it's a normal jpeg image)
                    bmp = new Bitmap(ums);
                }

                Error = EDSDK_API.Release(streamRef);

                //Fire the event with the image
                OnImageDownloaded(bmp);
            }, true);
        else
        {
            //if it's a RAW image, cancel the download and release the image
            SendSDKCommand(delegate { Error = EDSDK_API.EdsDownloadCancel(ObjectPointer); });

            Error = EDSDK_API.Release(ObjectPointer);
        }
    }

    protected void OnImageDownloaded(Bitmap bitmap) => ImageDownloaded?.Invoke(bitmap);

    /// <summary>
    /// Gets the thumbnail of an image (can be raw or jpg)
    /// </summary>
    /// <param name="filepath">The filename of the image</param>
    /// <returns>The thumbnail of the image</returns>
    public Bitmap GetFileThumb(string filepath)
    {
        //create a filestream to given file
        Error = EDSDK_API.EdsCreateFileStream(filepath, EdsFileCreateDisposition.OpenExisting, EdsAccess.Read, out nint stream);
        return GetImage(stream, EdsImageSource.Thumbnail);
    }

    /// <summary>
    /// Downloads data from the camera
    /// </summary>
    /// <param name="ObjectPointer">Pointer to the object</param>
    /// <param name="stream">Pointer to the stream created in advance</param>
    private void DownloadData(nint ObjectPointer, nint stream)
    {
        //get information about the object
        Error = EDSDK_API.EdsGetDirectoryItemInfo(ObjectPointer, out EdsDirectoryItemInfo dirInfo);

        try
        {
            //set progress event
            Error = EDSDK_API.EdsSetProgressCallback(stream, SDKProgressCallbackEvent, EdsProgressOption.Periodically, ObjectPointer);
            //download the data
            Error = EDSDK_API.EdsDownload(ObjectPointer, dirInfo.Size, stream);
        }
        finally
        {
            //set the download as complete
            Error = EDSDK_API.EdsDownloadComplete(ObjectPointer);
            Error = EDSDK_API.Release(ObjectPointer);
        }
    }

    /// <summary>
    /// Creates a Bitmap out of a stream
    /// </summary>
    /// <param name="img_stream">Image stream</param>
    /// <param name="imageSource">Type of image</param>
    /// <returns>The bitmap from the stream</returns>
    private Bitmap GetImage(nint img_stream, EdsImageSource imageSource)
    {
        nint stream = 0;
        nint img_ref = 0;
        nint streamPointer = 0;

        try
        {
            //create reference and get image info
            Error = EDSDK_API.EdsCreateImageRef(img_stream, out img_ref);
            Error = EDSDK_API.EdsGetImageInfo(img_ref, imageSource, out EdsImageInfo imageInfo);

            EdsSize outputSize = new(imageInfo.EffectiveRect.Width, imageInfo.EffectiveRect.Height);
            //calculate amount of data
            int datalength = outputSize.height * outputSize.width * 3;
            //create buffer that stores the image
            byte[] buffer = new byte[datalength];
            //create a stream to the buffer

            nint ptr = new();
            Marshal.StructureToPtr(buffer, ptr, false);


            Error = EDSDK_API.EdsCreateMemoryStreamFromPointer(ptr, (uint)datalength, out stream);
            //load image into the buffer
            Error = EDSDK_API.EdsGetImage(img_ref, imageSource, EdsTargetImageType.RGB, imageInfo.EffectiveRect, outputSize, stream);

            //create output bitmap
            Bitmap bmp = new(outputSize.width, outputSize.height, PixelFormat.Format24bppRgb);

            //assign values to bitmap and make BGR from RGB (System.Drawing (i.e. GDI+) uses BGR)
            unsafe
            {
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);

                byte* outPix = (byte*)data.Scan0;
                fixed (byte* inPix = buffer)
                {
                    for (int i = 0; i < datalength; i += 3)
                    {
                        outPix[i] = inPix[i + 2];//Set B value with R value
                        outPix[i + 1] = inPix[i + 1];//Set G value
                        outPix[i + 2] = inPix[i];//Set R value with B value
                    }
                }

                bmp.UnlockBits(data);
            }

            return bmp;
        }
        finally
        {
            EDSDK_API.Release(img_stream);
            EDSDK_API.Release(img_ref);
            EDSDK_API.Release(stream);
        }
    }

    #endregion
    #region Get Settings

    /// <summary>
    /// Gets the list of possible values for the current camera to set.
    /// Only the PropertyIDs "AEModeSelect", "ISO", "Av", "Tv", "MeteringMode" and "ExposureCompensation" are allowed.
    /// </summary>
    /// <param name="PropID">The property ID</param>
    /// <returns>A list of available values for the given property ID</returns>
    public List<int> GetSettingsList(uint PropID)
    {
        if (MainCamera.Handle != 0)
        {
            //a list of settings can only be retrieved for following properties
            if (PropID == PropID_AEModeSelect || PropID == PropID_ISOSpeed || PropID == PropID_Av
                || PropID == PropID_Tv || PropID == PropID_MeteringMode || PropID == PropID_ExposureCompensation)
            {
                //get the list of possible values
                EdsPropertyDesc des = new();
                Error = EDSDK_API.EdsGetPropertyDesc(MainCamera.Handle, PropID, out des);
                return des.PropDesc.Take(des.NumElements).ToList();
            }
            else
            {
                throw new ArgumentException("Method cannot be used with this Property ID");
            }
        }
        else
        {
            throw new ArgumentNullException("Camera or camera reference is null/zero");
        }
    }

    /// <summary>
    /// Gets the current setting of given property ID as an uint
    /// </summary>
    /// <param name="PropID">The property ID</param>
    /// <returns>The current setting of the camera</returns>
    public uint GetSetting(uint PropID)
    {
        if (MainCamera.Handle != 0)
        {
            Error = EDSDK_API.EdsGetPropertyData(MainCamera.Handle, PropID, 0, out uint property);
            return property;
        }
        else
        {
            throw new ArgumentNullException("Camera or camera reference is null/zero");
        }
    }

    /// <summary>
    /// Gets the current setting of given property ID as a string
    /// </summary>
    /// <param name="PropID">The property ID</param>
    /// <returns>The current setting of the camera</returns>
    public string GetStringSetting(uint PropID)
    {
        if (MainCamera.Handle != 0)
        {
            string data = string.Empty;
            EDSDK_API.EdsGetPropertyData(MainCamera.Handle, PropID, 0, out data);
            return data;
        }
        else
        {
            throw new ArgumentNullException("Camera or camera reference is null/zero");
        }
    }

    /// <summary>
    /// Gets the current setting of given property ID as a struct
    /// </summary>
    /// <param name="PropID">The property ID</param>
    /// <typeparam name="T">One of the EDSDK structs</typeparam>
    /// <returns>The current setting of the camera</returns>
    public T GetStructSetting<T>(uint PropID) where T : struct
    {
        if (MainCamera.Handle != 0)
        {
            //get type and size of struct
            Type structureType = typeof(T);
            int bufferSize = Marshal.SizeOf(structureType);

            //allocate memory
            nint ptr = Marshal.AllocHGlobal(bufferSize);
            //retrieve value
            Error = EDSDK_API.EdsGetPropertyData(MainCamera.Handle, PropID, 0, bufferSize, ptr);

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
        else
        {
            throw new ArgumentNullException("Camera or camera reference is null/zero");
        }
    }

    #endregion
    #region Set Settings

    /// <summary>
    /// Sets an uint value for the given property ID
    /// </summary>
    /// <param name="propertyId">The property ID</param>
    /// <param name="value">The value which will be set</param>
    public void SetSetting(uint propertyId, uint value)
    {
        LogSetProperty(propertyId, $"0x{value:X}");
        
        if (MainCamera.Handle != 0)
        {
            SendSDKCommand(delegate
            {
                Thread cThread = Thread.CurrentThread;

                LogInfo($"Executing SDK command. ThreadName: {cThread.Name}, ApartmentState: {cThread.GetApartmentState()}");

                //get size of property
                Error = EDSDK_API.EdsGetPropertySize(MainCamera.Handle, propertyId, 0, out EdsDataType proptype, out int propsize);
                //set given property
                Error = EDSDK_API.EdsSetPropertyData(MainCamera.Handle, propertyId, 0, propsize, value);
            }, sdkAction: nameof(EDSDK_API.EdsSetPropertyData));
        }
        else
            throw new InvalidOperationException("Camera or camera reference is null/zero");
    }

    /// <summary>
    /// Sends a camera command
    /// </summary>
    /// <param name="propertyId">The property ID</param>
    /// <param name="value">The value which will be set</param>
    public void SendCommand(CameraCommand command, int value)
    {
        Log(LogLevel.Debug, $"Sending command. Command: {command}, Value: 0x{value:X}");

        if (MainCamera.Handle != 0)
            SendSDKCommand(delegate
            {
                Thread cThread = Thread.CurrentThread;

                LogInfo($"Executing SDK command. ThreadName: {cThread.Name}, ApartmentState: {cThread.GetApartmentState()}");

                Error = EDSDK_API.EdsSendCommand(MainCamera.Handle, command, value);
            }, sdkAction: nameof(EDSDK_API.EdsSetPropertyData));
        else
            throw new InvalidOperationException("Camera or camera reference is null/zero");
    }

    /// <summary>
    /// Sets a DateTime value for the given property ID
    /// </summary>
    /// <param name="PropID">The property ID</param>
    /// <param name="Value">The value which will be set</param>
    public void SetDateTimeSetting(uint propertyId, DateTime value)
    {
        EdsTime dateTime = new()
        {
            Year = value.Year,
            Month = value.Month,
            Day = value.Day,
            Hour = value.Hour,
            Minute = value.Minute,
            Second = value.Second
        };
        SetStructSetting(propertyId, dateTime);
    }

    /// <summary>
    /// Sets a string value for the given property ID
    /// </summary>
    /// <param name="propertyId">The property ID</param>
    /// <param name="value">The value which will be set</param>
    public void SetStringSetting(uint propertyId, string value)
    {
        LogSetProperty(propertyId, value);
        //TODO: Refactor to remove duplicate code in Set_XXX_Setting methods
        if (MainCamera.Handle != 0)
        {
            if (value == null)
            {
                throw new ArgumentNullException("String must not be null");
            }

            //convert string to byte array
            byte[] propertyValueBytes = System.Text.Encoding.ASCII.GetBytes(value + '\0');
            int propertySize = propertyValueBytes.Length;

            //check size of string
            if (propertySize > 32)
            {
                throw new ArgumentOutOfRangeException("Value must be smaller than 32 bytes");
            }

            //set value
            SendSDKCommand(delegate { Error = EDSDK_API.EdsSetPropertyData(MainCamera.Handle, propertyId, 0, 32, propertyValueBytes); }, sdkAction: nameof(EDSDK_API.EdsSetPropertyData));
        }
        else
        {
            throw new ArgumentNullException("Camera or camera reference is null/zero");
        }
    }

    /// <summary>
    /// Sets a struct value for the given property ID
    /// </summary>
    /// <param name="propertyId">The property ID</param>
    /// <param name="value">The value which will be set</param>
    public void SetStructSetting<T>(uint propertyId, T value) where T : struct
    {
        LogSetProperty(propertyId, value.ToString());
        if (MainCamera.Handle != 0)
        {
            SendSDKCommand(delegate { Error = EDSDK_API.EdsSetPropertyData(MainCamera.Handle, propertyId, 0, Marshal.SizeOf(typeof(T)), value); }, sdkAction: nameof(EDSDK_API.EdsSetPropertyData));
        }
        else
        {
            throw new ArgumentNullException("Camera or camera reference is null/zero");
        }
    }

    #endregion
    #region Live view

    /// <summary>
    /// Starts the live view
    /// </summary>
    public async Task StartLiveView()
    {
        if (!IsLiveViewOn)
        {
            LogInfo("Starting Liveview");

            int listener_count = LiveViewUpdated?.GetInvocationList()?.Length ?? 0;

            LogInfo($"{listener_count} LiveViewUpdated listeners found");
            LogPropertyValue(nameof(PropID_Evf_OutputDevice), GetSetting(PropID_Evf_OutputDevice));

            SetSetting(PropID_Evf_OutputDevice, EvfOutputDevice_PC);

            IsLiveViewOn = true;

            LogPropertyValue(nameof(PropID_Evf_OutputDevice), GetSetting(PropID_Evf_OutputDevice));
        }
    }

    /// <summary>
    /// Stops the live view
    /// </summary>
    public void StopLiveView()
    {
        if (IsLiveViewOn)
        {
            LogInfo("Stopping liveview");

            LVoff = true;
            IsLiveViewOn = false;

            //Wait 5 seconds for evf thread to finish, otherwise manually stop
            if (!cancelLiveViewWait.WaitOne(TimeSpan.FromSeconds(5)))
                KillLiveView();
            else
                _logger.LogDebug("LiveView stopped cleanly");
        }
    }

    private readonly AutoResetEvent cancelLiveViewWait = new(false);

    /// <summary>
    /// Downloads the live view image
    /// </summary>
    private void DownloadEvf()
    {
        LVThread = STAThread.Create(delegate
        {
            try
            {
                nint EvfImageRef = 0;
                UnmanagedMemoryStream ums;

                SDKError err;
                //create stream
                Error = EDSDK_API.EdsCreateMemoryStream(0, out nint stream);

                //run live view
                while (IsLiveViewOn)
                {
                    lock (STAThread.ExecLock)
                    {
                        //download current live view image
                        err = EDSDK_API.EdsCreateEvfImageRef(stream, out EvfImageRef);

                        if (err == SDKError.OK)
                            err = EDSDK_API.EdsDownloadEvfImage(MainCamera.Handle, EvfImageRef);

                        if (err == SDKError.OBJECT_NOTREADY)
                        {
                            Thread.Sleep(4);

                            continue;
                        }
                        else
                            Error = err;
                    }

                    //get pointer
                    Error = EDSDK_API.EdsGetPointer(stream, out nint jpgPointer);
                    Error = EDSDK_API.EdsGetLength(stream, out ulong length);

                    //get some live view image metadata
                    if (!IsCoordSystemSet)
                    {
                        Evf_CoordinateSystem = GetEvfCoord(EvfImageRef);
                        IsCoordSystemSet = true;
                    }

                    Evf_ZoomRect = GetEvfZoomRect(EvfImageRef);
                    Evf_ZoomPosition = GetEvfPoints(PropID_Evf_ZoomPosition, EvfImageRef);
                    Evf_ImagePosition = GetEvfPoints(PropID_Evf_ImagePosition, EvfImageRef);

                    //release current evf image
                    Error = EDSDK_API.Release(EvfImageRef);

                    //create stream to image
                    unsafe
                    {
                        ums = new UnmanagedMemoryStream((byte*)jpgPointer.ToPointer(), (long)length, (long)length, FileAccess.Read);
                    }

                    //fire the LiveViewUpdated event with the live view image stream
                    OnLiveViewUpdated(ums);
                    ums.Close();
                }

                Error = EDSDK_API.Release(stream);

                KillLiveView();

                cancelLiveViewWait.Set();
            }
            catch
            {
                IsLiveViewOn = false;
            }
        });
        LVThread.Start();
    }

    private void KillLiveView()
    {
        LogInfo("Killing LiveView");

        //stop the live view
        SetSetting(PropID_Evf_OutputDevice, LVoff ? 0 : EvfOutputDevice_TFT);
    }


    /// <summary>
    /// Fires the LiveViewUpdated event
    /// </summary>
    /// <param name="stream"></param>
    private void OnLiveViewUpdated(UnmanagedMemoryStream stream) => LiveViewUpdated?.Invoke(stream);


    /// <summary>
    /// Get the live view ZoomRect value
    /// </summary>
    /// <param name="imgRef">The live view reference</param>
    /// <returns>ZoomRect value</returns>
    private EdsRect GetEvfZoomRect(nint imgRef)
    {
        EDSDK_API.EdsGetPropertyData<EdsRect>(imgRef, EDSDK_API.PropID_Evf_ZoomPosition, 0, out EdsRect rect);

        return rect;
    }

    /// <summary>
    /// Get the live view coordinate system
    /// </summary>
    /// <param name="imgRef">The live view reference</param>
    /// <returns>the live view coordinate system</returns>
    private static EdsSize GetEvfCoord(nint imgRef)
    {
        EDSDK_API.EdsGetPropertyData<EdsSize>(imgRef, EDSDK_API.PropID_Evf_CoordinateSystem, 0, out EdsSize size);

        return size;
    }

    /// <summary>
    /// Get a live view EdsPoint value
    /// </summary>
    /// <param name="imgRef">The live view reference</param>
    /// <returns>a live view EdsPoint value</returns>
    private static EdsPoint GetEvfPoints(uint PropID, nint imgRef)
    {
        EDSDK_API.EdsGetPropertyData<EdsPoint>(imgRef, PropID, 0, out EdsPoint point);

        return point;
    }

    #endregion
    #region Filming

    /// <summary>
    /// Starts recording a video and downloads it when finished
    /// </summary>
    /// <param name="FilePath">Directory to where the final video will be saved to</param>
    public void StartFilming(string FilePath)
    {
        if (!IsFilming)
        {
            StartFilming();
            DownloadVideo = true;
            ImageSaveDirectory = FilePath;
        }
    }

    public static void SetTFTEvf() => SetSetting(PropID_Evf_OutputDevice, EvfOutputDevice_TFT);

    /// <summary>
    /// Starts recording a video
    /// NOTE: Will throw an ArgumentException if the camera is not in the correct mode
    /// </summary>
    public void StartFilming()
    {
        if (!IsFilming)
        {
            //Snapshot setting for restoration after filming completes
            PrevEVFSetting = GetSetting(PropID_Evf_OutputDevice);


            //Set EVF output to TFT to enable film, otherwise
            //NOTE: Not working to set it and start video in the same action, disabling
            //SetSetting(PropID_Evf_OutputDevice, EvfOutputDevice_TFT);
            //SetTFTEvf();

            //LogPropertyValue(nameof(PropID_Record), GetSetting(PropID_Record));

            SetSetting(PropID_Evf_OutputDevice, 3);

            //Check if the camera is ready to film
            uint recordStatus = GetSetting(PropID_Record);
            if (recordStatus != (uint)PropID_Record_Status.Movie_shooting_ready)
            {
                //DOES NOT WORK, readonly setting?
                //DOES NOT THROW AN ERROR
                //SetSetting(PropID_Record, (uint)EdsDriveMode.Video);
                //SetSetting(PropID_Record, (uint)PropID_Record_Status.Movie_shooting_ready);


                LogPropertyValue(PropID_Record, recordStatus);
                Task tx = Log(LogLevel.Information, "Camera reporting incorrect mode. expected. Continue. {expected}, was: {was}", PropID_Record_Status.Movie_shooting_ready, recordStatus);
                tx = Log(LogLevel.Information, "Camera physical switch must be in movie record mode. Leave in this mode permanently!");
                //throw new ArgumentException("Camera in invalid mode", nameof(PropID_Record));
            }
            IsFilming = true;


            //to restore the current setting after recording
            PrevSaveTo = GetSetting(PropID_SaveTo);


            //when recording videos, it has to be saved on the camera internal memory
            SetSetting(PropID_SaveTo, (uint)EdsSaveTo.Camera);
            DownloadVideo = false;
            //start the video recording

            LogInfo("Start filming");

            SendSDKCommand(delegate { Error = EDSDK_API.EdsSetPropertyData(MainCamera.Handle, PropID_Record, 0, sizeof(PropID_Record_Status), (uint)PropID_Record_Status.Begin_movie_shooting); });
        }
    }

    private TaskCompletionSource<string> videoDownloadDone;

    /// <summary>
    /// Stops recording a video
    /// </summary>
    public TaskCompletionSource<string> StopFilming(out long unixTimeMs)
    {
        if (IsFilming)
        {
            videoDownloadDone = new TaskCompletionSource<string>();
            long stopMs = 0;
            SendSDKCommand(delegate
            {
                //Shut off live view (it will hang otherwise)
                //StopLiveView(false);
                //stop video recording
                Error = EDSDK_API.EdsSetPropertyData(MainCamera.Handle, PropID_Record, 0, sizeof(PropID_Record_Status), (uint)PropID_Record_Status.End_movie_shooting);
                stopMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                //set back to previous state
            });
            SetSetting(PropID_SaveTo, PrevSaveTo);
            SetSetting(PropID_Evf_OutputDevice, PrevEVFSetting);
            if (PrevCapacity.NumberOfFreeClusters != 0)
            {
                SetCapacity(PrevCapacity);
            }
            IsFilming = false;
            unixTimeMs = stopMs;
        }
        else
        {
            videoDownloadDone.SetResult(null);
            unixTimeMs = 0;
        }
        return videoDownloadDone;
    }

    #endregion
    #region Taking photos

    /// <summary>
    /// Press the shutter button
    /// </summary>
    /// <param name="state">State of the shutter button</param>
    public void PressShutterButton(EdsShutterButton state) =>
        //start thread to not block everything
        SendSDKCommand(delegate
        {
            //send command to camera
            lock (STAThread.ExecLock)
            {
                Error = EDSDK_API.EdsSendCommand(MainCamera.Handle, CameraCommand.PressShutterButton, (int)state);
            };
        }, true);

    /// <summary>
    /// Takes a photo and returns the file info
    /// </summary>
    /// <returns></returns>
    public async Task<FileInfo?> TakePhotoAsync(FileInfo saveFile)
    {
        if (IsFilming || IsLiveViewOn)
        {
            LogWarning($"Ignoring attempt to take photo whilst filming or in live-view mode. Filming: {IsFilming}, LiveView: {IsLiveViewOn}");

            return null;
        }

        takePhotoCompletionSource = new TaskCompletionSource<FileInfo>();
        SetSaveToLocation(saveFile.Directory);
        ImageSaveFilename = saveFile.Name;

        TakePhoto();

        await takePhotoCompletionSource.Task;

        if (takePhotoCompletionSource.Task.Status == TaskStatus.RanToCompletion)
            return takePhotoCompletionSource.Task.Result;

        LogError("Error taking photo, check previous messages");

        return null;
    }



    public void SetSaveToLocation(DirectoryInfo directory)
    {
        SetSaveToHost();
        ImageSaveDirectory = directory.FullName;
    }

    /// <summary>
    /// Takes a photo with the current camera settings
    /// </summary>
    public void TakePhoto() =>
        //start thread to not block everything
        SendSDKCommand(delegate
        {
            //send command to camera
            lock (STAThread.ExecLock)
            {
                Error = EDSDK_API.EdsSendCommand(MainCamera.Handle, CameraCommand.TakePicture, 0);
            };
        }, true);

    /// <summary>
    /// Takes a photo in bulb mode with the current camera settings
    /// </summary>
    /// <param name="bulbTime">The time in milliseconds for how long the shutter will be open</param>
    public void TakePhoto(uint bulbTime)
    {
        //bulbtime has to be at least a second
        if (bulbTime < 1000)
        {
            throw new ArgumentException("Bulbtime has to be bigger than 1000ms");
        }

        //start thread to not block everything
        SendSDKCommand(delegate
        {
            //open the shutter
            lock (STAThread.ExecLock)
                Error = EDSDK_API.EdsSendCommand(MainCamera.Handle, CameraCommand.BulbStart, 0);

            //wait for the specified time
            Thread.Sleep((int)bulbTime);

            //close shutter
            lock (STAThread.ExecLock)
                Error = EDSDK_API.EdsSendCommand(MainCamera.Handle, CameraCommand.BulbEnd, 0);
        }, true);
    }


    public void FormatAllVolumes() => RunForEachVolume((childReference, volumeInfo) =>
                                                                              {
                                                                                  _logger.LogInformation("Formatting volume. Volume: {Volume}", volumeInfo.szVolumeLabel);
                                                                                  SendSDKCommand(() => Error = EDSDK_API.EdsFormatVolume(childReference));
                                                                              });

    public float GetMinVolumeSpacePercent()
    {
        float minPercent = 1f;
        RunForEachVolume((childReference, volumeInfo) =>
        {
            var freePc = volumeInfo.FreeSpaceInBytes / (float)volumeInfo.MaxCapacity;
            _logger.LogDebug("Camera volume free space. volume: {volume}, freeSpaceBytes: {freeSpaceBytes}, maxCapacity: {maxCapacity}, freePercent: {freePercent}", volumeInfo.szVolumeLabel, volumeInfo.FreeSpaceInBytes, volumeInfo.MaxCapacity, freePc);
            if (freePc < minPercent)
            {
                minPercent = freePc;
            }
        });
        return minPercent;
    }

    private void RunForEachVolume(Action<nint, EdsVolumeInfo> action)
    {
        //get the number of volumes currently installed in the camera
        Error = EDSDK_API.EdsGetChildCount(GetCamera().Handle, out int VolumeCount);

        for (int i = 0; i < VolumeCount; i++)
        {
            //get information about volume
            Error = EDSDK_API.EdsGetChildAtIndex(MainCamera.Handle, i, out nint childReference);
            EdsVolumeInfo volumeInfo = new();
            SendSDKCommand(delegate { Error = EDSDK_API.EdsGetVolumeInfo(childReference, out volumeInfo); });

            if (volumeInfo.StorageType != (uint)EdsStorageType.Non && volumeInfo.Access == (uint)EdsAccess.ReadWrite)
            {
                action(childReference, volumeInfo);
            }
            Error = EDSDK_API.Release(childReference);
        }
    }


    public void FormatVolume(CameraFileEntry volume)
    {
        throw new NotImplementedException();
        // NOTE: Need to marry up obj ref to camera entry then delete based on camera entry / ref
        _logger.LogDebug("Formatting volume. Volume: {Volume}", volume.Name);
        SendSDKCommand(() =>
        {
            nint ptr = Marshal.AllocHGlobal(Marshal.SizeOf(volume.Volume));
            try
            {
                Marshal.StructureToPtr(volume.Volume, ptr, false);
                Error = EDSDK_API.EdsFormatVolume(ptr);

            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }, sdkAction: nameof(EDSDK_API.EdsFormatVolume)
        );
    }


    #endregion

    #region Other

    /// <summary>
    /// Sends a command to the camera safely
    /// </summary>
    private void SendSDKCommand(Action command, bool longTask = false, string? sdkAction = null)
    {
        if (sdkAction != null)
            LogInfo($"Sending SDK command: {sdkAction}");

        try
        {
            if (longTask)
                STAThread.Create(command).Start();
            else
                STAThread.ExecuteSafely(command);
        }
        catch (Exception ex)
        {
            HandleException(ex, $"Error sending command: {sdkAction}");
        }
    }

    /// <summary>
    /// Tells the camera that there is enough space on the HDD if SaveTo is set to Host
    /// This method does not use the actual free space!
    /// </summary>
    public void SetCapacity() => SetCapacity(0x1000, 0x7FFFFFFF);

    /// <summary>
    /// Tells the camera how much space is available on the host PC
    /// </summary>
    /// <param name="bytesPerSector">Bytes per sector on HD</param>
    /// <param name="numberOfFreeClusters">Number of free clusters on HD</param>
    public void SetCapacity(int bytesPerSector, int numberOfFreeClusters)
    {
        //create new capacity struct
        EdsCapacity capacity = new()
        {
            //set given values
            Reset = 1,
            BytesPerSector = bytesPerSector,
            NumberOfFreeClusters = numberOfFreeClusters
        };

        SetCapacity(capacity);
    }

    private void SetCapacity(EdsCapacity capacity)
    {
        PrevCapacity = capacity;
        SendSDKCommand(delegate { Error = EDSDK_API.EdsSetCapacity(MainCamera.Handle, capacity); });
    }




    /// <summary>
    /// Moves the focus (only works while in live view)
    /// </summary>
    /// <param name="speed">Speed and direction of focus movement</param>
    public void SetFocus(uint speed)
    {
        if (IsLiveViewOn)
        {
            SendSDKCommand(delegate { Error = EDSDK_API.EdsSendCommand(MainCamera.Handle, CameraCommand.DriveLensEvf, (int)speed); });
        }
    }

    /// <summary>
    /// Sets the WB of the live view while in live view
    /// </summary>
    /// <param name="x">X Coordinate</param>
    /// <param name="y">Y Coordinate</param>
    public void SetManualWBEvf(ushort x, ushort y)
    {
        if (IsLiveViewOn)
        {
            //converts the coordinates to a form the camera accepts
            byte[] xa = BitConverter.GetBytes(x);
            byte[] ya = BitConverter.GetBytes(y);
            uint coord = BitConverter.ToUInt32(new byte[] { xa[0], xa[1], ya[0], ya[1] }, 0);
            //send command to camera
            SendSDKCommand(delegate { Error = EDSDK_API.EdsSendCommand(MainCamera.Handle, CameraCommand.DoClickWBEvf, (int)coord); });
        }
    }

    public List<CameraFileEntry> GetVolumes() => GetVolumes(GetCamera());

    public List<CameraFileEntry> GetVolumes(CameraFileEntry camera)
    {
        //get the number of volumes currently installed in the camera
        Error = EDSDK_API.EdsGetChildCount(camera.Handle, out int VolumeCount);
        List<CameraFileEntry> volumes = [];

        //iterate through all of them
        for (int i = 0; i < VolumeCount; i++)
        {
            //get information about volume
            Error = EDSDK_API.EdsGetChildAtIndex(MainCamera.Handle, i, out nint childReference);
            EdsVolumeInfo volumeInfo = new();
            SendSDKCommand(delegate { Error = EDSDK_API.EdsGetVolumeInfo(childReference, out volumeInfo); });
            //ignore the HDD
            if (volumeInfo.szVolumeLabel != "HDD")
            {
                //add volume to the list
                volumes.Add(new CameraFileEntry($"Volume{i}({volumeInfo.szVolumeLabel})", CameraFileEntryTypes.Volume, childReference) { Volume = volumeInfo });
            }
            //release the volume
            Error = EDSDK_API.Release(childReference);
        }
        return volumes;
    }

    public CameraFileEntry GetCamera() => new("Camera", CameraFileEntryTypes.Camera, MainCamera.Handle);

    public void DeleteFileItem(CameraFileEntry fileItem)
    {
        throw new NotImplementedException();
        // NOTE: Get original structure from camera to delete
        SendSDKCommand(() =>
        {
            nint ptr = Marshal.AllocHGlobal(Marshal.SizeOf(fileItem));
            try
            {
                Marshal.StructureToPtr(fileItem, ptr, false);
                Error = EDSDK_API.EdsDeleteDirectoryItem(ptr);

            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        );
    }

    /// <summary>
    /// Gets all volumes, folders and files existing on the camera
    /// </summary>
    /// <returns>A CameraFileEntry with all informations</returns>
    public CameraFileEntry GetAllEntries()
    {
        //create the main entry which contains all subentries
        CameraFileEntry camera = GetCamera();

        List<CameraFileEntry> volumes = GetVolumes(camera);

        volumes.ForEach(v => v.AddSubEntries(GetChildren(v.Handle)));

        //add all volumes to the main entry and return it
        camera.AddSubEntries(volumes.ToArray());
        return camera;
    }

    /// <summary>
    /// Locks or unlocks the cameras UI
    /// </summary>
    /// <param name="lockState">True for locked, false to unlock</param>
    public void UILock(bool lockState) => SendSDKCommand(() => Error = EDSDK_API.EdsSendStatusCommand(MainCamera.Handle, lockState ? CameraState.UILock : CameraState.UIUnLock, 0));

    /// <summary>
    /// Gets the children of a camera folder/volume. Recursive method.
    /// </summary>
    /// <param name="ptr">Pointer to volume or folder</param>
    /// <returns></returns>
    private CameraFileEntry[] GetChildren(nint ptr)
    {
        //get children of first pointer
        Error = EDSDK_API.EdsGetChildCount(ptr, out int childCount);
        if (childCount > 0)
        {
            //if it has children, create an array of entries
            CameraFileEntry[] children = new CameraFileEntry[childCount];
            for (int i = 0; i < childCount; i++)
            {
                //get children of children
                Error = EDSDK_API.EdsGetChildAtIndex(ptr, i, out nint childReference);
                //get the information about this children
                EdsDirectoryItemInfo child = new();
                SendSDKCommand(delegate { Error = EDSDK_API.EdsGetDirectoryItemInfo(childReference, out child); });
                //create entry from information
                children[i] = new CameraFileEntry(child.szFileName, child.isFolder != 0 ? CameraFileEntryTypes.Folder : CameraFileEntryTypes.File, childReference);
                if (children[i].Type == CameraFileEntryTypes.File)
                {
                    if (false)
                    {
                        //if it's not a folder, create thumbnail and save it to the entry                       
                        Error = EDSDK_API.EdsCreateMemoryStream(0, out nint stream);
                        SendSDKCommand(delegate { Error = EDSDK_API.EdsDownloadThumbnail(childReference, stream); });
                        children[i].AddThumb(GetImage(stream, EdsImageSource.Thumbnail));
                    }
                }
                else
                {
                    //if it's a folder, check for children with recursion
                    CameraFileEntry[] retval = GetChildren(childReference);
                    if (retval != null)
                    {
                        children[i].AddSubEntries(retval);
                    }
                }

                EDSDK_API.Release(childReference);
            }
            return children;
        }
        else
        {
            return new CameraFileEntry[0];
        }
    }

    #endregion


    private void LogSetProperty(uint propertyId, string value)
    {
        SDKProperty prop = GetSDKProperty(propertyId);

        LogInfo($"Setting property. Name: {prop.Name}, Id: {prop.Value}, Value: {value}");
    }

    public void LogPropertyValue(string propertyName, uint propertyValue) => LogInfo($"Camera_SDKPropertyEvent. Property {propertyName} changed to 0x{propertyValue:X}");

    public void LogPropertyValue(uint propertyID, uint propertyValue)
    {
        SDKProperty prop = GetSDKProperty(propertyID);
        LogPropertyValue(prop.Name, propertyValue);
        //do nothing with task, continue
    }

    private void HandleException(Exception ex, string message) => _logger?.LogError(ex, message);

    private void Log(LogLevel level, string message) => Task.Factory.StartNew(delegate
    {
        if (_logger != null)
        {
            Action<string, object?[]> handler = level switch
            {
                LogLevel.None => delegate { }
                ,
                LogLevel.Trace => _logger.LogTrace,
                LogLevel.Debug => _logger.LogDebug,
                LogLevel.Information => _logger.LogInformation,
                LogLevel.Warning => _logger.LogWarning,
                LogLevel.Critical => _logger.LogCritical,
                LogLevel.Error => _logger.LogError,
                _ => new((m, _) => _logger.LogError($"Unknown level: {level}\nMessage: {m}"))
            };

            handler(message, []);
        }

#if DEBUG
        if (level >= LogLevel.Error)
            throw new(message);
#endif
    });

    private void LogInfo(string message) => Log(LogLevel.Information, message);

    private void LogWarning(string message) => Log(LogLevel.Warning, message);

    private void LogError(string message) => Log(LogLevel.Error, message);
}
