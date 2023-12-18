using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Threading;
using System.Drawing.Imaging;
using System.Drawing;
using System.Text;
using System.Linq;
using System.IO;
using System;

using EDSDK.Native;

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;


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
    private TaskCompletionSource<FileInfo> videoDownloadDone;
    private readonly AutoResetEvent cancelLiveViewWait = new(false);

    private string _imageSaveFilename;


    /// <summary>
    /// States if a finished video should be downloaded from the camera
    /// </summary>
    private bool DownloadVideo;

    /// <summary>
    /// For video recording, SaveTo has to be set to Camera. This is to store the previous setting until after the filming.
    /// </summary>
    private EdsSaveTo PrevSaveTo;

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



    /* public */ event EdsSDKErrorEventHandler OnSDKError;
    /* public */ event EdsCameraAddedHandler SDKCameraAddedEvent;
    /* public */ event EdsObjectEventHandler SDKObjectEvent;
    /* public */ event EdsProgressCallback SDKProgressCallbackEvent;
    /* public */ event EdsPropertyEventHandler SDKPropertyEvent;
    /* public */ event EdsStateEventHandler SDKStateEvent;



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
    public event EventHandler OnCameraShutdown;

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
            Logger.LogInfo($"Setting {nameof(ImageSaveFilename)} to '{value}'.");

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
            if (value != Native.SDKError.OK)
            {
                SDKProperty property = SDKProperty.FromSDKError(value) ?? SDKProperty.Unknown;

                Logger.LogError($"SDK Error set to {value} ({property}).");

                if (value is Native.SDKError.COMM_DISCONNECTED or Native.SDKError.DEVICE_INVALID or Native.SDKError.DEVICE_NOT_FOUND)
                    OnSDKError?.Invoke(this, new(property.Name, LogLevel.Critical));
            }
        }
    }



    //public void SetUintSetting(string name, string propertyValue)
    //{
    //    bool error = false;
    //
    //    if (!string.IsNullOrEmpty(propertyValue))
    //        propertyValue = propertyValue.Replace("0x", "");
    //
    //    if (!uint.TryParse(propertyValue, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out uint value))
    //    {
    //        Logger.LogError($"Could not convert value {propertyValue} to uint");
    //
    //        error = true;
    //    }
    //
    //    if (!string.IsNullOrEmpty(name) && name.StartsWith("kEds"))
    //        name = name[4..];
    //
    //    if (SDKProperty.FromName(name) is SDKProperty prop)
    //    {
    //        if (!error)
    //            MainCamera?.Set(prop, value);
    //    }
    //    else
    //        Logger.LogWarning($"Could not find property named {name}");
    //}

    public void DumpAllProperties()
    {
        string dump = $"========= SDK Properties ({SDKProperties.Length}) =========";

        foreach (SDKProperty prop in SDKProperties)
        {
            uint value = MainCamera?[prop] ?? 0;

            dump += $"\n{prop.Name,50} = 0x{value:x8} ({value})";
        }

        Logger.LogInfo(dump);
    }




    public SDKProperty[] SDKProperties { get; private set; }

    public SDKProperty[] SDKStateEvents { get; private set; }

    public object Value { get; private set; }

    public bool KeepAlive { get; set; }





    ////////////////////////////////////////////////////// TODO : clean everything above this line //////////////////////////////////////////////////////


    public SDKLogger Logger { get; }





    public SDKWrapper(SDKLogger logger)
    {
        Logger = logger;

        STAThread.SetLogAction(logger);
        STAThread.FatalError += STAThread_FatalError;

        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            logger.LogWarning($"{nameof(SDKWrapper)} created on a non-STA thread.");

        EDSDK_API.InitializeSDK(this);
        STAThread.Init();

        //subscribe to camera added event (the C# event and the SDK event)
        SDKCameraAddedEvent += SDKHandler_CameraAddedEvent;

        EDSDK_API.SetCameraAddedHandler(SDKCameraAddedEvent);

        //subscribe to the camera events (for the C# events)
        SDKStateEvent += Camera_SDKStateEvent;
        SDKPropertyEvent += Camera_SDKPropertyEvent;
        SDKProgressCallbackEvent += Camera_SDKProgressCallbackEvent;
        SDKObjectEvent += Camera_SDKObjectEvent;
    }

    private void STAThread_FatalError(object? sender, EventArgs e) => OnSDKError?.Invoke(this, new("Execution thread error", LogLevel.Critical));






    /// <summary>
    /// Get a list of all connected cameras
    /// </summary>
    /// <returns>The camera list</returns>
    public SDKCamera[] GetCameraList() => [.. SDKList.GetConnectedCameras(this)];




    /// <summary>
    /// Opens a session with given camera
    /// </summary>
    /// <param name="newCamera">The camera which will be used</param>
    public void OpenSession(SDKCamera newCamera)
    {
        Logger.LogInfo("Opening session");

        if (CameraSessionOpen)
            CloseSession();

        if (newCamera != null)
        {
            MainCamera = newCamera;

            //open a session
            SendSDKCommand(MainCamera.OpenSession, sdk_action: nameof(EDSDK_API.OpenSession));

            //subscribe to the camera events (for the SDK)
            MainCamera.StateEventHandler = SDKStateEvent;
            MainCamera.ObjectEventHandler = SDKObjectEvent;
            MainCamera.PropertyEventHandler = SDKPropertyEvent;

            CameraSessionOpen = true;

            Logger.LogInfo($"Connected to Camera: {newCamera.Info.szDeviceDescription}");
        }
    }

    /// <summary>
    /// Closes the session with the current camera
    /// </summary>
    public void CloseSession()
    {
        Logger.LogInfo("Closing session");

        if (CameraSessionOpen)
        {
            //if live view is still on, stop it and wait till the thread has stopped
            if (IsLiveViewOn)
            {
                StopLiveView();
                LVThread.Join(1000);
            }

            //Remove the event handler
            MainCamera.StateEventHandler = null;
            MainCamera.ObjectEventHandler = null;
            MainCamera.PropertyEventHandler = null;

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
        CloseSession();

        EDSDK_API.TerminateSDK(this);
        STAThread.Shutdown(); // stop command execution thread
    }


    /// <summary>
    /// A new camera was plugged into the computer
    /// </summary>
    /// <param name="inContext">The pointer to the added camera</param>
    /// <returns>An EDSDK errorcode</returns>
    private SDKError SDKHandler_CameraAddedEvent(SDKObject? context)
    {
        //Handle new camera here
        OnCameraAdded();

        return Native.SDKError.OK;
    }

    private void OnCameraAdded() => CameraAdded?.Invoke();


    /// <summary>
    /// An Objectevent fired
    /// </summary>
    /// <param name="inEvent">The ObjectEvent id</param>
    /// <param name="object">Pointer to the object</param>
    /// <param name="inContext"></param>
    /// <returns>An EDSDK errorcode</returns>
    private SDKError Camera_SDKObjectEvent(EdsEvent inEvent, SDKObject @object, SDKObject? context)
    {
        Logger.LogInfo($"SDK Object Event. Property: {SDKProperty.FromSDKObjectEvent(inEvent)}, Object: {@object}");

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
                DownloadImage(@object.As<SDKFilesystemFile>(), ImageSaveDirectory, ImageSaveFilename, is_video: true);
                DownloadVideo = false;

                break;
            case EdsEvent.DirItemRequestTransfer:
                DownloadImage(@object.As<SDKFilesystemFile>(), ImageSaveDirectory, ImageSaveFilename);

                break;
        }

        return Native.SDKError.OK;
    }

    /// <summary>
    /// A progress was made
    /// </summary>
    /// <param name="inPercent">Percent of progress</param>
    /// <param name="inContext">...</param>
    /// <param name="outCancel">Set true to cancel event</param>
    /// <returns>An EDSDK errorcode</returns>
    private SDKError Camera_SDKProgressCallbackEvent(uint inPercent, SDKObject? context, ref bool outCancel)
    {
        //Handle progress here
        OnProgressChanged((int)inPercent);

        return Native.SDKError.OK;
    }

    private void OnProgressChanged(int percent) => ProgressChanged?.Invoke(percent);

    /// <summary>
    /// A property changed
    /// </summary>
    /// <param name="event">The PropertyEvent ID</param>
    /// <param name="property">The Property ID</param>
    /// <param name="inParameter">Event Parameter</param>
    /// <param name="inContext">...</param>
    /// <returns>An EDSDK errorcode</returns>
    private SDKError Camera_SDKPropertyEvent(PropertyEvent @event, SDKProperty property, uint param, SDKObject? context)
    {
        //if (inEvent is PropertyEvent.PropertyChanged)
        //    Logger.LogPropertyValue(property, property.Get(this));

        if (property == SDKProperty.Evf_OutputDevice && IsLiveViewOn)
            DownloadEvf();

        return Native.SDKError.OK;
    }

    /// <summary>
    /// The camera state changed
    /// </summary>
    /// <param name="inEvent">The StateEvent ID</param>
    /// <param name="inParameter">Parameter from this event</param>
    /// <param name="inContext">...</param>
    /// <returns>An EDSDK errorcode</returns>
    private SDKError Camera_SDKStateEvent(StateEvent inEvent, uint inParameter, SDKObject? context)
    {
        Logger.LogInfo($"SDK State Event. Property: {SDKProperty.FromSDKStateEvent(inEvent)}");

        //Handle state event here
        switch (inEvent)
        {
            case StateEvent.All:
            case StateEvent.JobStatusChanged:
            case StateEvent.ShutDownTimerUpdate:
                break;
            case StateEvent.CaptureError:
            case StateEvent.InternalError:
                Logger.LogError($"Error event. error: {inEvent}");

                break;
            case StateEvent.Shutdown:
                CameraSessionOpen = false;

                if (IsLiveViewOn)
                {
                    StopLiveView();

                    LVThread.Abort(); // Not supported in .NET Core. Transition to cancellation token LVThread.Abort();
#warning todo fix this shite
                }

                OnCameraShutdown?.Invoke(this, new());

                break;
            case StateEvent.WillSoonShutDown:
                if (KeepAlive)
                    SendSDKCommand(() =>
                    {
                        Logger.LogInfo("Extending camera shutdown timer");

                        MainCamera.SendCommand(CameraCommand.ExtendShutDownTimer, 0);
                    }, sdk_action: nameof(CameraCommand.ExtendShutDownTimer));
                break;
        }

        return Native.SDKError.OK;
    }


    // private void RaiseCameraShutdown() => OnCameraShutdown?.Invoke(this, new EventArgs());
    // 
    // private void RaiseSDKError(SDKErrorEventArgs e) => OnSDKError?.Invoke(this, e);


    /// <summary>
    /// Downloads an image to given directory
    /// </summary>
    /// <param name="file">Pointer to the object. Get it from the SDKObjectEvent.</param>
    /// <param name="target_directory">Path to where the image will be saved to</param>
    public void DownloadImage(SDKFilesystemFile file, string target_directory, string? target_file_name = null, bool is_video = false)
    {
        try
        {
            FileInfo target_file;

            if (string.IsNullOrEmpty(target_file_name))
                target_file_name = file.Name;
            else
            {
                target_file = new(target_file_name);
                string target_extension = Path.GetExtension(file.Name);

                if (!string.Equals(target_file.Extension, target_extension, StringComparison.OrdinalIgnoreCase))
                    target_file_name = $"{target_file.Name[..^target_file.Extension.Length]}{target_extension}";
            }

            target_file = new(Path.Combine(target_directory, target_file_name));

            if (target_file.Exists)
                throw new IOException($"The destination file '{target_file}' already exists.");
            else if (!Directory.Exists(target_directory))
                Directory.CreateDirectory(target_directory);

            Logger.LogInfo($"Downloading '{file}' -> '{target_file}'.");

            SendSDKCommand(delegate
            {
                Stopwatch sw = Stopwatch.StartNew();
                SDKStream stream = SDKStream.CreateFileStream(this, target_file, EdsFileCreateDisposition.CreateAlways, EdsAccess.ReadWrite);

                STAThread.TryLockAndExecute(STAThread.ExecLock, nameof(STAThread.ExecLock), TimeSpan.FromSeconds(30), () => file.Download(stream, SDKProgressCallbackEvent));

                stream.Release();
                sw.Stop();

                double mB = target_file.Length * 9.5367431640625e-7;

                Logger.LogInfo($"Downloaded '{file}' -> '{target_file}' ({mB:0.0} MB, {sw.Elapsed.TotalSeconds:0.0} sec, {mB / sw.Elapsed.TotalSeconds:0.0} MBps)");

                if (is_video)
                    videoDownloadDone?.TrySetResult(target_file);
                else
                    takePhotoCompletionSource?.TrySetResult(target_file);
            }, long_running: true);
        }
        catch (Exception x)
        {
            Logger.LogError(x, $"Error downloading '{file}'.");

            takePhotoCompletionSource.TrySetException(x);
            videoDownloadDone?.TrySetException(x);
        }
    }

    /// <summary>
    /// Downloads a jpg image from the camera into a Bitmap. Fires the ImageDownloaded event when done.
    /// </summary>
    /// <param name="file">Pointer to the object. Get it from the SDKObjectEvent.</param>
    public unsafe void DownloadImage(SDKFilesystemFile file)
    {
        Logger.LogInfo($"Downloading image {file.Name}");

        string ext = Path.GetExtension(file.Name).ToLower();

        if (ext is ".jpg" or ".jpeg")
            SendSDKCommand(delegate
            {
                Bitmap bmp;
                SDKStream stream = SDKStream.CreateMemoryStream(this, file.FileSize);

                lock (STAThread.ExecLock)
                    file.Download(stream, SDKProgressCallbackEvent);

                ulong length = stream.Length;

                using (UnmanagedMemoryStream ums = new((byte*)stream.Pointer, (long)length, (long)length, FileAccess.Read))
                    bmp = new Bitmap(ums);

                stream.Release();

                //Fire the event with the image
                OnImageDownloaded(bmp);
            }, long_running: true);
        else
        {
            // if it's a RAW image, cancel the download and release the image
            SendSDKCommand(file.DownloadCancel);

            file.Release();
        }
    }

    protected void OnImageDownloaded(Bitmap bitmap) => ImageDownloaded?.Invoke(bitmap);

    /// <summary>
    /// Gets the thumbnail of an image (can be raw or jpg)
    /// </summary>
    /// <param name="filepath">The filename of the image</param>
    /// <returns>The thumbnail of the image</returns>
    public Bitmap GetFileThumb(FileInfo filepath)
    {
        SDKStream stream = SDKStream.CreateFileStream(this, filepath, EdsFileCreateDisposition.OpenExisting, EdsAccess.Read);

        return GetImage(stream, EdsImageSource.Thumbnail);
    }


    public void __SendCommand(CameraCommand command, int value)
    {
        if (MainCamera.Handle != 0)
            SendSDKCommand(delegate
            {
                Thread cThread = Thread.CurrentThread;

                Logger.LogInfo($"Executing SDK command. ThreadName: {cThread.Name}, ApartmentState: {cThread.GetApartmentState()}");

                MainCamera.SendCommand(command, value);
            }, sdk_action: nameof(EDSDK_API.SetPropertyData));
        else
            throw new InvalidOperationException("Camera or camera reference is null/zero");
    }




    /// <summary>
    /// Starts the live view
    /// </summary>
    public async Task StartLiveView()
    {
        if (!IsLiveViewOn)
        {
            Logger.LogInfo("Starting Liveview");

            int listener_count = LiveViewUpdated?.GetInvocationList()?.Length ?? 0;

            Logger.LogInfo($"{listener_count} LiveViewUpdated listeners found");
            //Logger.LogPropertyValue(nameof(SDKProperty.Evf_OutputDevice), SDKProperty.Evf_DepthOfFieldPreview.Get<uint>(MainCamera));

            MainCamera.StartLiveView();

            IsLiveViewOn = true;

            //Logger.LogPropertyValue(nameof(SDKProperty.Evf_OutputDevice), MainCamera.Get(SDKProperty.Evf_OutputDevice));
        }
    }

    /// <summary>
    /// Stops the live view
    /// </summary>
    public void StopLiveView()
    {
        if (IsLiveViewOn)
        {
            Logger.LogInfo("Stopping liveview");

            LVoff = true;
            IsLiveViewOn = false;

            //Wait 5 seconds for evf thread to finish, otherwise manually stop
            if (!cancelLiveViewWait.WaitOne(TimeSpan.FromSeconds(5)))
                MainCamera.StopLiveView(LVoff);
            else
                Logger.LogInfo("LiveView stopped cleanly");
        }
    }

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

                var stream = SDKStream.CreateMemoryStream(this, 0);

                //run live view
                while (IsLiveViewOn)
                {
                    lock (STAThread.ExecLock)
                    {
                        SDKError error = EDSDK_API.CreateEvfImageRef(stream, this, out efv_image);

                        if (error is Native.SDKError.OK)
                            error = EDSDK_API.DownloadEvfImage(MainCamera, efv_image);

                        if (error is Native.SDKError.OBJECT_NOTREADY)
                        {
                            Thread.Sleep(10);

                            continue;
                        }
                        else
                            Error = error;
                    }

                    ulong length = stream.Length;

                    //get some live view image metadata
                    if (!IsCoordSystemSet)
                    {
                        Evf_CoordinateSystem = efv_image.EVFCoordinateSystem;
                        IsCoordSystemSet = true;
                    }

                    Evf_ZoomRect = efv_image.ZoomPosition;
                    Evf_ZoomPosition = efv_image.GetEVFPoint(SDKProperty.Evf_ZoomPosition);
                    Evf_ImagePosition = efv_image.GetEVFPoint(SDKProperty.Evf_ImagePosition);

                    efv_image.Release();

                    using (UnmanagedMemoryStream ums = new((byte*)stream.Pointer, (long)length, (long)length, FileAccess.Read))
                        OnLiveViewUpdated(ums);
                }

                stream.Release();

                MainCamera.StopLiveView(LVoff);

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
            Logger.LogWarning($"Ignoring attempt to take photo whilst filming or in live-view mode. Filming: {IsFilming}, LiveView: {IsLiveViewOn}");

            return null;
        }

        takePhotoCompletionSource = new();
        SetSaveToLocation(saveFile.Directory);
        ImageSaveFilename = saveFile.Name;

        TakePhoto();

        await takePhotoCompletionSource.Task;

        if (takePhotoCompletionSource.Task.Status == TaskStatus.RanToCompletion)
            return takePhotoCompletionSource.Task.Result;

        Logger.LogError("Error taking photo, check previous messages");

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
            MainCamera.SendCommand(CameraCommand.TakePicture, 0);
    }, long_running: true);

    /// <summary>
    /// Takes a photo in bulb mode with the current camera settings
    /// </summary>
    /// <param name="bulbTime">The time in milliseconds for how long the shutter will be open</param>
    public void TakePhoto(uint bulbTime)
    {
        //bulbtime has to be at least a second
        if (bulbTime < 1000)
            throw new ArgumentException("Bulb time has to be bigger than 1000ms");

        //start thread to not block everything
        SendSDKCommand(delegate
        {
            //open the shutter
            lock (STAThread.ExecLock)
                MainCamera.SendCommand(CameraCommand.BulbStart, 0);

            //wait for the specified time
            Thread.Sleep((int)bulbTime);

            //close shutter
            lock (STAThread.ExecLock)
                MainCamera.SendCommand(CameraCommand.BulbEnd, 0);
        }, long_running: true);
    }


    public void FormatAllVolumes() => RunForEachVolume(volume => SendSDKCommand(volume.Format));

    public float GetMinVolumeSpacePercent()
    {
        float min_percent = 1f;

        RunForEachVolume(volume =>
        {
            float free = volume.FreeSpace / (float)volume.Capacity;

            Logger.LogInfo($"Camera volume free space. volume: {volume.Name}, free: {volume.FreeSpace} ({free:P}), capacity: {volume.Capacity}");

            min_percent = Math.Min(min_percent, free);
        });

        return min_percent;
    }

    private void RunForEachVolume(Action<SDKFilesystemVolume> action)
    {
        foreach (SDKFilesystemVolume volume in MainCamera.Filesystem.AllVolumes)
            if (volume is { StorageType: not EdsStorageType.None, Access: EdsAccess.ReadWrite })
                action(volume);
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
        Logger.LogInfo($"Sending SDK command: {sdk_action ?? "(unknown)"}");

        try
        {
            if (long_running)
                STAThread.Create(command).Start();
            else
                STAThread.ExecuteSafely(command);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error sending command: {sdk_action ?? "(unknown)"}");
        }
    }


    // /// <summary>
    // /// Gets all volumes, folders and files existing on the camera
    // /// </summary>
    // /// <returns>A CameraFileEntry with all informations</returns>
    // public SDKFilesystemEntry[] GetAllEntries() => [.. MainCamera.Filesystem.NonHDDVolumes.SelectMany(volume => volume.GetAllSubEntriesRecursively())];


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
