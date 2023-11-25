using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using EDSDK.Native;

namespace EDSDK.NET;


/// <summary>
/// Handles the Canon SDK
/// </summary>
public class SDKHandler : IDisposable
{
    #region Events

    public event EventHandler<SdkErrorEventArgs> SdkError;

    #endregion Events
    #region Variables

    private readonly ILogger logger;

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
            var t = LogInfoAsync("Setting ImageSaveFilename. ImageSaveFilename: {ImageSaveFilename}", value);
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
    /// <summary>
    /// States if the Evf_CoordinateSystem is already set
    /// </summary>
    public bool IsCoordSystemSet = false;
    /// <summary>
    /// Handles errors that happen with the SDK
    /// </summary>
    public uint Error
    {
        get => EDS_ERR_OK;
        set
        {
            if (value != EDS_ERR_OK)
            {
                var errorProperty = SDKErrorToProperty(value);
                LogError("SDK Error. Name: {0}, Value: {1}", errorProperty.Name, errorProperty.ValueToString());


                switch (value)
                {
                    case EDS_ERR_COMM_DISCONNECTED:
                    case EDS_ERR_DEVICE_INVALID:
                    case EDS_ERR_DEVICE_NOT_FOUND:
                        string name = FindProperty(SDKErrors, value).Name;
                        OnSdkError(new SdkErrorEventArgs() { Error = name, ErrorLevel = LogLevel.Critical });
                        break;
                }

            }
        }
    }


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

    public void SetUintSetting(string propertyName, string propertyValue)
    {
        uint value;
        bool error = false;
        if (!string.IsNullOrEmpty(propertyValue))
        {
            propertyValue = propertyValue.Replace("0x", "");
        }

        if (!uint.TryParse(propertyValue, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out value))
        {
            LogError("Could not convert value {0} to uint", propertyValue);
            error = true;
        }

        if (!string.IsNullOrEmpty(propertyName) && propertyName.StartsWith("kEds"))
        {
            propertyName = propertyName[4..];
        }


        var prop = GetSDKProperty(propertyName);
        if (!prop.Matched)
        {
            LogWarning("Could not find property named {0}");
        }

        if (!error)
        {
            SetSetting(prop.Value, value);
        }


    }

    /// <summary>
    /// The thread on which the live view images will get downloaded continuously
    /// </summary>
    private Thread LVThread;
    /// <summary>
    /// If true, the live view will be shut off completely. If false, live view will go back to the camera.
    /// </summary>
    private bool LVoff;

    public void DumpAllProperties()
    {
        var t = LogInfoAsync("=========Dumping properties=========");

        foreach (var prop in SDKProperties)
        {
            uint value = GetSetting(prop.Value);

            t = LogInfoAsync("Property: {SDKProperty}, Value: {SDKPropertyValue}", prop.Name, "0x" + value.ToString("X"));
        }
    }

    #endregion
    #region Events

    #region SDK Events

    public event EdsCameraAddedHandler SDKCameraAddedEvent;
    public event EdsObjectEventHandler SDKObjectEvent;
    public event EdsProgressCallback SDKProgressCallbackEvent;
    public event EdsPropertyEventHandler SDKPropertyEvent;
    public event EdsStateEventHandler SDKStateEvent;

    #endregion

    #region Custom Events

    public delegate void CameraAddedHandler();
    public delegate void ProgressHandler(int Progress);
    public delegate void StreamUpdate(Stream img);
    public delegate void BitmapUpdate(Bitmap bmp);

    /// <summary>
    /// Fires if a camera is added
    /// </summary>
    public event CameraAddedHandler CameraAdded;
    /// <summary>
    /// Fires if any process reports progress
    /// </summary>
    public event ProgressHandler ProgressChanged;
    /// <summary>
    /// Fires if the live view image has been updated
    /// </summary>
    public event StreamUpdate LiveViewUpdated;
    /// <summary>
    /// If the camera is disconnected or shuts down, this event is fired
    /// </summary>
    public event EventHandler CameraHasShutdown;
    /// <summary>
    /// If an image is downloaded, this event fires with the downloaded image.
    /// </summary>
    public event BitmapUpdate ImageDownloaded;

    #endregion

    #endregion
    #region Basic SDK and Session handling

    public SDKProperty SDKObjectEventToProperty(uint objectEvent)
    {
        return FindProperty(SDKObjectEvents, objectEvent);
    }

    private SDKProperty[] _sDKObjectEvents;
    public SDKProperty[] SDKObjectEvents => _sDKObjectEvents;

    public SDKProperty SDKErrorToProperty(uint error)
    {
        return FindProperty(SDKErrors, error);
    }

    private SDKProperty[] _sDKErrors;
    public SDKProperty[] SDKErrors => _sDKErrors;

    private SDKProperty FindProperty(SDKProperty[] properties, string property)
    {
        var search = properties.FirstOrDefault(p => p.Name == property);
        if (search == null)
        {
            search = new SDKProperty(property, 0, false);
        }
        return search;
    }


    private SDKProperty FindProperty(SDKProperty[] properties, uint property)
    {
        var search = properties.FirstOrDefault(p => p.Value == property);
        if (search == null)
        {
            search = new SDKProperty("UNKNOWN", property, false);
        }
        return search;
    }

    public void SetSaveToHost()
    {
        SetSetting(EDSDKLib.EDSDK.PropID_SaveTo, (uint)EDSDKLib.EDSDK.EdsSaveTo.Host);
    }

    public SDKProperty GetStateEvent(uint stateEvent)
    {
        return FindProperty(SDKStateEvents, stateEvent);
    }

    public SDKProperty GetSDKProperty(string property)
    {
        return FindProperty(SDKProperties, property);
    }

    public SDKProperty GetSDKProperty(uint property)
    {
        return FindProperty(SDKProperties, property);
    }

    private SDKProperty[] _sDKProperties;
    public SDKProperty[] SDKProperties => _sDKProperties;

    private SDKProperty[] _sDKStateEvents;
    public SDKProperty[] SDKStateEvents => _sDKStateEvents;

    public object Value { get; private set; }
    public bool KeepAlive { get; set; }


    /// <summary>
    /// Initializes the SDK and adds events
    /// </summary>
    public SDKHandler(ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger<SDKHandler>();

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
            Error = EdsInitializeSDK();

        }
        catch (Exception x)
        {
            logger.LogError(x, "Error initialising SDK");
            throw new Exception("Error initialising SDK", x);
            //TODO: Move to Initialise pattern instead of constructor
        }
        STAThread.Init();

