using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System.Drawing.Imaging;
using System.Drawing;
using System.Text;
using System.Linq;
using System.IO;
using System;

using EDSDK.Native;
using EDSDK.NET;

using Microsoft.Extensions.Logging;


namespace EDSDK.NET;


public sealed class SDKErrorEventArgs(string error, LogLevel level)
    : EventArgs
{
    public string Error { get; } = error;
    public LogLevel ErrorLevel { get; } = level;
}

/// <summary>
/// Handles the Canon SDK
/// </summary>
public sealed class SDKWrapper
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
    public SDKCamera? MainCamera { get; private set; }

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
                SDKProperty property = SDKProperty.FromSDKError(value) ?? SDKProperty.Unknown;

                LogError($"SDK Error. {property}");

                if (value is SDKError.COMM_DISCONNECTED or SDKError.DEVICE_INVALID or SDKError.DEVICE_NOT_FOUND)
                    OnSdkError(new SDKErrorEventArgs(property.Name, LogLevel.Critical));
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

        if (SDKProperty.FromName(propertyName) is SDKProperty prop)
        {
            if (!error)
                MainCamera?.Set(prop, value);
        }
        else
            LogWarning($"Could not find property named {propertyName}");
    }

    public void DumpAllProperties()
    {
        LogInfo("=========Dumping properties=========");

        foreach (SDKProperty prop in SDKProperties)
            LogInfo($"Property: {prop.Name}, Value: 0x{MainCamera?.Get(prop) ?? 0:X}");
    }

    #region Basic SDK and Session handling





    public SDKProperty[] SDKProperties { get; private set; }

    public SDKProperty[] SDKStateEvents { get; private set; }

    public object Value { get; private set; }

    public bool KeepAlive { get; set; }


    /// <summary>
    /// Initializes the SDK and adds events
    /// </summary>
    public SDKWrapper(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SDKWrapper>();

        STAThread.SetLogAction(loggerFactory.CreateLogger(nameof(STAThread)));
        STAThread.FatalError += STAThread_FatalError;

        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            LogWarning("SDKHandler created on a non-STA thread");

        try
        {
            Error = EDSDK_API.EdsInitializeSDK();
        }
        catch (Exception x)
        {
            _logger.LogError(x, "Error Initializing SDK");

            throw new("Error Initializing SDK", x);
            // TODO: Move to Initialize pattern instead of constructor
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

    private void STAThread_FatalError(object? sender, EventArgs e) => OnSdkError(new("Execution thread error", LogLevel.Critical));






    /// <summary>
    /// Get a list of all connected cameras
    /// </summary>
    /// <returns>The camera list</returns>
    public List<SDKCamera> GetCameraList()
    {
        //get list of cameras
        Error = EDSDK_API.EdsGetCameraList(out nint camlist);

        //get each camera from camlist
        //get amount of connected cameras
        Error = EDSDK_API.EdsGetChildCount(camlist, out int c);
        List<SDKCamera> camList = [];
        for (int i = 0; i < c; i++)
        {
            //get pointer to camera at index i
            Error = EDSDK_API.EdsGetChildAtIndex(camlist, i, out nint cptr);
            camList.Add(new SDKCamera(this, cptr));
        }

        LogInfo($"Found {camList.Count} camera(s).");

        return camList;
    }

    /// <summary>
    /// Opens a session with given camera
    /// </summary>
    /// <param name="newCamera">The camera which will be used</param>
    public void OpenSession(SDKCamera newCamera)
    {
        _logger?.LogDebug("Opening session");

        if (CameraSessionOpen)
            CloseSession();

        if (newCamera != null)
        {
            MainCamera = newCamera;

            //open a session
            SendSDKCommand(delegate
            {
                Error = MainCamera.OpenSession();
            }, sdk_action: nameof(EDSDK_API.OpenSession));

            //subscribe to the camera events (for the SDK)
            AddCameraHandler(() => Error = EDSDK_API.EdsSetCameraStateEventHandler(MainCamera.Handle, StateEvent.All, SDKStateEvent, MainCamera.Handle), nameof(EDSDK_API.EdsSetCameraStateEventHandler));
            AddCameraHandler(() => Error = EDSDK_API.EdsSetObjectEventHandler(MainCamera.Handle, EdsEvent.All, SDKObjectEvent, MainCamera.Handle), nameof(EDSDK_API.EdsSetObjectEventHandler));
            AddCameraHandler(() => Error = EDSDK_API.EdsSetPropertyEventHandler(MainCamera.Handle, PropertyEvent.All, SDKPropertyEvent, MainCamera.Handle), nameof(EDSDK_API.EdsSetPropertyEventHandler));

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
            Error = EDSDK_API.EdsSetCameraStateEventHandler(MainCamera, StateEvent.All, null, MainCamera.Handle);
            Error = EDSDK_API.EdsSetObjectEventHandler(MainCamera, EdsEvent.All, null, MainCamera.Handle);
            Error = EDSDK_API.EdsSetPropertyEventHandler(MainCamera, PropertyEvent.All, null, MainCamera.Handle);

            //close session and release camera
            SendSDKCommand(delegate
            {
                Error = EDSDK_API.CloseSession(MainCamera);
            }, sdk_action: nameof(EDSDK_API.CloseSession));

            SDKError c = EDSDK_API.Release(MainCamera);

            CameraSessionOpen = false;
            MainCamera = null;
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
        Error = EDSDK_API.EdsTerminateSDK();
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
        LogInfo($"SDK Object Event. Property: {SDKProperty.FromSDKObjectEvent(inEvent)}");

        switch (inEvent)
        {
            case EdsEvent.All:
            case EdsEvent.DirItemCancelTransferDT:
            case EdsEvent.DirItemContentChanged:
            case EdsEvent.DirItemInfoChanged:
            case EdsEvent.DirItemRemoved:
            case EdsEvent.DirItemRequestTransferDT:
            case EdsEvent.FolderUpdateItems:
            case EdsEvent.VolumeAdded:
            case EdsEvent.VolumeInfoChanged:
            case EdsEvent.VolumeRemoved:
            case EdsEvent.VolumeUpdateItems:
                break;
            case EdsEvent.DirItemCreated when DownloadVideo:
                DownloadImage(inRef, ImageSaveDirectory, ImageSaveFilename, isVideo: true);
                DownloadVideo = false;

                break;
            case EdsEvent.DirItemRequestTransfer:
                DownloadImage(inRef, ImageSaveDirectory, ImageSaveFilename);

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
    /// <param name="property">The Property ID</param>
    /// <param name="inParameter">Event Parameter</param>
    /// <param name="inContext">...</param>
    /// <returns>An EDSDK errorcode</returns>
    private SDKError Camera_SDKPropertyEvent(PropertyEvent inEvent, SDKProperty property, uint inParameter, nint inContext)
    {
        if (inEvent is PropertyEvent.PropertyChanged)
            LogPropertyValue(property, property.Get(this));

        if (property == SDKProperty.Evf_OutputDevice && IsLiveViewOn)
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
        LogInfo($"SDK State Event. Property: {SDKProperty.FromSDKStateEvent(inEvent)}");

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

                        Error = MainCamera.SendCommand(CameraCommand.ExtendShutDownTimer, 0);
                    }, sdk_action: nameof(CameraCommand.ExtendShutDownTimer));
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

                LogInfo($"Downloaded data. Filename: {targetImage}, FileLengthMB: {mB:0.0}, DurationSeconds: {stopWatch.Elapsed.TotalSeconds:0.0}, MBPerSecond: {mB / stopWatch.Elapsed.TotalSeconds:0.0}");

                if (isVideo)
                    videoDownloadDone?.TrySetResult(targetImage);
                else
                    takePhotoCompletionSource?.TrySetResult(new FileInfo(fileName));
            }, long_running: true);
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
                    DownloadData(ObjectPointer, streamRef);

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
            }, long_running: true);
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

    #region Set Settings

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

                Error = MainCamera.SendCommand(command, value);
            }, sdk_action: nameof(EDSDK_API.SetPropertyData));
        else
            throw new InvalidOperationException("Camera or camera reference is null/zero");
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
            LogPropertyValue(nameof(SDKProperty.Evf_OutputDevice), SDKProperty.Evf_DepthOfFieldPreview.Get<uint>(MainCamera));

            MainCamera.StartLiveView();

            IsLiveViewOn = true;

            LogPropertyValue(nameof(SDKProperty.Evf_OutputDevice), MainCamera.Get(SDKProperty.Evf_OutputDevice));
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
                MainCamera.StopLiveView(LVoff);
            else
                _logger.LogDebug("LiveView stopped cleanly");
        }
    }

    private readonly AutoResetEvent cancelLiveViewWait = new(false);

    /// <summary>
    /// Downloads the live view image
    /// </summary>
    private unsafe void DownloadEvf()
    {
        LVThread = STAThread.Create(delegate
        {
            try
            {
                SDKElectronicViewfinderImage efv_image;

                //create stream
                Error = EDSDK_API.EdsCreateMemoryStream(0, out nint stream);

                //run live view
                while (IsLiveViewOn)
                {
                    lock (STAThread.ExecLock)
                    {
                        SDKError error = EDSDK_API.CreateEvfImageRef(stream, this, out efv_image);

                        if (error is SDKError.OK)
                            error = EDSDK_API.DownloadEvfImage(MainCamera, efv_image);

                        if (error is SDKError.OBJECT_NOTREADY)
                        {
                            Thread.Sleep(10);

                            continue;
                        }
                        else
                            Error = error;
                    }

                    //get pointer
                    Error = EDSDK_API.EdsGetPointer(stream, out nint jpgPointer);
                    Error = EDSDK_API.EdsGetLength(stream, out ulong length);

                    //get some live view image metadata
                    if (!IsCoordSystemSet)
                    {
                        Evf_CoordinateSystem = efv_image.EVFCoordinateSystem;
                        IsCoordSystemSet = true;
                    }

                    Evf_ZoomRect = efv_image.ZoomPosition;
                    Evf_ZoomPosition = efv_image.GetEVFPoint(SDKProperty.Evf_ZoomPosition);
                    Evf_ImagePosition = efv_image.GetEVFPoint(SDKProperty.Evf_ImagePosition);

                    //release current evf image
                    Error = EDSDK_API.Release(efv_image);

                    using UnmanagedMemoryStream ums = new((byte*)jpgPointer, (long)length, (long)length, FileAccess.Read);

                    // fire the LiveViewUpdated event with the live view image stream
                    OnLiveViewUpdated(ums);

                    ums.Close();
                }

                Error = EDSDK_API.Release(stream);

                MainCamera.StopLiveView();

                cancelLiveViewWait.Set();
            }
            catch
            {
                IsLiveViewOn = false;
            }
        });
        LVThread.Start();
    }

    /// <summary>
    /// Fires the LiveViewUpdated event
    /// </summary>
    /// <param name="stream"></param>
    private void OnLiveViewUpdated(UnmanagedMemoryStream stream) => LiveViewUpdated?.Invoke(stream);

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

    /// <summary>
    /// Starts recording a video
    /// NOTE: Will throw an ArgumentException if the camera is not in the correct mode
    /// </summary>
    public void StartFilming()
    {
        if (!IsFilming)
        {
            //Snapshot setting for restoration after filming completes
            PrevEVFSetting = MainCamera.Get(SDKProperty.Evf_OutputDevice);


            //Set EVF output to TFT to enable film, otherwise
            //NOTE: Not working to set it and start video in the same action, disabling
            //SetSetting(SDKProperty.Evf_OutputDevice, EvfOutputDevice_TFT);
            //SetTFTEvf();

            //LogPropertyValue(nameof(SDKProperty.Record), GetSetting(SDKProperty.Record));

            MainCamera.Set(SDKProperty.Evf_OutputDevice, 3);

            //Check if the camera is ready to film
            VideoRecordStatus recordStatus = MainCamera.Get<VideoRecordStatus>(SDKProperty.Record);

            if (recordStatus != VideoRecordStatus.Movie_shooting_ready)
            {
                //DOES NOT WORK, readonly setting?
                //DOES NOT THROW AN ERROR
                //SetSetting(SDKProperty.Record, EdsDriveMode.Video);
                //SetSetting(SDKProperty.Record, VideoRecordStatus.Movie_shooting_ready);

                LogPropertyValue(SDKProperty.Record, recordStatus);
                LogInfo($"Camera reporting incorrect mode. expected. Continue. {VideoRecordStatus.Movie_shooting_ready}, was: {recordStatus}");
                LogInfo("Camera physical switch must be in movie record mode. Leave in this mode permanently!");
                //throw new ArgumentException("Camera in invalid mode", nameof(SDKProperty.Record));
            }

            IsFilming = true;
            PrevSaveTo = MainCamera.Get(SDKProperty.SaveTo); // to restore the current setting after recording

            MainCamera.Set(SDKProperty.SaveTo, (uint)EdsSaveTo.Camera); // when recording videos, it has to be saved on the camera internal memory

            DownloadVideo = false; // start the video recording

            LogInfo("Start filming");

            SendSDKCommand(() => MainCamera.Set(SDKProperty.Record, VideoRecordStatus.Begin_movie_shooting));
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
                MainCamera.Set(SDKProperty.Record, VideoRecordStatus.End_movie_shooting);
                stopMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                //set back to previous state
            });

            MainCamera.Set(SDKProperty.SaveTo, PrevSaveTo);
            MainCamera.Set(SDKProperty.Evf_OutputDevice, PrevEVFSetting);

            if (PrevCapacity.NumberOfFreeClusters != 0)
                SetCapacity(PrevCapacity);

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
                MainCamera.SendCommand(CameraCommand.PressShutterButton, (int)state);
        }, long_running: true);

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
        MainCamera.SetSaveToHost();
        ImageSaveDirectory = directory.FullName;
    }

    /// <summary>
    /// Takes a photo with the current camera settings
    /// </summary>
    public void TakePhoto() => SendSDKCommand(delegate
    {
        lock (STAThread.ExecLock)
            Error = MainCamera.SendCommand(CameraCommand.TakePicture, 0);
    }, long_running: true);

    /// <summary>
    /// Takes a photo in bulb mode with the current camera settings
    /// </summary>
    /// <param name="bulbTime">The time in milliseconds for how long the shutter will be open</param>
    public void TakePhoto(uint bulbTime)
    {
        //bulbtime has to be at least a second
        if (bulbTime < 1000)
            throw new ArgumentException("Bulbtime has to be bigger than 1000ms");

        //start thread to not block everything
        SendSDKCommand(delegate
        {
            //open the shutter
            lock (STAThread.ExecLock)
                Error = MainCamera.SendCommand(CameraCommand.BulbStart, 0);

            //wait for the specified time
            Thread.Sleep((int)bulbTime);

            //close shutter
            lock (STAThread.ExecLock)
                Error = MainCamera.SendCommand(CameraCommand.BulbEnd, 0);
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
            float freePc = volumeInfo.FreeSpaceInBytes / (float)volumeInfo.MaxCapacity;

            _logger.LogDebug("Camera volume free space. volume: {volume}, freeSpaceBytes: {freeSpaceBytes}, maxCapacity: {maxCapacity}, freePercent: {freePercent}", volumeInfo.szVolumeLabel, volumeInfo.FreeSpaceInBytes, volumeInfo.MaxCapacity, freePc);

            if (freePc < minPercent)
                minPercent = freePc;
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
                action(childReference, volumeInfo);

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
        }, sdk_action: nameof(EDSDK_API.EdsFormatVolume));
    }


    #endregion

    #region Other

    internal void SendSDKCommand(Func<SDKError> command, string? sdk_action = null, bool long_running = false) =>
        SendSDKCommand(new Action(() => Error = command()), sdk_action, long_running);

    /// <summary>
    /// Sends a command to the camera safely
    /// </summary>
    internal void SendSDKCommand(Action command, string? sdk_action = null, bool long_running = false)
    {
        LogInfo($"Sending SDK command: {sdk_action ?? "(unknown)"}");

        try
        {
            if (long_running)
                STAThread.Create(command).Start();
            else
                STAThread.ExecuteSafely(command);
        }
        catch (Exception ex)
        {
            HandleException(ex, $"Error sending command: {sdk_action ?? "(unknown)"}");
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

        SendSDKCommand(() => Error = EDSDK_API.EdsSetCapacity(MainCamera.Handle, capacity));
    }




    /// <summary>
    /// Moves the focus (only works while in live view)
    /// </summary>
    /// <param name="speed">Speed and direction of focus movement</param>
    public void SetFocus(uint speed)
    {
        if (IsLiveViewOn)
            SendSDKCommand(() => Error = MainCamera.SendCommand(CameraCommand.DriveLensEvf, (int)speed));
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
            uint coord = BitConverter.ToUInt32([xa[0], xa[1], ya[0], ya[1]], 0);
            //send command to camera
            SendSDKCommand(() => Error = MainCamera.SendCommand(CameraCommand.DoClickWBEvf, (int)coord));
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
    public void UILock(bool lockState) => SendSDKCommand(() => Error = MainCamera.SendStatusCommand(lockState ? CameraState.UILock : CameraState.UIUnLock, 0));

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
}

public class SDKLogger
    : ILogger
    , IAsyncDisposable
{
    private sealed record SDKLoggerMessage(DateTime Timestamp, LogLevel Level, Exception? Exception, object? State, EventId Event, string Message);

    private readonly struct __empty
    {
    }


    public static SDKLogger ConsoleOutput => new(Console.OpenStandardOutput(), true);

    public static SDKLogger ConsoleError => new(Console.OpenStandardOutput(), true);

    private readonly ConcurrentQueue<SDKLoggerMessage> _messages;
    private readonly ConcurrentDictionary<LogLevel, __empty> _enabled;
    private readonly bool _use_vt100_sequence;
    private readonly Stream _output_stream;
    private volatile bool _is_logging;
    private Task? _logger_task;


    public SDKLogger(Stream stream, bool useVT100 = true)
    {
        _output_stream = stream;
        _use_vt100_sequence = useVT100;
        _messages = new();
        _enabled = new(Enum.GetValues<LogLevel>().Select(v => new KeyValuePair<LogLevel, __empty>(v, default)));
    }

    public async Task StartAsync()
    {
        await StopAsync();

        while (_logger_task is { })
            await Task.Delay(20);

        _is_logging = true;
        _logger_task = Task.Factory.StartNew(async delegate
        {
            bool flushed = true;

            while (_is_logging)
                if (_messages.TryDequeue(out SDKLoggerMessage? message))
                {
                    await PrintAsync(message);

                    flushed = false;
                }
                else if (flushed)
                    await Task.Delay(200);
                else
                {
                    await FlushAsync();

                    flushed = true;
                }
        });
    }

    public async Task StopAsync()
    {
        if (_logger_task is { })
        {
            _is_logging = false;

            await _logger_task;

            while (_messages.TryDequeue(out SDKLoggerMessage? message))
                await PrintAsync(message);

            await FlushAsync();

            _logger_task.Dispose();
            _logger_task = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _output_stream.DisposeAsync();
    }

    private Task PrintAsync(SDKLoggerMessage message)
    {
        // TODO : vt100 formatting
        string repr = $"[{message.Timestamp:yyyy-MM-dd HH:mm:ss.ffffff}][{message.Level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO.",
            LogLevel.Warning => "WARN.",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT.",
            LogLevel.None => "      ",
            _ => " ??? "
        }}][{message.Event}] {message.Message} {message.Exception}\n";
        byte[] bytes = Encoding.UTF8.GetBytes(repr);

        return _output_stream.WriteAsync(bytes, 0, bytes.Length);
    }

    private Task FlushAsync() => _output_stream.FlushAsync();

    public void Enable(LogLevel level) => _enabled[level] = default;

    public void Disable(LogLevel level) => _enabled.TryRemove(level, out _);

    public bool IsEnabled(LogLevel level) => _enabled.ContainsKey(level);

    public void LogInfo(string message) => this.LogInformation(message, []);

    public void LogWarning(string message) => this.LogWarning(message, []);

    public void LogError(string message) => this.LogError(message, []);

    public void LogError(Exception exception, string message) => this.LogError(exception, message, []);

    public void LogError(Exception exception) => this.LogError(exception, exception.Message, []);

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _messages.Enqueue(new(DateTime.Now, logLevel, exception, state, eventId, formatter(state, exception)));

    IDisposable? ILogger.BeginScope<TState>(TState state) => throw new NotImplementedException();
}