        //subscribe to camera added event (the C# event and the SDK event)
        SDKCameraAddedEvent += new EdsCameraAddedHandler(SDKHandler_CameraAddedEvent);
        AddCameraHandler(() => EdsSetCameraAddedHandler(SDKCameraAddedEvent, 0), nameof(EdsSetCameraAddedHandler));

        //subscribe to the camera events (for the C# events)
        SDKStateEvent += new EdsStateEventHandler(Camera_SDKStateEvent);
        SDKPropertyEvent += new EdsPropertyEventHandler(Camera_SDKPropertyEvent);
        SDKProgressCallbackEvent += new EdsProgressCallback(Camera_SDKProgressCallbackEvent);
        SDKObjectEvent += new EdsObjectEventHandler(Camera_SDKObjectEvent);

    }

    private void STAThread_FatalError(object sender, EventArgs e)
    {
        var args = new SdkErrorEventArgs() { Error = "Execution thread error", ErrorLevel = LogLevel.Critical };
        OnSdkError(args);
    }

    /// <summary>
    /// Call this once to initialize event listeners and thread management
    /// NOTE: Should be called from an STA thread
    /// </summary>
    private void PopulateSDKConstantStructures()
    {
        FieldInfo[] fields = typeof(EDSDKLib.EDSDK).GetFields(BindingFlags.Public | BindingFlags.Static);

        _sDKStateEvents = FilterFields(fields, "StateEvent_");
        _sDKObjectEvents = FilterFields(fields, "ObjectEvent_");
        _sDKErrors = FilterFields(fields, "EDS_ERR_");
        _sDKProperties = FilterFields(fields, "kEdsPropID_", "PropID_");
    }

    private static SDKProperty[] FilterFields(FieldInfo[] fields, string prefix, string prefix2 = null)
    {
        var filteredFields = from f in fields
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
        nint camlist;
        //get list of cameras
        Error = EdsGetCameraList(out camlist);

        //get each camera from camlist
        int c;
        //get amount of connected cameras
        Error = EdsGetChildCount(camlist, out c);
        List<Camera> camList = [];
        for (int i = 0; i < c; i++)
        {
            nint cptr;
            //get pointer to camera at index i
            Error = EdsGetChildAtIndex(camlist, i, out cptr);
            camList.Add(new Camera(cptr));
        }

        var t = LogInfoAsync("Found {CameraCount} cameras", camList.Count);

        return camList;
    }

    private async Task LogInfoAsync(string message, params object[] args)
    {
        await Log(LogLevel.Information, message, args);
    }

    /// <summary>
    /// Opens a session with given camera
    /// </summary>
    /// <param name="newCamera">The camera which will be used</param>
    public void OpenSession(Camera newCamera)
    {
        logger?.LogDebug("Opening session");

        if (CameraSessionOpen)
        {
            CloseSession();
        }

        if (newCamera != null)
        {
            MainCamera = newCamera;
            //open a session
            SendSDKCommand(delegate { Error = EdsOpenSession(MainCamera.Ref); }, sdkAction: nameof(EdsOpenSession));
            //subscribe to the camera events (for the SDK)
            AddCameraHandler(() => EdsSetCameraStateEventHandler(MainCamera.Ref, StateEvent_All, SDKStateEvent, MainCamera.Ref), nameof(EdsSetCameraStateEventHandler));
            AddCameraHandler(() => EdsSetObjectEventHandler(MainCamera.Ref, ObjectEvent_All, SDKObjectEvent, MainCamera.Ref), nameof(EdsSetObjectEventHandler));
            AddCameraHandler(() => EdsSetPropertyEventHandler(MainCamera.Ref, PropertyEvent_All, SDKPropertyEvent, MainCamera.Ref), nameof(EdsSetPropertyEventHandler));
            CameraSessionOpen = true;

            var t = LogInfoAsync("Connected to Camera: {CameraName}", newCamera.Info.szDeviceDescription);

        }
    }

    private void AddCameraHandler(Func<uint> action, string handlerName)
    {
        var t = LogInfoAsync("Adding handler: {SDKHandlerName}", handlerName);

        Error = action();
    }


    /// <summary>
    /// Closes the session with the current camera
    /// </summary>
    public void CloseSession()
    {
        logger?.LogDebug("Closing session");
        if (CameraSessionOpen)
        {
            //if live view is still on, stop it and wait till the thread has stopped
            if (IsLiveViewOn)
            {
                StopLiveView();
                LVThread.Join(1000);
            }

            //Remove the event handler
            EdsSetCameraStateEventHandler(MainCamera.Ref, StateEvent_All, null, MainCamera.Ref);
            EdsSetObjectEventHandler(MainCamera.Ref, ObjectEvent_All, null, MainCamera.Ref);
            EdsSetPropertyEventHandler(MainCamera.Ref, PropertyEvent_All, null, MainCamera.Ref);

            //close session and release camera
            SendSDKCommand(delegate { Error = EdsCloseSession(MainCamera.Ref); }, sdkAction: nameof(EdsCloseSession));
            uint c = EdsRelease(MainCamera.Ref);
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
    private uint SDKHandler_CameraAddedEvent(nint inContext)
    {
        //Handle new camera here
        OnCameraAdded();
        return EDS_ERR_OK;
    }

    protected void OnCameraAdded()
    {
        CameraAdded?.Invoke();
    }


    /// <summary>
    /// An Objectevent fired
    /// </summary>
    /// <param name="inEvent">The ObjectEvent id</param>
    /// <param name="inRef">Pointer to the object</param>
    /// <param name="inContext"></param>
    /// <returns>An EDSDK errorcode</returns>
    private uint Camera_SDKObjectEvent(uint inEvent, nint inRef, nint inContext)
    {
        var eventProperty = SDKObjectEventToProperty(inEvent);
        //LogInfo("SDK Object Event. Name: {SDKEventName}, Value: {SDKEventHex}", eventProperty.Name, eventProperty.ValueToString());

        //handle object event here
        switch (inEvent)
        {
            case ObjectEvent_All:
                break;
            case ObjectEvent_DirItemCancelTransferDT:
                break;
            case ObjectEvent_DirItemContentChanged:
                break;
            case ObjectEvent_DirItemCreated:
                if (DownloadVideo)
                {
                    DownloadImage(inRef, ImageSaveDirectory, ImageSaveFilename, isVideo: true);
                    DownloadVideo = false;
                }
                break;
            case ObjectEvent_DirItemInfoChanged:
                break;
            case ObjectEvent_DirItemRemoved:
                break;
            case ObjectEvent_DirItemRequestTransfer:
                DownloadImage(inRef, ImageSaveDirectory, ImageSaveFilename);
                break;
            case ObjectEvent_DirItemRequestTransferDT:
                break;
            case ObjectEvent_FolderUpdateItems:
                break;
            case ObjectEvent_VolumeAdded:
                break;
            case ObjectEvent_VolumeInfoChanged:
                break;
            case ObjectEvent_VolumeRemoved:
                break;
            case ObjectEvent_VolumeUpdateItems:
                break;
        }

        return EDS_ERR_OK;
    }

    /// <summary>
    /// A progress was made
    /// </summary>
    /// <param name="inPercent">Percent of progress</param>
    /// <param name="inContext">...</param>
    /// <param name="outCancel">Set true to cancel event</param>
    /// <returns>An EDSDK errorcode</returns>
    private uint Camera_SDKProgressCallbackEvent(uint inPercent, nint inContext, ref bool outCancel)
    {
        //Handle progress here
        OnProgressChanged((int)inPercent);
        return EDS_ERR_OK;
    }

    protected void OnProgressChanged(int percent)
    {
        ProgressChanged?.Invoke(percent);
    }

    /// <summary>
    /// A property changed
    /// </summary>
    /// <param name="inEvent">The PropertyEvent ID</param>
    /// <param name="inPropertyID">The Property ID</param>
    /// <param name="inParameter">Event Parameter</param>
    /// <param name="inContext">...</param>
    /// <returns>An EDSDK errorcode</returns>
    private uint Camera_SDKPropertyEvent(uint inEvent, uint inPropertyID, uint inParameter, nint inContext)
    {
        //Handle property event here
        switch (inEvent)
        {
            case PropertyEvent_All:
                break;
            case PropertyEvent_PropertyChanged:
                LogPropertyValue(inPropertyID, GetSetting(inPropertyID));
                break;

            case PropertyEvent_PropertyDescChanged:
                break;
        }

        switch (inPropertyID)
        {
            case PropID_Unknown:
            case PropID_ProductName:
            case PropID_BodyIDEx:
            case PropID_OwnerName:
            case PropID_MakerName:
            case PropID_DateTime:
            case PropID_FirmwareVersion:
            case PropID_BatteryLevel:
            case PropID_CFn:
            case PropID_SaveTo:
            case PropID_ImageQuality:
            case PropID_Orientation:
            case PropID_ICCProfile:
            case PropID_FocusInfo:
            case PropID_WhiteBalance:
            case PropID_ColorTemperature:
            case PropID_WhiteBalanceShift:
            case PropID_ColorSpace:
            case PropID_PictureStyle:
            case PropID_PictureStyleDesc:
            case PropID_PictureStyleCaption:
            case PropID_AEMode:
            case PropID_AEModeSelect:
            case PropID_DriveMode:
            case PropID_ISOSpeed:
            case PropID_MeteringMode:
            case PropID_AFMode:
            case PropID_Av:
            case PropID_Tv:
            case PropID_ExposureCompensation:
            case PropID_FocalLength:
            case PropID_AvailableShots:
            case PropID_Bracket:
            case PropID_WhiteBalanceBracket:
            case PropID_LensName:
            case PropID_AEBracket:
            case PropID_FEBracket:
            case PropID_ISOBracket:
            case PropID_NoiseReduction:
            case PropID_FlashOn:
            case PropID_RedEye:
            case PropID_FlashMode:
            case PropID_LensStatus:
            case PropID_Artist:
            case PropID_Copyright:
            case PropID_Evf_Mode:
            case PropID_Evf_WhiteBalance:
            case PropID_Evf_ColorTemperature:
            case PropID_Evf_DepthOfFieldPreview:
            case PropID_Evf_Zoom:
            case PropID_Evf_ZoomPosition:
            case PropID_Evf_ImagePosition:
            case PropID_Evf_HistogramStatus:
            case PropID_Evf_AFMode:
            case PropID_Evf_HistogramY:
            case PropID_Evf_HistogramR:
            case PropID_Evf_HistogramG:
            case PropID_Evf_HistogramB:
            case PropID_Evf_CoordinateSystem:
            case PropID_Evf_ZoomRect:
            case PropID_Record:
            case PropID_GPSVersionID:
            case PropID_GPSLatitudeRef:
            case PropID_GPSLatitude:
            case PropID_GPSLongitudeRef:
            case PropID_GPSLongitude:
            case PropID_GPSAltitudeRef:
            case PropID_GPSAltitude:
            case PropID_GPSTimeStamp:
            case PropID_GPSSatellites:
            case PropID_GPSStatus:
            case PropID_GPSMapDatum:
            case PropID_GPSDateStamp:
            case PropID_DC_Zoom:
            case PropID_DC_Strobe:
            case PropID_LensBarrelStatus:
            case PropID_TempStatus:
            case PropID_Evf_RollingPitching:
            case PropID_FixedMovie:
            case PropID_MovieParam:
            case PropID_Evf_ClickWBCoeffs:
            case PropID_ManualWhiteBalanceData:
            case PropID_MirrorUpSetting:
            case PropID_MirrorLockUpState:
            case PropID_UTCTime:
            case PropID_TimeZone:
            case PropID_SummerTimeSetting:
            case PropID_AutoPowerOffSetting:
                break;
            case PropID_Evf_OutputDevice:
                if (IsLiveViewOn == true)
                {
                    DownloadEvf();
                }

                break;
        }
        return EDS_ERR_OK;
    }

    public void LogPropertyValue(string propertyName, uint propertyValue)
    {
        var task = LogInfoAsync($"Camera_SDKPropertyEvent. Property {propertyName} changed to {"0x" + propertyValue.ToString("X")}");
    }


    public void LogPropertyValue(uint propertyID, uint propertyValue)
    {
        var prop = GetSDKProperty(propertyID);
        LogPropertyValue(prop.Name, propertyValue);
        //do nothing with task, continue
    }

    /// <summary>
    /// The camera state changed
    /// </summary>
    /// <param name="inEvent">The StateEvent ID</param>
    /// <param name="inParameter">Parameter from this event</param>
    /// <param name="inContext">...</param>
    /// <returns>An EDSDK errorcode</returns>
    private uint Camera_SDKStateEvent(uint inEvent, uint inParameter, nint inContext)
    {

        var stateProperty = GetStateEvent(inEvent);

        var t = LogInfoAsync("SDK State Event. Name: {SDKStateEventName}, Hex {SDKStateEventHex}", stateProperty.Name, stateProperty.ValueToString());

        //Handle state event here
        switch (inEvent)
        {
            case StateEvent_All:
            case StateEvent_JobStatusChanged:
            case StateEvent_ShutDownTimerUpdate:
            case StateEvent_CaptureError:
                LogError("Error event. error: {error}", nameof(StateEvent_CaptureError));
                break;
            case StateEvent_InternalError:
                LogError("Error event. error: {error}", nameof(StateEvent_InternalError));
                break;
            case StateEvent_Shutdown:
                CameraSessionOpen = false;
                if (IsLiveViewOn)
                {
                    StopLiveView();
                    // Not supported in .NET Core. Transition to cancellation token LVThread.Abort();
                }
                OnCameraHasShutdown();
                break;
            case StateEvent_WillSoonShutDown:
                if (KeepAlive)
                {
                    SendSDKCommand(() =>
                    {
                        logger.LogDebug("Extending camera shutdown timer");
                        EdsSendCommand(MainCamera.Ref, CameraCommand_ExtendShutDownTimer, 0);
                    }, sdkAction: nameof(CameraCommand_ExtendShutDownTimer));
                }
                break;
        }
        return EDS_ERR_OK;
    }

    protected void OnCameraHasShutdown()
    {
        CameraHasShutdown?.Invoke(this, new EventArgs());
    }

    protected void OnSdkError(SdkErrorEventArgs e)
    {
        SdkError?.Invoke(this, e);
    }

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
            EdsDirectoryItemInfo dirInfo;
            nint streamRef;
            //get information about object
            Error = EdsGetDirectoryItemInfo(ObjectPointer, out dirInfo);
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
                    fileName = targetInfo.Name[..^targetInfo.Extension.Length] + cameraInfo.Extension;
                }
            }

            LogInfoAsync("Downloading data. Filename: {Filename}", fileName);

            string targetImage = Path.Combine(directory, fileName);
            if (File.Exists(targetImage))
            {
                throw new NotImplementedException("Renaming files not permitted");
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }


            var t = LogInfoAsync("Downloading data {Filename} to {SaveDirectory}", fileName, directory);

            SendSDKCommand(delegate
            {

                var stopWatch = Stopwatch.StartNew();

                //create filestream to data
                Error = EdsCreateFileStream(targetImage, EdsFileCreateDisposition.CreateAlways, EdsAccess.ReadWrite, out streamRef);
                //download file
                STAThread.TryLockAndExecute(STAThread.ExecLock, nameof(STAThread.ExecLock), TimeSpan.FromSeconds(30), () => DownloadData(ObjectPointer, streamRef));
                //release stream
                Error = EdsRelease(streamRef);

                stopWatch.Stop();

                var downloadFile = new FileInfo(targetImage);
                var mB = downloadFile.Length / 1000.0 / 1000;

                t = LogInfoAsync("Downloaded data. Filename: {Filename}, FileLengthMB: {FileLengthMB}, DurationSeconds: {DurationSeconds}, MBPerSecond: {MBPerSecond}", targetImage, mB.ToString("0.0"), stopWatch.Elapsed.TotalSeconds.ToString("0.0"), (mB / stopWatch.Elapsed.TotalSeconds).ToString("0.0"));

                if (isVideo)
                {
                    videoDownloadDone?.TrySetResult(targetImage);
                }
                else
                {
                    takePhotoCompletionSource?.TrySetResult(new FileInfo(fileName));
                }

            }, true);
        }
        catch (Exception x)
        {
            logger.LogError(x, "Error downloading data");
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
        Error = EdsGetDirectoryItemInfo(ObjectPointer, out dirInfo);

        //check the extension. Raw data cannot be read by the bitmap class
        string ext = Path.GetExtension(dirInfo.szFileName).ToLower();

        var t = LogInfoAsync("Downloading image {ImageFileName}", dirInfo.szFileName);


        if (ext == ".jpg" || ext == ".jpeg")
        {
            SendSDKCommand(delegate
            {
                Bitmap bmp = null;
                nint streamRef, jpgPointer = 0;
                ulong length = 0;

                //create memory stream
                Error = EdsCreateMemoryStream(dirInfo.Size, out streamRef);

                //download data to the stream
                lock (STAThread.ExecLock) { DownloadData(ObjectPointer, streamRef); }
                Error = EdsGetPointer(streamRef, out jpgPointer);
                Error = EdsGetLength(streamRef, out length);

                unsafe
                {
                    //create a System.IO.Stream from the pointer
                    using UnmanagedMemoryStream ums = new((byte*)jpgPointer.ToPointer(), (long)length, (long)length, FileAccess.Read);
                    //create bitmap from stream (it's a normal jpeg image)
                    bmp = new Bitmap(ums);
                }

                //release data
                Error = EdsRelease(streamRef);

                //Fire the event with the image
                OnImageDownloaded(bmp);
            }, true);
        }
        else
        {
            //if it's a RAW image, cancel the download and release the image
            SendSDKCommand(delegate { Error = EdsDownloadCancel(ObjectPointer); });
            Error = EdsRelease(ObjectPointer);
        }
    }

    protected void OnImageDownloaded(Bitmap bitmap)
    {
        ImageDownloaded?.Invoke(bitmap);
    }

    /// <summary>
    /// Gets the thumbnail of an image (can be raw or jpg)
    /// </summary>
    /// <param name="filepath">The filename of the image</param>
    /// <returns>The thumbnail of the image</returns>
    public Bitmap GetFileThumb(string filepath)
    {
        nint stream;
        //create a filestream to given file
        Error = EdsCreateFileStream(filepath, EdsFileCreateDisposition.OpenExisting, EdsAccess.Read, out stream);
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
        EdsDirectoryItemInfo dirInfo;
        Error = EdsGetDirectoryItemInfo(ObjectPointer, out dirInfo);

        try
        {
            //set progress event
            Error = EdsSetProgressCallback(stream, SDKProgressCallbackEvent, EdsProgressOption.Periodically, ObjectPointer);
            //download the data
            Error = EdsDownload(ObjectPointer, dirInfo.Size, stream);
        }
        finally
        {
            //set the download as complete
            Error = EdsDownloadComplete(ObjectPointer);
            //release object
            Error = EdsRelease(ObjectPointer);
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
        EdsImageInfo imageInfo;

        try
        {
            //create reference and get image info
            Error = EdsCreateImageRef(img_stream, out img_ref);
            Error = EdsGetImageInfo(img_ref, imageSource, out imageInfo);

            EdsSize outputSize = new()
            {
                width = imageInfo.EffectiveRect.width,
                height = imageInfo.EffectiveRect.height
            };
            //calculate amount of data
            int datalength = outputSize.height * outputSize.width * 3;
            //create buffer that stores the image
            byte[] buffer = new byte[datalength];
            //create a stream to the buffer

            nint ptr = new();
            Marshal.StructureToPtr<byte[]>(buffer, ptr, false);


            Error = EdsCreateMemoryStreamFromPointer(ptr, (uint)datalength, out stream);
            //load image into the buffer
            Error = EdsGetImage(img_ref, imageSource, EdsTargetImageType.RGB, imageInfo.EffectiveRect, outputSize, stream);

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
            //Release all data
            if (img_stream != 0)
            {
                EdsRelease(img_stream);
            }

            if (img_ref != 0)
            {
                EdsRelease(img_ref);
            }

            if (stream != 0)
            {
                EdsRelease(stream);
            }
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
        if (MainCamera.Ref != 0)
        {
            //a list of settings can only be retrieved for following properties
            if (PropID == PropID_AEModeSelect || PropID == PropID_ISOSpeed || PropID == PropID_Av
                || PropID == PropID_Tv || PropID == PropID_MeteringMode || PropID == PropID_ExposureCompensation)
            {
                //get the list of possible values
                EdsPropertyDesc des = new();
                Error = EdsGetPropertyDesc(MainCamera.Ref, PropID, out des);
                return des.PropDesc.Take(des.NumElements).ToList();
            }
            else
            {
                throw new ArgumentException("Method cannot be used with this Property ID");
            }
        }
        else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
    }

    /// <summary>
    /// Gets the current setting of given property ID as an uint
    /// </summary>
    /// <param name="PropID">The property ID</param>
    /// <returns>The current setting of the camera</returns>
    public uint GetSetting(uint PropID)
    {
        if (MainCamera.Ref != 0)
        {
            uint property = 0;
            Error = EdsGetPropertyData(MainCamera.Ref, PropID, 0, out property);
            return property;
        }
        else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
    }

    /// <summary>
    /// Gets the current setting of given property ID as a string
    /// </summary>
    /// <param name="PropID">The property ID</param>
    /// <returns>The current setting of the camera</returns>
    public string GetStringSetting(uint PropID)
    {
        if (MainCamera.Ref != 0)
        {
            string data = String.Empty;
            EdsGetPropertyData(MainCamera.Ref, PropID, 0, out data);
            return data;
        }
        else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
    }

    /// <summary>
    /// Gets the current setting of given property ID as a struct
    /// </summary>
    /// <param name="PropID">The property ID</param>
    /// <typeparam name="T">One of the EDSDK structs</typeparam>
    /// <returns>The current setting of the camera</returns>
    public T GetStructSetting<T>(uint PropID) where T : struct
    {
        if (MainCamera.Ref != 0)
        {
            //get type and size of struct
            Type structureType = typeof(T);
            int bufferSize = Marshal.SizeOf(structureType);

            //allocate memory
            nint ptr = Marshal.AllocHGlobal(bufferSize);
            //retrieve value
            Error = EdsGetPropertyData(MainCamera.Ref, PropID, 0, bufferSize, ptr);

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
        else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
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
        LogSetProperty(propertyId, "0x" + value.ToString("X"));
        if (MainCamera.Ref != 0)
        {
            SendSDKCommand(delegate
            {
                var cThread = Thread.CurrentThread;
                var t = LogInfoAsync("Executing SDK command. ThreadName: {ThreadName}, ApartmentState: {ApartmentState}", cThread.Name, cThread.GetApartmentState());
                int propsize;
                EdsDataType proptype;
                //get size of property
                Error = EdsGetPropertySize(MainCamera.Ref, propertyId, 0, out proptype, out propsize);
                //set given property
                Error = EdsSetPropertyData(MainCamera.Ref, propertyId, 0, propsize, value);
            }, sdkAction: nameof(EdsSetPropertyData));
        }
        else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
    }

    /// <summary>
    /// Sends a camera command
    /// </summary>
    /// <param name="propertyId">The property ID</param>
    /// <param name="value">The value which will be set</param>
    public void SendCommand(uint commandId, int value)
    {
        Log(LogLevel.Debug, "Sending command. CommandId: {CommandId}, Value: {Value}", $"0x{commandId:X}", $"0x{value:X}").RunSynchronously();
        if (MainCamera.Ref != 0)
        {

            SendSDKCommand(delegate
            {
                var cThread = Thread.CurrentThread;
                var t = LogInfoAsync("Executing SDK command. ThreadName: {ThreadName}, ApartmentState: {ApartmentState}", cThread.Name, cThread.GetApartmentState());
                Error = EdsSendCommand(MainCamera.Ref, commandId, value);
            }, sdkAction: nameof(EdsSetPropertyData));
        }
        else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
    }

    /// <summary>
    /// Sets a DateTime value for the given property ID
    /// </summary>
    /// <param name="PropID">The property ID</param>
    /// <param name="Value">The value which will be set</param>
    public void SetDateTimeSetting(uint propertyId, DateTime value)
    {
        EDSDKLib.EDSDK.EdsTime dateTime = new()
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

    private void LogSetProperty(uint propertyId, string value)
    {
        var prop = GetSDKProperty(propertyId);
        var t = LogInfoAsync("Setting property. Name: {SDKPropertyName}, Id: {SDKPropertyHex}, Value: {SDKPropertyValue}", prop.Name, prop.Value, value);
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
        if (MainCamera.Ref != 0)
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
            SendSDKCommand(delegate { Error = EdsSetPropertyData(MainCamera.Ref, propertyId, 0, 32, propertyValueBytes); }, sdkAction: nameof(EdsSetPropertyData));
        }
        else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
    }

    /// <summary>
    /// Sets a struct value for the given property ID
    /// </summary>
    /// <param name="propertyId">The property ID</param>
    /// <param name="value">The value which will be set</param>
    public void SetStructSetting<T>(uint propertyId, T value) where T : struct
    {
        LogSetProperty(propertyId, value.ToString());
        if (MainCamera.Ref != 0)
        {
            SendSDKCommand(delegate { Error = EdsSetPropertyData(MainCamera.Ref, propertyId, 0, Marshal.SizeOf(typeof(T)), value); }, sdkAction: nameof(EdsSetPropertyData));
        }
        else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
    }

    #endregion
    #region Live view

    /// <summary>
    /// Starts the live view
    /// </summary>
    public void StartLiveView()
    {
        if (!IsLiveViewOn)
        {
            var t = LogInfoAsync("Starting Liveview");

            t = LiveViewUpdated != null
                ? LogInfoAsync("{LiveViewUpdatedEventListeners} LiveViewUpdated listeners found", LiveViewUpdated.GetInvocationList().Length)
                : LogInfoAsync("{LiveViewUpdatedEventListeners} LiveViewUpdated listeners found", 0);


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
            var t = LogInfoAsync("Stopping liveview");
            LVoff = true;
            IsLiveViewOn = false;

            //Wait 5 seconds for evf thread to finish, otherwise manually stop
            if (!cancelLiveViewWait.WaitOne(TimeSpan.FromSeconds(5)))
            {
                KillLiveView();
            }
            else
            {
                logger.LogDebug("LiveView stopped cleanly");
            }
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
                nint jpgPointer;
                nint stream = 0;
                nint EvfImageRef = 0;
                UnmanagedMemoryStream ums;

                uint err;
                ulong length;
                //create stream
                Error = EdsCreateMemoryStream(0, out stream);

                //run live view
                while (IsLiveViewOn)
                {
                    lock (STAThread.ExecLock)
                    {
                        //download current live view image
                        err = EdsCreateEvfImageRef(stream, out EvfImageRef);
                        if (err == EDS_ERR_OK)
                        {
                            err = EdsDownloadEvfImage(MainCamera.Ref, EvfImageRef);
                        }

                        if (err == EDS_ERR_OBJECT_NOTREADY) { Thread.Sleep(4); continue; }
                        else
                        {
                            Error = err;
                        }
                    }

                    //get pointer
                    Error = EdsGetPointer(stream, out jpgPointer);
                    Error = EdsGetLength(stream, out length);

                    //get some live view image metadata
                    if (!IsCoordSystemSet) { Evf_CoordinateSystem = GetEvfCoord(EvfImageRef); IsCoordSystemSet = true; }
                    Evf_ZoomRect = GetEvfZoomRect(EvfImageRef);
                    Evf_ZoomPosition = GetEvfPoints(PropID_Evf_ZoomPosition, EvfImageRef);
                    Evf_ImagePosition = GetEvfPoints(PropID_Evf_ImagePosition, EvfImageRef);

                    //release current evf image
                    if (EvfImageRef != 0) { Error = EdsRelease(EvfImageRef); }

                    //create stream to image
                    unsafe { ums = new UnmanagedMemoryStream((byte*)jpgPointer.ToPointer(), (long)length, (long)length, FileAccess.Read); }

                    //fire the LiveViewUpdated event with the live view image stream
                    OnLiveViewUpdated(ums);
                    ums.Close();
                }

                //release and finish
                if (stream != 0)
                {
                    Error = EdsRelease(stream);
                }
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
        var t = LogInfoAsync("Killing LiveView");
        //stop the live view
        SetSetting(PropID_Evf_OutputDevice, LVoff ? 0 : EvfOutputDevice_TFT);
    }


    /// <summary>
    /// Fires the LiveViewUpdated event
    /// </summary>
    /// <param name="stream"></param>
    protected void OnLiveViewUpdated(UnmanagedMemoryStream stream)
    {
        LiveViewUpdated?.Invoke(stream);
    }


    /// <summary>
    /// Get the live view ZoomRect value
    /// </summary>
    /// <param name="imgRef">The live view reference</param>
    /// <returns>ZoomRect value</returns>
    private EdsRect GetEvfZoomRect(nint imgRef)
    {
        int size = Marshal.SizeOf(typeof(EdsRect));
        nint ptr = Marshal.AllocHGlobal(size);

        uint err = EdsGetPropertyData(imgRef, PropID_Evf_ZoomPosition, 0, size, ptr);
        EdsRect rect = (EdsRect)Marshal.PtrToStructure(ptr, typeof(EdsRect));
        Marshal.FreeHGlobal(ptr);
        return err == EDS_ERR_OK ? rect : new EdsRect();
    }

    /// <summary>
    /// Get the live view coordinate system
    /// </summary>
    /// <param name="imgRef">The live view reference</param>
    /// <returns>the live view coordinate system</returns>
    private EdsSize GetEvfCoord(nint imgRef)
    {
        int size = Marshal.SizeOf(typeof(EdsSize));
        nint ptr = Marshal.AllocHGlobal(size);
        uint err = EdsGetPropertyData(imgRef, PropID_Evf_CoordinateSystem, 0, size, ptr);
        EdsSize coord = (EdsSize)Marshal.PtrToStructure(ptr, typeof(EdsSize));
        Marshal.FreeHGlobal(ptr);
        return err == EDS_ERR_OK ? coord : new EdsSize();
    }

    /// <summary>
    /// Get a live view EdsPoint value
    /// </summary>
    /// <param name="imgRef">The live view reference</param>
    /// <returns>a live view EdsPoint value</returns>
    private EdsPoint GetEvfPoints(uint PropID, nint imgRef)
    {
        int size = Marshal.SizeOf(typeof(EdsPoint));
        nint ptr = Marshal.AllocHGlobal(size);
        uint err = EdsGetPropertyData(imgRef, PropID, 0, size, ptr);
        EdsPoint data = (EdsPoint)Marshal.PtrToStructure(ptr, typeof(EdsPoint));
        Marshal.FreeHGlobal(ptr);
        return err == EDS_ERR_OK ? data : new EdsPoint();
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

    public void SetTFTEvf()
    {
        SetSetting(PropID_Evf_OutputDevice, EvfOutputDevice_TFT);
    }

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
            var recordStatus = GetSetting(PropID_Record);
            if (recordStatus != (uint)PropID_Record_Status.Movie_shooting_ready)
            {
                //DOES NOT WORK, readonly setting?
                //DOES NOT THROW AN ERROR
                //SetSetting(PropID_Record, (uint)EdsDriveMode.Video);
                //SetSetting(PropID_Record, (uint)PropID_Record_Status.Movie_shooting_ready);


                LogPropertyValue(PropID_Record, recordStatus);
                var tx = Log(LogLevel.Information, "Camera reporting incorrect mode. expected. Continue. {expected}, was: {was}", PropID_Record_Status.Movie_shooting_ready, recordStatus);
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

            var t = LogInfoAsync("Start filming");

            SendSDKCommand(delegate { Error = EdsSetPropertyData(MainCamera.Ref, PropID_Record, 0, sizeof(PropID_Record_Status), (uint)PropID_Record_Status.Begin_movie_shooting); });
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
                Error = EdsSetPropertyData(MainCamera.Ref, PropID_Record, 0, sizeof(PropID_Record_Status), (uint)PropID_Record_Status.End_movie_shooting);
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
    public void PressShutterButton(EdsShutterButton state)
    {
        //start thread to not block everything
        SendSDKCommand(delegate
        {
            //send command to camera
            lock (STAThread.ExecLock) { Error = EdsSendCommand(MainCamera.Ref, CameraCommand_PressShutterButton, (int)state); };
        }, true);
    }

    private TaskCompletionSource<FileInfo> takePhotoCompletionSource;
    private string _imageSaveFilename;

    /// <summary>
    /// Takes a photo and returns the file info
    /// </summary>
    /// <returns></returns>
    public async Task<FileInfo> TakePhotoAsync(FileInfo saveFile)
    {
        return await Task.Run<FileInfo>(async () =>
                                                                          {
                                                                              if (IsFilming || IsLiveViewOn)
                                                                              {
                                                                                  logger.LogWarning("Ignoring attempt to take photo whilst filming or in live-view mode. Filming: {Filming}, LiveView: {LiveView}", IsFilming, IsLiveViewOn);
                                                                                  return null;
                                                                              }

                                                                              takePhotoCompletionSource = new TaskCompletionSource<FileInfo>();
                                                                              SetSaveToLocation(saveFile.Directory);
                                                                              ImageSaveFilename = saveFile.Name;

                                                                              TakePhoto();

                                                                              await takePhotoCompletionSource.Task;
                                                                              if (takePhotoCompletionSource.Task.Status == TaskStatus.RanToCompletion)
                                                                              {
                                                                                  return takePhotoCompletionSource.Task.Result;
                                                                              }
                                                                              else
                                                                              {
                                                                                  LogError("Error taking photo, check previous messages");
                                                                                  return null;
                                                                              }
                                                                          });
    }

    private async Task Log(LogLevel level, string message, params object[] args)
    {
        await Task.Run(() =>
                                                                                         {

                                                                                             if (logger != null)
                                                                                             {
                                                                                                 switch (level)
                                                                                                 {
                                                                                                     case LogLevel.Trace:
                                                                                                         logger.LogTrace(message, args);
                                                                                                         break;

                                                                                                     case LogLevel.Debug:
                                                                                                         logger.LogDebug(message, args);
                                                                                                         break;

                                                                                                     case LogLevel.Information:
                                                                                                         logger.LogInformation(message, args);
                                                                                                         break;

                                                                                                     case LogLevel.Warning:
                                                                                                         logger.LogWarning(message, args);
                                                                                                         break;

                                                                                                     case LogLevel.Critical:
                                                                                                         logger.LogCritical(message, args);
                                                                                                         break;

                                                                                                     case LogLevel.None:
                                                                                                         // breakpoint only
                                                                                                         break;

                                                                                                     case LogLevel.Error:
                                                                                                         logger.LogError(message, args);
                                                                                                         break;

                                                                                                     default:
                                                                                                         logger.LogError("Unknown level: {0}{1}Message: {2}", level, Environment.NewLine, string.Format(message, args));
                                                                                                         break;
                                                                                                 }
                                                                                             }

                                                                                             if (level >= LogLevel.Error)
                                                                                             {
                                                                                                 // throw new Exception(string.Format(message, args));
                                                                                             }
                                                                                         });
    }

    private void HandleException(Exception ex, string message, params object[] args)
    {
        if (logger != null)
        {
            logger.LogError(ex, message, args);
        }
    }

    private void LogWarning(string message, params object[] args)
    {
        var t = Log(LogLevel.Warning, message, args);
    }

    private void LogError(string message, params object[] args)
    {
        var t = Log(LogLevel.Error, message, args);
    }


    public void SetSaveToLocation(DirectoryInfo directory)
    {
        SetSaveToHost();
        ImageSaveDirectory = directory.FullName;
    }

    /// <summary>
    /// Takes a photo with the current camera settings
    /// </summary>
    public void TakePhoto()
    {
        //start thread to not block everything
        SendSDKCommand(delegate
        {
            //send command to camera
            lock (STAThread.ExecLock) { Error = EdsSendCommand(MainCamera.Ref, CameraCommand_TakePicture, 0); };
        }, true);
    }

    /// <summary>
    /// Takes a photo in bulb mode with the current camera settings
    /// </summary>
    /// <param name="bulbTime">The time in milliseconds for how long the shutter will be open</param>
    public void TakePhoto(uint bulbTime)
    {
        //bulbtime has to be at least a second
        if (bulbTime < 1000) { throw new ArgumentException("Bulbtime has to be bigger than 1000ms"); }

        //start thread to not block everything
        SendSDKCommand(delegate
        {
            //open the shutter
            lock (STAThread.ExecLock) { Error = EdsSendCommand(MainCamera.Ref, CameraCommand_BulbStart, 0); }
            //wait for the specified time
            Thread.Sleep((int)bulbTime);
            //close shutter
            lock (STAThread.ExecLock) { Error = EdsSendCommand(MainCamera.Ref, CameraCommand_BulbEnd, 0); }
        }, true);
    }


    public void FormatAllVolumes()
    {
        RunForEachVolume((childReference, volumeInfo) =>
                                           {
                                               logger.LogInformation("Formatting volume. Volume: {Volume}", volumeInfo.szVolumeLabel);
                                               SendSDKCommand(() => Error = EdsFormatVolume(childReference));
                                           });
    }

    public float GetMinVolumeSpacePercent()
    {
        var minPercent = 1f;
        RunForEachVolume((childReference, volumeInfo) =>
        {
            var freePc = volumeInfo.FreeSpaceInBytes / (float)volumeInfo.MaxCapacity;
            logger.LogDebug("Camera volume free space. volume: {volume}, freeSpaceBytes: {freeSpaceBytes}, maxCapacity: {maxCapacity}, freePercent: {freePercent}", volumeInfo.szVolumeLabel, volumeInfo.FreeSpaceInBytes, volumeInfo.MaxCapacity, freePc);
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
        int VolumeCount;
        Error = EdsGetChildCount(GetCamera().Reference, out VolumeCount);

        for (int i = 0; i < VolumeCount; i++)
        {
            //get information about volume
            nint childReference;
            Error = EdsGetChildAtIndex(MainCamera.Ref, i, out childReference);
            EdsVolumeInfo volumeInfo = new();
            SendSDKCommand(delegate { Error = EdsGetVolumeInfo(childReference, out volumeInfo); });

            if (volumeInfo.StorageType != (uint)EdsStorageType.Non && volumeInfo.Access == (uint)EdsAccess.ReadWrite)
            {
                action(childReference, volumeInfo);
            }
            Error = EdsRelease(childReference);
        }
    }


    public void FormatVolume(CameraFileEntry volume)
    {
        throw new NotImplementedException();
        // NOTE: Need to marry up obj ref to camera entry then delete based on camera entry / ref
        logger.LogDebug("Formatting volume. Volume: {Volume}", volume.Name);
        SendSDKCommand(() =>
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(volume.Volume));
            try
            {
                Marshal.StructureToPtr(volume.Volume, ptr, false);
                Error = EdsFormatVolume(ptr);

            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }, sdkAction: nameof(EdsFormatVolume)
        );
    }


    #endregion

    #region Other

    /// <summary>
    /// Sends a command to the camera safely
    /// </summary>
    private void SendSDKCommand(Action command, bool longTask = false, string sdkAction = null)
    {
        if (sdkAction != null)
        {
            var t = LogInfoAsync("Sending SDK command: {SDKCommand}", sdkAction);
        }


        try
        {
            if (longTask)
            {
                STAThread.Create(command).Start();
            }
            else
            {
                STAThread.ExecuteSafely(command);
            }

        }
        catch (Exception ex)
        {
            HandleException(ex, "Error sending command: {0}", sdkAction);
        }
    }

    /// <summary>
    /// Tells the camera that there is enough space on the HDD if SaveTo is set to Host
    /// This method does not use the actual free space!
    /// </summary>
    public void SetCapacity()
    {
        SetCapacity(0x1000, 0x7FFFFFFF);
    }

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
        SendSDKCommand(delegate { Error = EdsSetCapacity(MainCamera.Ref, capacity); });
    }




    /// <summary>
    /// Moves the focus (only works while in live view)
    /// </summary>
    /// <param name="speed">Speed and direction of focus movement</param>
    public void SetFocus(uint speed)
    {
        if (IsLiveViewOn)
        {
            SendSDKCommand(delegate { Error = EdsSendCommand(MainCamera.Ref, CameraCommand_DriveLensEvf, (int)speed); });
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
            SendSDKCommand(delegate { Error = EdsSendCommand(MainCamera.Ref, CameraCommand_DoClickWBEvf, (int)coord); });
        }
    }

    public List<CameraFileEntry> GetVolumes()
    {
        return GetVolumes(GetCamera());
    }

    public List<CameraFileEntry> GetVolumes(CameraFileEntry camera)
    {
        //get the number of volumes currently installed in the camera
        int VolumeCount;
        Error = EdsGetChildCount(camera.Reference, out VolumeCount);
        List<CameraFileEntry> volumes = [];

        //iterate through all of them
        for (int i = 0; i < VolumeCount; i++)
        {
            //get information about volume
            nint childReference;
            Error = EdsGetChildAtIndex(MainCamera.Ref, i, out childReference);
            EdsVolumeInfo volumeInfo = new();
            SendSDKCommand(delegate { Error = EdsGetVolumeInfo(childReference, out volumeInfo); });
            //ignore the HDD
            if (volumeInfo.szVolumeLabel != "HDD")
            {
                //add volume to the list
                volumes.Add(new CameraFileEntry("Volume" + i + "(" + volumeInfo.szVolumeLabel + ")", CameraFileEntryTypes.Volume, childReference) { Volume = volumeInfo });
            }
            //release the volume
            Error = EdsRelease(childReference);
        }
        return volumes;
    }

    public CameraFileEntry GetCamera()
    {
        return new("Camera", CameraFileEntryTypes.Camera, MainCamera.Ref);
    }

    public void DeleteFileItem(CameraFileEntry fileItem)
    {
        throw new NotImplementedException();
        // NOTE: Get original structure from camera to delete
        SendSDKCommand(() =>
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(fileItem));
            try
            {
                Marshal.StructureToPtr(fileItem, ptr, false);
                Error = EdsDeleteDirectoryItem(ptr);

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

        volumes.ForEach(v => v.AddSubEntries(GetChildren(v.Reference)));

        //add all volumes to the main entry and return it
        camera.AddSubEntries(volumes.ToArray());
        return camera;
    }

    /// <summary>
    /// Locks or unlocks the cameras UI
    /// </summary>
    /// <param name="lockState">True for locked, false to unlock</param>
    public void UILock(bool lockState)
    {
        SendSDKCommand(delegate
                                               {
                                                   Error = lockState == true
                                                       ? EdsSendStatusCommand(MainCamera.Ref, CameraState_UILock, 0)
                                                       : EdsSendStatusCommand(MainCamera.Ref, CameraState_UIUnLock, 0);
                                               });
    }

    /// <summary>
    /// Gets the children of a camera folder/volume. Recursive method.
    /// </summary>
    /// <param name="ptr">Pointer to volume or folder</param>
    /// <returns></returns>
    private CameraFileEntry[] GetChildren(nint ptr)
    {
        int childCount;
        //get children of first pointer
        Error = EdsGetChildCount(ptr, out childCount);
        if (childCount > 0)
        {
            //if it has children, create an array of entries
            CameraFileEntry[] children = new CameraFileEntry[childCount];
            for (int i = 0; i < childCount; i++)
            {
                nint childReference;
                //get children of children
                Error = EdsGetChildAtIndex(ptr, i, out childReference);
                //get the information about this children
                EdsDirectoryItemInfo child = new();
                SendSDKCommand(delegate { Error = EdsGetDirectoryItemInfo(childReference, out child); });
                //create entry from information
                children[i] = new CameraFileEntry(child.szFileName, GetBool(child.isFolder) ? CameraFileEntryTypes.Folder : CameraFileEntryTypes.File, childReference);
                if (children[i].Type == CameraFileEntryTypes.File)
                {
                    if (false)
                    {
                        //if it's not a folder, create thumbnail and save it to the entry                       
                        nint stream;
                        Error = EdsCreateMemoryStream(0, out stream);
                        SendSDKCommand(delegate { Error = EdsDownloadThumbnail(childReference, stream); });
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
                //release current children
                EdsRelease(childReference);
            }
            return children;
        }
        else
        {
            return new CameraFileEntry[0];
        }
    }

    /// <summary>
    /// Converts an int to a bool
    /// </summary>
    /// <param name="val">Value</param>
    /// <returns>A bool created from the value</returns>
    private bool GetBool(int val)
    {
        return val != 0;
    }

    #endregion

    #endregion
}
