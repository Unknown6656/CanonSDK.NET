using System.Runtime.InteropServices;
using System;

using EDSDK.NET;

namespace EDSDK.Native;


public delegate SDKError EdsProgressCallback(uint inPercent, nint inContext, ref bool outCancel);
public delegate SDKError EdsCameraAddedHandler(nint inContext);
public delegate SDKError EdsPropertyEventHandler(PropertyEvent @event, SDKProperty property, uint param, nint inContext);
public delegate SDKError EdsObjectEventHandler(EdsEvent @event, nint inRef, nint inContext);
public delegate SDKError EdsStateEventHandler(StateEvent @event, uint inParameter, nint inContext);

public enum EdsDataType
    : uint
{
    Unknown = 0,
    Bool = 1,
    String = 2,
    Int8 = 3,
    UInt8 = 6,
    Int16 = 4,
    UInt16 = 7,
    Int32 = 8,
    UInt32 = 9,
    Int64 = 10,
    UInt64 = 11,
    Float = 12,
    Double = 13,
    ByteBlock = 14,
    Rational = 20,
    Point = 21,
    Rect = 22,
    Time = 23,

    Bool_Array = 30,
    Int8_Array = 31,
    Int16_Array = 32,
    Int32_Array = 33,
    UInt8_Array = 34,
    UInt16_Array = 35,
    UInt32_Array = 36,
    Rational_Array = 37,

    FocusInfo = 101,
    PictureStyleDesc,
}

public enum EdsEvfAf
    : uint
{
    CameraCommand_EvfAf_OFF = 0,
    CameraCommand_EvfAf_ON = 1,
}

public enum EdsShutterButton
    : uint
{
    CameraCommand_ShutterButton_OFF = 0x00000000,
    CameraCommand_ShutterButton_Halfway = 0x00000001,
    CameraCommand_ShutterButton_Completely = 0x00000003,
    CameraCommand_ShutterButton_Halfway_NonAF = 0x00010001,
    CameraCommand_ShutterButton_Completely_NonAF = 0x00010003,
}

/// <summary>
/// Stream Seek Origins
/// </summary>
public enum EdsSeekOrigin
    : uint
{
    Cur = 0,
    Begin,
    End,
}

/// <summary>
/// File and Propaties Access
/// </summary>
public enum EdsAccess
    : uint
{
    Read = 0,
    Write,
    ReadWrite,
    Error = 0xFFFFFFFF,
}

/// <summary>
/// File Create Disposition
/// </summary>
public enum EdsFileCreateDisposition
    : uint
{
    CreateNew = 0,
    CreateAlways,
    OpenExisting,
    OpenAlways,
    TruncateExsisting,
}

/// <summary>
/// Target Image Types
/// </summary>
public enum EdsTargetImageType
    : uint
{
    Unknown = 0x00000000,
    Jpeg = 0x00000001,
    TIFF = 0x00000007,
    TIFF16 = 0x00000008,
    RGB = 0x00000009,
    RGB16 = 0x0000000A,
}

/// <summary>
/// Image Source
/// </summary>
public enum EdsImageSource
    : uint
{
    FullView = 0,
    Thumbnail,
    Preview,
}

/// <summary>
/// Progress Option
/// </summary>
public enum EdsProgressOption
    : uint
{
    NoReport = 0,
    Done,
    Periodically,
}

/// <summary>
/// file attribute
/// </summary>
public enum EdsFileAttribute
    : uint
{
    Normal = 0x00000000,
    ReadOnly = 0x00000001,
    Hidden = 0x00000002,
    System = 0x00000004,
    Archive = 0x00000020,
}

public enum EdsSaveTo
    : uint
{
    Camera = 1,
    Host = 2,
    Both = 3,
}

/// <summary>
/// Storage type
/// </summary>
public enum EdsStorageType
    : uint
{
    Non = 0,
    CF = 1,
    SD = 2,
}

/// <summary>
/// Transfer Option
/// </summary>
public enum EdsTransferOption
    : uint
{
    ByDirectTransfer = 1,
    ByRelease = 2,
    ToDesktop = 0x00000100,
}

/// <summary>
/// Mirror Lockup State
/// </summary>
public enum EdsMirrorLockupState
    : uint
{
    Disable = 0,
    Enable = 1,
    DuringShooting = 2,
}

/// <summary>
/// Mirror Up Setting
/// </summary>
public enum EdsMirrorUpSetting
    : uint
{
    Off = 0,
    On = 1,
}

/// <summary>
/// Drive mode enum, see SDKProperty_DriveMode for get / set
/// <para/>
/// <i>NOTE: Does not seem to correspond to the recording properties for video</i>
/// </summary>
public enum EdsDriveMode
    : uint
{
    Single_Frame_Shooting = 0x00000000,
    Continuous_Shooting = 0x00000001,
    Video = 0x00000002,
    Not_used = 0x00000003,
    High_Speed_Continuous_Shooting = 0x00000004,
    Low_Speed_Continuous_Shooting = 0x00000005,
    Single_Silent_Shooting = 0x00000006,
    _10_Sec_Self_Timer_plus_continuous_shots = 0x00000007,
    _10_Sec_Self_Timer = 0x00000010,
    _2_Sec_Self_Timer = 0x00000011,
    _14fps_super_high_speed = 0x00000012,
    Silent_single_shooting = 0x00000013,
    Silent_contin_shooting = 0x00000014,
    Silent_HS_continuous = 0x00000015,
    Silent_LS_continuous = 0x00000016,
}

public enum EdsEvfAFMode
    : uint
{
    Evf_AFMode_Quick = 0,
    Evf_AFMode_Live = 1,
    Evf_AFMode_LiveFace = 2,
    Evf_AFMode_LiveMulti = 3,
    Evf_AFMode_LiveZone = 4,
    Evf_AFMode_LiveCatchAF = 9,
    Evf_AFMode_LiveSpotAF = 10
}

/// <summary>
/// Strobo Mode
/// </summary>
public enum EdsStroboMode
{
    kEdsStroboModeInternal = 0,
    kEdsStroboModeExternalETTL = 1,
    kEdsStroboModeExternalATTL = 2,
    kEdsStroboModeExternalTTL = 3,
    kEdsStroboModeExternalAuto = 4,
    kEdsStroboModeExternalManual = 5,
    kEdsStroboModeManual = 6,
}

public enum EdsETTL2Mode
{
    kEdsETTL2ModeEvaluative = 0,
    kEdsETTL2ModeAverage = 1,
}

public enum EdsBracket
    : uint
{
    Bracket_AEB = 0x01,
    Bracket_ISOB = 0x02,
    Bracket_WBB = 0x04,
    Bracket_FEB = 0x08,
    Bracket_Unknown = 0xffffffff,
}

/// <summary>
/// DC Strobe
/// </summary>
public enum DcStrobe
    : uint
{
    DcStrobeAuto = 0,
    DcStrobeOn = 1,
    DcStrobeSlowsynchro = 2,
    DcStrobeOff = 3,
}

/// <summary>
/// DC Lens Barrel State
/// </summary>
public enum DcLensBarrelState
    : uint
{
    DcLensBarrelStateInner = 0,
    DcLensBarrelStateOuter = 1,
}

/// <summary>
/// DC Remote Shooting Mode
/// </summary>
public enum DcRemoteShootingMode
    : uint
{
    DcRemoteShootingModeStop = 0,
    DcRemoteShootingModeStart = 1,
}

public enum EdsImageQuality
    : uint
{
    /* Jpeg Only */
    LJ = 0x0010ff0f,    /* Jpeg Large */
    M1J = 0x0510ff0f,   /* Jpeg Middle1 */
    M2J = 0x0610ff0f,   /* Jpeg Middle2 */
    SJ = 0x0210ff0f,    /* Jpeg Small */
    LJF = 0x0013ff0f,   /* Jpeg Large Fine */
    LJN = 0x0012ff0f,   /* Jpeg Large Normal */
    MJF = 0x0113ff0f,   /* Jpeg Middle Fine */
    MJN = 0x0112ff0f,   /* Jpeg Middle Normal */
    SJF = 0x0213ff0f,   /* Jpeg Small Fine */
    SJN = 0x0212ff0f,   /* Jpeg Small Normal */
    S1JF = 0x0E13ff0f,  /* Jpeg Small1 Fine */
    S1JN = 0x0E12ff0f,  /* Jpeg Small1 Normal */
    S2JF = 0x0F13ff0f,  /* Jpeg Small2 */
    S3JF = 0x1013ff0f,  /* Jpeg Small3 */

    /* RAW + Jpeg */
    LR = 0x0064ff0f,    /* RAW */
    LRLJF = 0x00640013, /* RAW + Jpeg Large Fine */
    LRLJN = 0x00640012, /* RAW + Jpeg Large Normal */
    LRMJF = 0x00640113, /* RAW + Jpeg Middle Fine */
    LRMJN = 0x00640112, /* RAW + Jpeg Middle Normal */
    LRSJF = 0x00640213, /* RAW + Jpeg Small Fine */
    LRSJN = 0x00640212, /* RAW + Jpeg Small Normal */
    LRS1JF = 0x00640E13,    /* RAW + Jpeg Small1 Fine */
    LRS1JN = 0x00640E12,    /* RAW + Jpeg Small1 Normal */
    LRS2JF = 0x00640F13,    /* RAW + Jpeg Small2 */
    LRS3JF = 0x00641013,    /* RAW + Jpeg Small3 */

    LRLJ = 0x00640010,  /* RAW + Jpeg Large */
    LRM1J = 0x00640510, /* RAW + Jpeg Middle1 */
    LRM2J = 0x00640610, /* RAW + Jpeg Middle2 */
    LRSJ = 0x00640210,  /* RAW + Jpeg Small */

    /* MRAW(SRAW1) + Jpeg */
    MR = 0x0164ff0f,    /* MRAW(SRAW1) */
    MRLJF = 0x01640013, /* MRAW(SRAW1) + Jpeg Large Fine */
    MRLJN = 0x01640012, /* MRAW(SRAW1) + Jpeg Large Normal */
    MRMJF = 0x01640113, /* MRAW(SRAW1) + Jpeg Middle Fine */
    MRMJN = 0x01640112, /* MRAW(SRAW1) + Jpeg Middle Normal */
    MRSJF = 0x01640213, /* MRAW(SRAW1) + Jpeg Small Fine */
    MRSJN = 0x01640212, /* MRAW(SRAW1) + Jpeg Small Normal */
    MRS1JF = 0x01640E13,    /* MRAW(SRAW1) + Jpeg Small1 Fine */
    MRS1JN = 0x01640E12,    /* MRAW(SRAW1) + Jpeg Small1 Normal */
    MRS2JF = 0x01640F13,    /* MRAW(SRAW1) + Jpeg Small2 */
    MRS3JF = 0x01641013,    /* MRAW(SRAW1) + Jpeg Small3 */

    MRLJ = 0x01640010,  /* MRAW(SRAW1) + Jpeg Large */
    MRM1J = 0x01640510, /* MRAW(SRAW1) + Jpeg Middle1 */
    MRM2J = 0x01640610, /* MRAW(SRAW1) + Jpeg Middle2 */
    MRSJ = 0x01640210,  /* MRAW(SRAW1) + Jpeg Small */

    /* SRAW(SRAW2) + Jpeg */
    SR = 0x0264ff0f,    /* SRAW(SRAW2) */
    SRLJF = 0x02640013, /* SRAW(SRAW2) + Jpeg Large Fine */
    SRLJN = 0x02640012, /* SRAW(SRAW2) + Jpeg Large Normal */
    SRMJF = 0x02640113, /* SRAW(SRAW2) + Jpeg Middle Fine */
    SRMJN = 0x02640112, /* SRAW(SRAW2) + Jpeg Middle Normal */
    SRSJF = 0x02640213, /* SRAW(SRAW2) + Jpeg Small Fine */
    SRSJN = 0x02640212, /* SRAW(SRAW2) + Jpeg Small Normal */
    SRS1JF = 0x02640E13,    /* SRAW(SRAW2) + Jpeg Small1 Fine */
    SRS1JN = 0x02640E12,    /* SRAW(SRAW2) + Jpeg Small1 Normal */
    SRS2JF = 0x02640F13,    /* SRAW(SRAW2) + Jpeg Small2 */
    SRS3JF = 0x02641013,    /* SRAW(SRAW2) + Jpeg Small3 */

    SRLJ = 0x02640010,  /* SRAW(SRAW2) + Jpeg Large */
    SRM1J = 0x02640510, /* SRAW(SRAW2) + Jpeg Middle1 */
    SRM2J = 0x02640610, /* SRAW(SRAW2) + Jpeg Middle2 */
    SRSJ = 0x02640210,  /* SRAW(SRAW2) + Jpeg Small */

    /* CRAW + Jpeg */
    CR = 0x0063ff0f,    /* CRAW */
    CRLJF = 0x00630013, /* CRAW + Jpeg Large Fine */
    CRMJF = 0x00630113, /* CRAW + Jpeg Middle Fine  */
    CRM1JF = 0x00630513,    /* CRAW + Jpeg Middle1 Fine  */
    CRM2JF = 0x00630613,    /* CRAW + Jpeg Middle2 Fine  */
    CRSJF = 0x00630213, /* CRAW + Jpeg Small Fine  */
    CRS1JF = 0x00630E13,    /* CRAW + Jpeg Small1 Fine  */
    CRS2JF = 0x00630F13,    /* CRAW + Jpeg Small2 Fine  */
    CRS3JF = 0x00631013,    /* CRAW + Jpeg Small3 Fine  */
    CRLJN = 0x00630012, /* CRAW + Jpeg Large Normal */
    CRMJN = 0x00630112, /* CRAW + Jpeg Middle Normal */
    CRM1JN = 0x00630512,    /* CRAW + Jpeg Middle1 Normal */
    CRM2JN = 0x00630612,    /* CRAW + Jpeg Middle2 Normal */
    CRSJN = 0x00630212, /* CRAW + Jpeg Small Normal */
    CRS1JN = 0x00630E12,    /* CRAW + Jpeg Small1 Normal */

    CRLJ = 0x00630010,  /* CRAW + Jpeg Large */
    CRM1J = 0x00630510, /* CRAW + Jpeg Middle1 */
    CRM2J = 0x00630610, /* CRAW + Jpeg Middle2 */
    CRSJ = 0x00630210,  /* CRAW + Jpeg Small */

    /* HEIF */
    HEIFL = 0x0080ff0f, /* HEIF Large */
    RHEIFL = 0x00640080, /* RAW  + HEIF Large */
    CRHEIFL = 0x00630080, /* CRAW + HEIF Large */

    HEIFLF = 0x0083ff0f, /* HEIF Large Fine */
    HEIFLN = 0x0082ff0f, /* HEIF Large Normal */
    HEIFMF = 0x0183ff0f, /* HEIF Middle Fine */
    HEIFMN = 0x0182ff0f, /* HEIF Middle Normal */
    HEIFS1F = 0x0e83ff0f, /* HEIF Small1 Fine */
    HEIFS1N = 0x0e82ff0f, /* HEIF Small1 Normal */
    HEIFS2F = 0x0f83ff0f, /* HEIF Small2 Fine */
    RHEIFLF = 0x00640083, /* RAW + HEIF Large Fine */
    RHEIFLN = 0x00640082, /* RAW + HEIF Large Normal */
    RHEIFMF = 0x00640183, /* RAW + HEIF Middle Fine */
    RHEIFMN = 0x00640182, /* RAW + HEIF Middle Normal */
    RHEIFS1F = 0x00640e83, /* RAW + HEIF Small1 Fine */
    RHEIFS1N = 0x00640e82, /* RAW + HEIF Small1 Normal */
    RHEIFS2F = 0x00640f83, /* RAW + HEIF Small2 Fine */
    CRHEIFLF = 0x00630083, /* CRAW + HEIF Large Fine */
    CRHEIFLN = 0x00630082, /* CRAW + HEIF Large Normal */
    CRHEIFMF = 0x00630183, /* CRAW + HEIF Middle Fine */
    CRHEIFMN = 0x00630182, /* CRAW + HEIF Middle Normal */
    CRHEIFS1F = 0x00630e83, /* CRAW + HEIF Small1 Fine */
    CRHEIFS1N = 0x00630e82, /* CRAW + HEIF Small1 Normal */
    CRHEIFS2F = 0x00630f83, /* CRAW + HEIF Small2 Fine */

    Unknown = 0xffffffff,
}

public enum EdsImageFormat
    : uint
{
    Unknown = 0x00000000,
    Jpeg = 0x00000001,
    CRW = 0x00000002,
    RAW = 0x00000004,
    CR2 = 0x00000006,
}

public enum EdsImageSize
    : uint
{
    Large = 0,
    Middle = 1,
    Small = 2,
    Middle1 = 5,
    Middle2 = 6,
    Unknown = 0xFFFFFFFF,
}

public enum EdsCompressQuality
    : uint
{
    Normal = 2,
    Fine = 3,
    Lossless = 4,
    SuperFine = 5,
    Unknown = 0xffffffff,
}

/// <summary>
/// Camera commands
/// </summary>
public enum CameraCommand
    : uint
{
    TakePicture = 0x00000000,
    ExtendShutDownTimer = 0x00000001,
    BulbStart = 0x00000002,
    BulbEnd = 0x00000003,
    DoEvfAf = 0x00000102,
    DriveLensEvf = 0x00000103,
    DoClickWBEvf = 0x00000104,
    MovieSelectSwON = 0x00000107,
    MovieSelectSwOFF = 0x00000108,
    PressShutterButton = 0x00000004,
    SetRemoteShootingMode = 0x0000010f,
    RequestRollPitchLevel = 0x00000109,
}

/// <summary>
/// Camera status command
/// </summary>
public enum CameraState
{
    UILock = 0x00000000,
    UIUnLock = 0x00000001,
    EnterDirectTransfer = 0x00000002,
    ExitDirectTransfer = 0x00000003,
}

public enum AEMode
    : uint
{
    Program = 0,
    Tv = 1,
    Av = 2,
    Mamual = 3,
    Bulb = 4,
    A_DEP = 5,
    DEP = 6,
    Custom = 7,
    Lock = 8,
    Green = 9,
    NigntPortrait = 10,
    Sports = 11,
    Portrait = 12,
    Landscape = 13,
    Closeup = 14,
    FlashOff = 15,
    CreativeAuto = 19,
    Movie = 20,
    PhotoInMovie = 21,
    SceneIntelligentAuto = 22,
    SCN = 25,
    HandheldNightScenes = 23,
    Hdr_BacklightControl = 24,
    Children = 26,
    Food = 27,
    CandlelightPortraits = 28,
    CreativeFilter = 29,
    RoughMonoChrome = 30,
    SoftFocus = 31,
    ToyCamera = 32,
    Fisheye = 33,
    WaterColor = 34,
    Miniature = 35,
    Hdr_Standard = 36,
    Hdr_Vivid = 37,
    Hdr_Bold = 38,
    Hdr_Embossed = 39,
    Movie_Fantasy = 40,
    Movie_Old = 41,
    Movie_Memory = 42,
    Movie_DirectMono = 43,
    Movie_Mini = 44,
    Panning = 45,
    GroupPhoto = 46,
    SelfPortrait = 50,
    PlusMovieAuto = 51,
    SmoothSkin = 52,
    Panorama = 53,
    Silent = 54,
    Flexible = 55,
    OilPainting = 56,
    Fireworks = 57,
    StarPortrait = 58,
    StarNightscape = 59,
    StarTrails = 60,
    StarTimelapseMovie = 61,
    BackgroundBlur = 62,
    Unknown = 0xffffffff,
}

public enum PictureStyle
    : uint
{
    Standard = 0x0081,
    Portrait = 0x0082,
    Landscape = 0x0083,
    Neutral = 0x0084,
    Faithful = 0x0085,
    Monochrome = 0x0086,
    Auto = 0x0087,
    FineDetail = 0x0088,
    User1 = 0x0021,
    User2 = 0x0022,
    User3 = 0x0023,
    PC1 = 0x0041,
    PC2 = 0x0042,
    PC3 = 0x0043,
}

public enum ColorSpace
    : uint
{
    sRGB = 1,
    AdobeRGB = 2,
    Unknown = 0xffffffff,
}

public enum WhiteBalanace
    : uint
{
    Click = 0xFFFFFFFF,
    Auto = 0,
    Daylight = 1,
    Cloudy = 2,
    Tungsten = 3,
    Fluorescent = 4,
    Strobe = 5,
    Shade = 8,
    ColorTemp = 9,
    Manual1 = 6,
    Manual2 = 15,
    Manual3 = 16,
    Manual4 = 18,
    Manual5 = 19,
    PCSet1 = 10,
    PCSet2 = 11,
    PCSet3 = 12,
    PCSet4 = 20,
    PCSet5 = 21,
    AwbWhite = 23,
}

public enum BatteryLevel
    : uint
{
    Empty = 1,
    Low = 30,
    Half = 50,
    Normal = 80,
    AC = 0xFFFFFFFF,
}

public enum EvfDriveLens
    : uint
{
    Near1 = 0x00000001,
    Near2 = 0x00000002,
    Near3 = 0x00000003,
    Far1 = 0x00008001,
    Far2 = 0x00008002,
    Far3 = 0x00008003,
}

/// <summary>
/// Depth of Field Preview
/// </summary>
public enum EvfDepthOfFieldPreview
    : uint
{
    OFF = 0x00000000,
    ON = 0x00000001,
}

/// <summary>
/// EVF Output Device [Flag]
/// </summary>
public enum EvfOutputDevice
    : uint
{
    Off = 0,
    TFT = 1,
    PC = 2,
}

/// <summary>
/// EVF Zoom
/// </summary>
public enum Zoom
    : uint
{
    Fit = 1,
    x5 = 5,
    x10 = 10,
}

/// <summary>
/// Video record mode
/// </summary>
public enum VideoRecordStatus
    : uint
{
    End_movie_shooting = 0,
    //NOTE: This is not documented anywhere, purely taken from the original implementation
    Movie_shooting_ready = 3,
    Begin_movie_shooting = 4,
}

public enum EdsEvent
    : uint
{
    /// <summary>
    /// Notifies all object events.
    /// </summary>
    All = 0x00000200,
    /// <summary>
    /// Notifies that the volume object (memory card) state (VolumeInfo) has been changed. Changed objects are indicated by event data.
    /// The changed value can be retrieved by means of EdsGetVolumeInfo. Notification of this event is not issued for type 1 protocol standard cameras.
    /// </summary>
    VolumeInfoChanged = 0x00000201,
    /// <summary>
    /// Notifies if the designated volume on a camera has been formatted.
    /// If notification of this event is received, get sub-items of the designated volume again as needed.
    /// Changed volume objects can be retrieved from event data. Objects cannot be identified on cameras earlier than the D30 if files are added or deleted.
    /// Thus, these events are subject to notification.
    /// </summary>
    VolumeUpdateItems = 0x00000202,
    /// <summary>
    /// Notifies if many images are deleted in a designated folder on a camera.
    /// If notification of this event is received, get sub-items of the designated folder again as needed.
    /// Changed folders (specifically, directory item objects) can be retrieved from event data.
    /// </summary>
    FolderUpdateItems = 0x00000203,
    /// <summary>
    /// Notifies of the creation of objects such as new folders or files on a camera compact flash card or the like.
    /// This event is generated if the camera has been set to store captured images simultaneously on the camera and a computer, for example, but not if the camera is set to store images on the computer alone.
    /// Newly created objects are indicated by event data. Because objects are not indicated for type 1 protocol standard cameras, (that is, objects are indicated as NULL), you must again retrieve child objects under the camera object to identify the new objects.
    /// </summary>
    DirItemCreated = 0x00000204,
    /// <summary>
    /// Notifies of the deletion of objects such as folders or files on a camera compact flash card or the like. Deleted objects are indicated in event data.
    /// Because objects are not indicated for type 1 protocol standard cameras, you must again retrieve child objects under the camera object to identify deleted objects.
    /// </summary>
    DirItemRemoved = 0x00000205,
    /// <summary>
    /// Notifies that information of DirItem objects has been changed. Changed objects are indicated by event data.
    /// The changed value can be retrieved by means of EdsGetDirectoryItemInfo. Notification of this event is not issued for type 1 protocol standard cameras.
    /// </summary>
    DirItemInfoChanged = 0x00000206,
    /// <summary>
    /// Notifies that header information has been updated, as for rotation information of image files on the camera.
    /// If this event is received, get the file header information again, as needed. This function is for type 2 protocol standard cameras only.
    /// </summary>
    DirItemContentChanged = 0x00000207,
    /// <summary>
    /// Notifies that there are objects on a camera to be transferred to a computer.
    /// This event is generated after remote release from a computer or local release from a camera.
    /// If this event is received, objects indicated in the event data must be downloaded.
    /// Furthermore, if the application does not require the objects, instead of downloading them, execute EdsDownloadCancel and release resources held by the camera.
    /// The order of downloading from type 1 protocol standard cameras must be the order in which the events are received.
    /// </summary>
    DirItemRequestTransfer = 0x00000208,
    /// <summary>
    /// Notifies if the camera's direct transfer button is pressed. If this event is received, objects indicated in the event data must be downloaded.
    /// Furthermore, if the application does not require the objects, instead of downloading them, execute EdsDownloadCancel and release resources held by the camera.
    /// Notification of this event is not issued for type 1 protocol standard cameras.
    /// </summary>
    DirItemRequestTransferDT = 0x00000209,
    /// <summary>
    /// Notifies of requests from a camera to cancel object transfer if the button to cancel direct transfer is pressed on the camera.
    /// If the parameter is 0, it means that cancellation of transfer is requested for objects still not downloaded, with these objects indicated by kEdsObjectEvent_DirItemRequestTransferDT.
    /// Notification of this event is not issued for type 1 protocol standard cameras.
    /// </summary>
    DirItemCancelTransferDT = 0x0000020a,
    VolumeAdded = 0x0000020c,
    VolumeRemoved = 0x0000020d,
}

public enum PropertyEvent
{
    /// <summary>
    /// Notifies all property events.
    /// </summary>
    All = 0x00000100,
    /// <summary>
    /// Notifies that a camera property value has been changed. The changed property can be retrieved from event data.
    /// The changed value can be retrieved by means of EdsGetPropertyData. In the case of type 1 protocol standard cameras, notification of changed properties can only be issued for custom functions (CFn).
    /// If the property type is 0x0000FFFF, the changed property cannot be identified. Thus, retrieve all required properties repeatedly.
    /// </summary>
    PropertyChanged = 0x00000101,
    /// <summary>
    /// Notifies of changes in the list of camera properties with configurable values. The list of configurable values for property IDs indicated in event data can be retrieved by means of EdsGetPropertyDesc.
    /// For type 1 protocol standard cameras, the property ID is identified as "Unknown" during notification.
    /// Thus, you must retrieve a list of configurable values for all properties and retrieve the property values repeatedly.
    /// (For details on properties for which you can retrieve a list of configurable properties, see the description of EdsGetPropertyDesc).
    /// </summary>
    PropertyDescChanged = 0x00000102,
}

public enum StateEvent
    : uint
{
    /// <summary>
    /// Notifies all state events.
    /// </summary>
    All = 0x00000300,
    /// <summary>
    /// Indicates that a camera is no longer connected to a computer, whether it was disconnected by unplugging a cord, opening the compact flash compartment, turning the camera off, auto shut-off, or by other means.
    /// </summary>
    Shutdown = 0x00000301,
    /// <summary>
    /// Notifies of whether or not there are objects waiting to be transferred to a host computer. This is useful when ensuring all shot images have been transferred when the application is closed.
    /// Notification of this event is not issued for type 1 protocol standard cameras.
    /// </summary>
    JobStatusChanged = 0x00000302,
    /// <summary>
    /// Notifies that the camera will shut down after a specific period. Generated only if auto shut-off is set.
    /// Exactly when notification is issued (that is, the number of seconds until shutdown) varies depending on the camera model.
    /// To continue operation without having the camera shut down, use EdsSendCommand to extend the auto shut-off timer.
    /// The time in seconds until the camera shuts down is returned as the initial value.
    /// </summary>
    WillSoonShutDown = 0x00000303,
    /// <summary>
    /// As the counterpart event to kEdsStateEvent_WillSoonShutDown, this event notifies of updates to the number of seconds until a camera shuts down.
    /// After the update, the period until shutdown is model-dependent.
    /// </summary>
    ShutDownTimerUpdate = 0x00000304,
    /// <summary>
    /// Notifies that a requested release has failed, due to focus failure or similar factors.
    /// </summary>
    CaptureError = 0x00000305,
    /// <summary>
    /// Notifies of internal SDK errors. If this error event is received, the issuing device will probably not be able to continue working properly, so cancel the remote connection.
    /// </summary>
    InternalError = 0x00000306,
    AfResult = 0x00000309,
}

public enum SDKError
    : uint
{
    /// <summary>
    /// ED-SDK Function Success Code
    /// </summary>
    OK = 0x00000000,

    /* Miscellaneous errors */
    UNIMPLEMENTED = 0x00000001,
    INTERNAL_ERROR = 0x00000002,
    MEM_ALLOC_FAILED = 0x00000003,
    MEM_FREE_FAILED = 0x00000004,
    OPERATION_CANCELLED = 0x00000005,
    INCOMPATIBLE_VERSION = 0x00000006,
    NOT_SUPPORTED = 0x00000007,
    UNEXPECTED_EXCEPTION = 0x00000008,
    PROTECTION_VIOLATION = 0x00000009,
    MISSING_SUBCOMPONENT = 0x0000000A,
    SELECTION_UNAVAILABLE = 0x0000000B,

    /* File errors */
    FILE_IO_ERROR = 0x00000020,
    FILE_TOO_MANY_OPEN = 0x00000021,
    FILE_NOT_FOUND = 0x00000022,
    FILE_OPEN_ERROR = 0x00000023,
    FILE_CLOSE_ERROR = 0x00000024,
    FILE_SEEK_ERROR = 0x00000025,
    FILE_TELL_ERROR = 0x00000026,
    FILE_READ_ERROR = 0x00000027,
    FILE_WRITE_ERROR = 0x00000028,
    FILE_PERMISSION_ERROR = 0x00000029,
    FILE_DISK_FULL_ERROR = 0x0000002A,
    FILE_ALREADY_EXISTS = 0x0000002B,
    FILE_FORMAT_UNRECOGNIZED = 0x0000002C,
    FILE_DATA_CORRUPT = 0x0000002D,
    FILE_NAMING_NA = 0x0000002E,

    /* Directory errors */
    DIR_NOT_FOUND = 0x00000040,
    DIR_IO_ERROR = 0x00000041,
    DIR_ENTRY_NOT_FOUND = 0x00000042,
    DIR_ENTRY_EXISTS = 0x00000043,
    DIR_NOT_EMPTY = 0x00000044,

    /* Property errors */
    PROPERTIES_UNAVAILABLE = 0x00000050,
    PROPERTIES_MISMATCH = 0x00000051,
    PROPERTIES_NOT_LOADED = 0x00000053,

    /* Function Parameter errors */
    INVALID_PARAMETER = 0x00000060,
    INVALID_HANDLE = 0x00000061,
    INVALID_POINTER = 0x00000062,
    INVALID_INDEX = 0x00000063,
    INVALID_LENGTH = 0x00000064,
    INVALID_FN_POINTER = 0x00000065,
    INVALID_SORT_FN = 0x00000066,

    /* Device errors */
    DEVICE_NOT_FOUND = 0x00000080,
    DEVICE_BUSY = 0x00000081,
    DEVICE_INVALID = 0x00000082,
    DEVICE_EMERGENCY = 0x00000083,
    DEVICE_MEMORY_FULL = 0x00000084,
    DEVICE_INTERNAL_ERROR = 0x00000085,
    DEVICE_INVALID_PARAMETER = 0x00000086,
    DEVICE_NO_DISK = 0x00000087,
    DEVICE_DISK_ERROR = 0x00000088,
    DEVICE_CF_GATE_CHANGED = 0x00000089,
    DEVICE_DIAL_CHANGED = 0x0000008A,
    DEVICE_NOT_INSTALLED = 0x0000008B,
    DEVICE_STAY_AWAKE = 0x0000008C,
    DEVICE_NOT_RELEASED = 0x0000008D,

    /* Stream errors */
    STREAM_IO_ERROR = 0x000000A0,
    STREAM_NOT_OPEN = 0x000000A1,
    STREAM_ALREADY_OPEN = 0x000000A2,
    STREAM_OPEN_ERROR = 0x000000A3,
    STREAM_CLOSE_ERROR = 0x000000A4,
    STREAM_SEEK_ERROR = 0x000000A5,
    STREAM_TELL_ERROR = 0x000000A6,
    STREAM_READ_ERROR = 0x000000A7,
    STREAM_WRITE_ERROR = 0x000000A8,
    STREAM_PERMISSION_ERROR = 0x000000A9,
    STREAM_COULDNT_BEGIN_THREAD = 0x000000AA,
    STREAM_BAD_OPTIONS = 0x000000AB,
    STREAM_END_OF_STREAM = 0x000000AC,

    /* Communications errors */
    COMM_PORT_IS_IN_USE = 0x000000C0,
    COMM_DISCONNECTED = 0x000000C1,
    COMM_DEVICE_INCOMPATIBLE = 0x000000C2,
    COMM_BUFFER_FULL = 0x000000C3,
    COMM_USB_BUS_ERR = 0x000000C4,

    /* Lock/Unlock */
    USB_DEVICE_LOCK_ERROR = 0x000000D0,
    USB_DEVICE_UNLOCK_ERROR = 0x000000D1,

    /* STI/WIA */
    STI_UNKNOWN_ERROR = 0x000000E0,
    STI_INTERNAL_ERROR = 0x000000E1,
    STI_DEVICE_CREATE_ERROR = 0x000000E2,
    STI_DEVICE_RELEASE_ERROR = 0x000000E3,
    DEVICE_NOT_LAUNCHED = 0x000000E4,

    ENUM_NA = 0x000000F0,
    INVALID_FN_CALL = 0x000000F1,
    HANDLE_NOT_FOUND = 0x000000F2,
    INVALID_ID = 0x000000F3,
    WAIT_TIMEOUT_ERROR = 0x000000F4,

    /* PTP */
    SESSION_NOT_OPEN = 0x00002003,
    INVALID_TRANSACTIONID = 0x00002004,
    INCOMPLETE_TRANSFER = 0x00002007,
    INVALID_STRAGEID = 0x00002008,
    DEVICEPROP_NOT_SUPPORTED = 0x0000200A,
    INVALID_OBJECTFORMATCODE = 0x0000200B,
    SELF_TEST_FAILED = 0x00002011,
    PARTIAL_DELETION = 0x00002012,
    SPECIFICATION_BY_FORMAT_UNSUPPORTED = 0x00002014,
    NO_VALID_OBJECTINFO = 0x00002015,
    INVALID_CODE_FORMAT = 0x00002016,
    UNKNOWN_VENDER_CODE = 0x00002017,
    CAPTURE_ALREADY_TERMINATED = 0x00002018,
    INVALID_PARENTOBJECT = 0x0000201A,
    INVALID_DEVICEPROP_FORMAT = 0x0000201B,
    INVALID_DEVICEPROP_VALUE = 0x0000201C,
    SESSION_ALREADY_OPEN = 0x0000201E,
    TRANSACTION_CANCELLED = 0x0000201F,
    SPECIFICATION_OF_DESTINATION_UNSUPPORTED = 0x00002020,
    UNKNOWN_COMMAND = 0x0000A001,
    OPERATION_REFUSED = 0x0000A005,
    LENS_COVER_CLOSE = 0x0000A006,
    LOW_BATTERY = 0x0000A101,
    OBJECT_NOTREADY = 0x0000A102,

    /* Capture Error */
    TAKE_PICTURE_AF_NG = 0x00008D01,
    TAKE_PICTURE_RESERVED = 0x00008D02,
    TAKE_PICTURE_MIRROR_UP_NG = 0x00008D03,
    TAKE_PICTURE_SENSOR_CLEANING_NG = 0x00008D04,
    TAKE_PICTURE_SILENCE_NG = 0x00008D05,
    TAKE_PICTURE_NO_CARD_NG = 0x00008D06,
    TAKE_PICTURE_CARD_NG = 0x00008D07,
    TAKE_PICTURE_CARD_PROTECT_NG = 0x00008D08,

    LAST_GENERIC_ERROR_PLUS_ONE = 0x000000F5,
}


[StructLayout(LayoutKind.Sequential)]
public record struct EdsPoint(int X, int Y);

[StructLayout(LayoutKind.Sequential)]
public record struct EdsRect(int X, int Y, int Width, int Height);

[StructLayout(LayoutKind.Sequential)]
public record struct EdsSize(int width, int height);

[StructLayout(LayoutKind.Sequential)]
public record struct EdsRational(int Numerator, uint Denominator);

[StructLayout(LayoutKind.Sequential)]
public record struct EdsTime(int Year, int Month, int Day, int Hour, int Minute, int Second, int Milliseconds);

[StructLayout(LayoutKind.Sequential)]
public struct EdsDeviceInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = EDSDK_API.EDS_MAX_NAME)]
    public string szPortName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = EDSDK_API.EDS_MAX_NAME)]
    public string szDeviceDescription;

    public uint DeviceSubType;

    public uint reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct EdsVolumeInfo
{
    public uint StorageType;
    public uint Access;
    public ulong MaxCapacity;
    public ulong FreeSpaceInBytes;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = EDSDK_API.EDS_MAX_NAME)]
    public string szVolumeLabel;
}

[StructLayout(LayoutKind.Sequential)]
public struct EdsDirectoryItemInfo
{
    public ulong Size;
    public int isFolder;
    public uint GroupID;
    public uint Option;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = EDSDK_API.EDS_MAX_NAME)]
    public string szFileName;

    public uint format;
    public uint dateTime;
}

[StructLayout(LayoutKind.Sequential)]
public struct EdsImageInfo
{
    public uint Width;                  // image width 
    public uint Height;                 // image height

    public uint NumOfComponents;        // number of color components in image.
    public uint ComponentDepth;         // bits per sample.  8 or 16.

    public EdsRect EffectiveRect;          // Effective rectangles except 
                                           // a black line of the image. 
                                           // A black line might be in the top and bottom
                                           // of the thumbnail image. 

    public uint reserved1;
    public uint reserved2;

}

[StructLayout(LayoutKind.Sequential)]
public struct EdsSaveImageSetting
{
    public uint JPEGQuality;
    private readonly nint iccProfileStream;
    public uint reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct EdsPropertyDesc
{
    public int Form;
    public uint Access;
    public int NumElements;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
    public int[] PropDesc;
}

/// <summary>
/// Picture Style Desc
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct EdsPictureStyleDesc
{
    public int contrast;
    public uint sharpness;
    public int saturation;
    public int colorTone;
    public uint filterEffect;
    public uint toningEffect;
    public uint sharpFineness;
    public uint sharpThreshold;
}

/// <summary>
/// Focus Info
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct EdsFocusPoint
{
    public uint valid;
    public uint selected;
    public uint justFocus;
    public EdsRect rect;
    public uint reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct EdsFocusInfo
{
    public EdsRect imageRect;
    public uint pointNumber;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1053)]
    public EdsFocusPoint[] focusPoint;
    public uint executeMode;
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public struct EdsCapacity
{
    public int NumberOfFreeClusters;
    public int BytesPerSector;
    public int Reset;
}

/// <summary>
/// Angle Information
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct EdsCameraPos
{
    public int status;
    public int position;
    public int rolling;
    public int pitching;
}

/// <summary>
/// Manual WhiteBalance Data
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct EdsManualWBData
{
    public uint Valid;
    public uint dataSize;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string szCaption;

    [MarshalAs(UnmanagedType.ByValArray)]
    public byte[] data;





    public byte[] ConvertMWB()
    {
        int headerSize = 40;
        int MWBHEADERSIZE = sizeof(uint) * 3;

        int datasize = (int)dataSize;

        // Since the pointer is copied by the following StructureToPtr,
        // if the data size is less than the pointer size, the buffer is enlarged.
        if (datasize < nint.Size)
            datasize = nint.Size;

        int size = (int)dataSize + MWBHEADERSIZE + headerSize;
        nint ptr = Marshal.AllocHGlobal(datasize + headerSize);

        dataSize += (uint)MWBHEADERSIZE;

        Marshal.StructureToPtr(this, ptr, true);

        dataSize -= (uint)MWBHEADERSIZE;

        byte[] buff = new byte[size];

        Marshal.Copy(ptr, buff, 0, headerSize);


        int i;

        for (i = 0; i < MWBHEADERSIZE; i++)
            buff[headerSize + i] = 0;

        for (int j = 0; j < dataSize; j++)
            buff[headerSize + i + j] = data[j];

        Marshal.FreeHGlobal(ptr);

        return buff;
    }

    public static EdsManualWBData MarshalPtrToManualWBData(nint ptr)
    {
        EdsManualWBData userdata = (EdsManualWBData)Marshal.PtrToStructure(ptr, typeof(EdsManualWBData));

        int headerSize = 40;
        byte[] tmp = new byte[userdata.dataSize + headerSize];

        userdata.data = new byte[userdata.dataSize];

        Marshal.Copy(ptr, tmp, 0, (int)userdata.dataSize + headerSize);

        for (int i = 0; i < userdata.dataSize; i++)
            userdata.data[i] = tmp[headerSize + i];

        return userdata;
    }
}

public static unsafe class EDSDK_API
{
    /// <summary>
    /// Path to the EDSDK DLL
    /// </summary>
    private const string _DLL_PATH = "EDSDK.dll";


    private static nint CheckValidCamera(Camera? camera) => camera?.Handle is 0 or null ? throw new ArgumentNullException(nameof(camera), "Camera or camera reference is null/zero") : camera.Handle;

    #region BASIC SDK INIT/CTOR & DTOR FUNCTIONS

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsInitializeSDK
    //
    //  Description:
    //      Initializes the libraries. 
    //      When using the EDSDK libraries, you must call this API once  
    //          before using EDSDK APIs.
    //
    //  Parameters:
    //       In:    None
    //      Out:    None
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsInitializeSDK();

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsTerminateSDK
    //
    //  Description:
    //      Terminates use of the libraries. 
    //      This function muse be called when ending the SDK.
    //      Calling this function releases all resources allocated by the libraries.
    //
    //  Parameters:
    //       In:    None
    //      Out:    None
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsTerminateSDK();

    #endregion
    #region REFERENCE-COUNTER OPERATING FUNCTIONS

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsRetain
    //
    //  Description:
    //      Increments the reference counter of existing objects.
    //
    //  Parameters:
    //       In:    inRef - The reference for the item.
    //      Out:    None
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsRetain(nint inRef);

    /// <summary>
    /// Decrements the reference counter to an object. When the reference counter reaches 0, the object is released.
    /// </summary>
    /// <param name="reference">The reference of the item.</param>
    /// <returns>Any of the SDK errors.</returns>
    public static SDKError Release(nint reference)
    {
        [DllImport(_DLL_PATH)]
        static extern SDKError EdsRelease(nint inRef);


        return reference != 0 ? EdsRelease(reference) : SDKError.OK;
    }

    #endregion
    #region ITEM-TREE OPERATING FUNCTIONS

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsGetChildCount
    //
    //  Description:
    //      Gets the number of child objects of the designated object.
    //      Example: Number of files in a directory
    //
    //  Parameters:
    //       In:    inRef - The reference of the list.
    //      Out:    outCount - Number of elements in this list.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsGetChildCount(nint inRef, out int outCount);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsGetChildAtIndex
    //
    //  Description:
    //       Gets an indexed child object of the designated object. 
    //
    //  Parameters:
    //       In:    inRef - The reference of the item.
    //              inIndex -  The index that is passed in, is zero based.
    //      Out:    outRef - The pointer which receives reference of the 
    //                           specified index .
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsGetChildAtIndex(nint inRef, int inIndex, out nint outRef);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsGetParent
    //
    //  Description:
    //      Gets the parent object of the designated object.
    //
    //  Parameters:
    //       In:    inRef        - The reference of the item.
    //      Out:    outParentRef - The pointer which receives reference.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern uint EdsGetParent(nint inRef, out nint outParentRef);

    #endregion
    #region PROPERTY OPERATING FUNCTIONS

    /// <summary>
    /// Gets a list of property data that can be set for the object designated in inRef, as well as maximum and minimum values.
    /// This API is intended for only some shooting-related properties.
    /// </summary>
    /// <param name="camera">The reference of the camera</param>
    /// <param name="property">The property.</param>
    /// <param name="outPropertyDesc">Array of the value which can be set up</param>
    /// <returns>An SDK error status.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static SDKError GetPropertyDesc(Camera? camera, SDKProperty property, out EdsPropertyDesc outPropertyDesc)
    {
        [DllImport(_DLL_PATH)]
        static extern SDKError EdsGetPropertyDesc(nint camera, SDKProperty property, out EdsPropertyDesc outPropertyDesc);

        return EdsGetPropertyDesc(CheckValidCamera(camera), property, out outPropertyDesc);
    }

    /// <summary>
    /// Gets the byte size and data type of a designated property from a camera object or image object.
    /// </summary>
    /// <param name="camera">The reference of the camera</param>
    /// <param name="property">The property.</param>
    /// <param name="param"> Additional information of property. We use this parameter in order to specify an index in case there are two or more values over the same ID.</param>
    /// <returns>An SDK error status.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static SDKError GetPropertySize(Camera? camera, SDKProperty property, out EdsDataType type, out int size)
    {
        [DllImport(_DLL_PATH)]
        static extern SDKError EdsGetPropertySize(nint camera, SDKProperty property, int param, out EdsDataType type, out int size);

        return EdsGetPropertySize(CheckValidCamera(camera), property, 0, out type, out size);
    }

    /// <summary>
    /// Gets property information from the object designated in inRef.
    /// </summary>
    /// <param name="camera">The camera reference</param>
    /// <param name="property">The property</param>
    /// <param name="param">Additional information of property. We use this parameter in order to specify an index in case there are two or more values over the same ID.</param>
    /// <param name="inPropertySize">The number of bytes of the prepared buffer for receive property-value.</param>
    /// <param name="outPropertyData">The buffer pointer to receive property-value.</param>
    /// <returns></returns>
    public static SDKError GetPropertyData(Camera? camera, SDKProperty property, int size, nint buffer)
    {
        [DllImport(_DLL_PATH)]
        static extern SDKError EdsGetPropertyData(nint camera, SDKProperty property, int param, int size, nint buffer);

        return EdsGetPropertyData(CheckValidCamera(camera), property, 0, size, buffer);
    }

    public static SDKError GetPropertyData<T>(Camera? camera, SDKProperty property, out T data)
        where T : unmanaged
    {
        T value = default;
        SDKError error = GetPropertyData(camera, property, sizeof(T), (nint)(void*)&value);

        data = value;

        return error;
    }

    public static SDKError GetPropertyData(Camera? camera, SDKProperty property, out EdsFocusInfo data)
    {
        int size = Marshal.SizeOf(typeof(EdsFocusInfo));
        nint ptr = Marshal.AllocHGlobal(size);
        SDKError err = GetPropertyData(camera, property, size, ptr);

        data = (EdsFocusInfo)Marshal.PtrToStructure(ptr, typeof(EdsFocusInfo));
        Marshal.FreeHGlobal(ptr);
        return err;
    }

    public static SDKError GetPropertyData(Camera? camera, SDKProperty property, out string data)
    {
        nint ptr = Marshal.AllocHGlobal(256);
        SDKError err = GetPropertyData(camera, property, 256, ptr);

        data = Marshal.PtrToStringAnsi(ptr);
        Marshal.FreeHGlobal(ptr);

        return err;
    }

    public static SDKError GetPropertyData(Camera? camera, SDKProperty property, out int[] data)
    {
        GetPropertySize(camera, property, out _, out int size);

        nint ptr = Marshal.AllocHGlobal(size);
        SDKError err = GetPropertyData(camera, property, size, ptr);
        int len = size / 4;

        data = new int[len];

        Marshal.Copy(ptr, data, 0, len);
        Marshal.FreeHGlobal(ptr);

        return err;
    }

    public static SDKError GetPropertyData(Camera? camera, SDKProperty property, out byte[] data)
    {
        GetPropertySize(camera, property, out _, out int size);

        nint ptr = Marshal.AllocHGlobal(size);
        SDKError err = GetPropertyData(camera, property, size, ptr);

        int len = size;
        data = new byte[len];

        Marshal.Copy(ptr, data, 0, len);
        Marshal.FreeHGlobal(ptr);
        return err;
    }

    ///// <summary>
    ///// Sets property data for the object designated in inRef.
    ///// </summary>
    ///// <param name="camera">The reference of the item</param>
    ///// <param name="property">The ProprtyID</param>
    ///// <param name="param">Additional information of property.</param>
    ///// <param name="size">The number of bytes of the prepared buffer for set property-value.</param>
    ///// <param name="data">The buffer pointer to set property-value.</param>
    ///// <returns>An SDK error status.</returns>
    ///// <exception cref="ArgumentNullException"></exception>
    //public static SDKError SetPropertyData(Camera? camera, SDKProperty property, int param, int size, nint buffer)
    //{
    //    [DllImport(_DLL_PATH)]
    //    static extern SDKError EdsSetPropertyData(nint camera, SDKProperty property, int param, int size, nint buffer);
    //
    //    return EdsSetPropertyData(CheckValidCamera(camera), property, param, size, buffer);
    //}

    /// <summary>
    /// Sets property data for the object designated in inRef.
    /// </summary>
    /// <param name="camera">The reference of the item</param>
    /// <param name="property">The ProprtyID</param>
    /// <param name="param">Additional information of property.</param>
    /// <param name="size">The number of bytes of the prepared buffer for set property-value.</param>
    /// <param name="data">The buffer pointer to set property-value.</param>
    /// <returns>An SDK error status.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static SDKError SetPropertyData(Camera? camera, SDKProperty property, int size, in object data)
    {
        [DllImport(_DLL_PATH)]
        static extern SDKError EdsSetPropertyData(nint camera, SDKProperty property, int param, int size, [MarshalAs(UnmanagedType.AsAny), In] object data);

        return EdsSetPropertyData(CheckValidCamera(camera), property, 0, size, data);
    }

    #endregion
    #region
    #endregion
    #region
    #endregion
    #region
    #endregion
    #region
    #endregion



    /*--------------------------------------------
      Device-list and device operating functions
    ---------------------------------------------*/
    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsGetCameraList
    //
    //  Description:
    //      Gets camera list objects.
    //
    //  Parameters:
    //       In:    None
    //      Out:    outCameraListRef - Pointer to the camera-list.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsGetCameraList(out nint outCameraListRef);

    /*--------------------------------------------
      Camera operating functions
    ---------------------------------------------*/
    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsGetDeviceInfo
    //
    //  Description:
    //      Gets device information, such as the device name.  
    //      Because device information of remote cameras is stored 
    //          on the host computer, you can use this API 
    //          before the camera object initiates communication
    //          (that is, before a session is opened). 
    //
    //  Parameters:
    //       In:    camera - The reference of the camera.
    //      Out:    outDeviceInfo - Information as device of camera.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsGetDeviceInfo(nint camera, out EdsDeviceInfo outDeviceInfo);




    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsOpenSession
    //
    //  Description:
    //      Establishes a logical connection with a remote camera. 
    //      Use this API after getting the camera's EdsCamera object.
    //
    //  Parameters:
    //       In:    camera - The reference of the camera 
    //      Out:    None
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    public static SDKError OpenSession(Camera? camera)
    {
        [DllImport(_DLL_PATH)]
        static extern SDKError EdsOpenSession(nint camera);

        return EdsOpenSession(CheckValidCamera(camera));
    }

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsCloseSession
    //
    //  Description:
    //       Closes a logical connection with a remote camera.
    //
    //  Parameters:
    //       In:    camera - The reference of the camera 
    //      Out:    None
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    public static SDKError CloseSession(Camera? camera)
    {
        [DllImport(_DLL_PATH)]
        static extern SDKError EdsCloseSession(nint camera);

        return camera is Camera c ? EdsCloseSession(CheckValidCamera(c)) : SDKError.OK;
    }




    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsSendCommand
    //
    //  Description:
    //       Sends a command such as "Shoot" to a remote camera. 
    //
    //  Parameters:
    //       In:    camera - The reference of the camera which will receive the 
    //                      command.
    //              inCommand - Specifies the command to be sent.
    //              param -     Specifies additional command-specific information.
    //      Out:    None
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsSendCommand(nint camera, CameraCommand inCommand, int param);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsSendStatusCommand
    //
    //  Description:
    //       Sets the remote camera state or mode.
    //
    //  Parameters:
    //       In:    camera - The reference of the camera which will receive the 
    //                      command.
    //              inStatusCommand - Specifies the command to be sent.
    //              param -     Specifies additional command-specific information.
    //      Out:    None
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsSendStatusCommand(nint camera, CameraState cameraState, int param);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsSetCapacity
    //
    //  Description:
    //      Sets the remaining HDD capacity on the host computer
    //          (excluding the portion from image transfer),
    //          as calculated by subtracting the portion from the previous time. 
    //      Set a reset flag initially and designate the cluster length 
    //          and number of free clusters.
    //      Some type 2 protocol standard cameras can display the number of shots 
    //          left on the camera based on the available disk capacity 
    //          of the host computer. 
    //      For these cameras, after the storage destination is set to the computer, 
    //          use this API to notify the camera of the available disk capacity 
    //          of the host computer.
    //
    //  Parameters:
    //       In:    camera - The reference of the camera which will receive the 
    //                      command.
    //              inCapacity -  The remaining capacity of a transmission place.
    //      Out:    None
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsSetCapacity(nint camera, EdsCapacity inCapacity);


    /*--------------------------------------------
      Volume operating functions
    ---------------------------------------------*/
    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsGetVolumeInfo
    //
    //  Description:
    //      Gets volume information for a memory card in the camera.
    //
    //  Parameters:
    //       In:    inVolumeRef - The reference of the volume.
    //      Out:    outVolumeInfo - information of  the volume.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsGetVolumeInfo(nint camera, out EdsVolumeInfo outVolumeInfo);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsFormatVolume
    //
    //  Description:
    //       .
    //
    //  Parameters:
    //       In:    inVolumeRef - The reference of volume .
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsFormatVolume(nint inVolumeRef);


    /*--------------------------------------------
      Directory-item operating functions
    ---------------------------------------------*/
    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsGetDirectoryItemInfo
    //
    //  Description:
    //      Gets information about the directory or file objects 
    //          on the memory card (volume) in a remote camera.
    //
    //  Parameters:
    //       In:    inDirItemRef - The reference of the directory item.
    //      Out:    outDirItemInfo - information of the directory item.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsGetDirectoryItemInfo(nint inDirItemRef, out EdsDirectoryItemInfo outDirItemInfo);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsDeleteDirectoryItem
    //
    //  Description:
    //      Deletes a camera folder or file.
    //      If folders with subdirectories are designated, all files are deleted 
    //          except protected files. 
    //      EdsDirectoryItem objects deleted by means of this API are implicitly 
    //          released by the EDSDK. Thus, there is no need to release them 
    //          by means of EdsRelease.
    //
    //  Parameters:
    //       In:    inDirItemRef - The reference of the directory item.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsDeleteDirectoryItem(nint inDirItemRef);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsDownload
    //
    //  Description:
    //       Downloads a file on a remote camera 
    //          (in the camera memory or on a memory card) to the host computer. 
    //      The downloaded file is sent directly to a file stream created in advance. 
    //      When dividing the file being retrieved, call this API repeatedly. 
    //      Also in this case, make the data block size a multiple of 512 (bytes), 
    //          excluding the final block.
    //
    //  Parameters:
    //       In:    inDirItemRef - The reference of the directory item.
    //              inReadSize   - 
    //
    //      Out:    outStream    - The reference of the stream.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsDownload(nint inDirItemRef, ulong inReadSize, nint outStream);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsDownloadCancel
    //
    //  Description:
    //       Must be executed when downloading of a directory item is canceled. 
    //      Calling this API makes the camera cancel file transmission.
    //      It also releases resources. 
    //      This operation need not be executed when using EdsDownloadThumbnail. 
    //
    //  Parameters:
    //       In:    inDirItemRef - The reference of the directory item.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsDownloadCancel(nint inDirItemRef);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsDownloadComplete
    //
    //  Description:
    //       Must be called when downloading of directory items is complete. 
    //          Executing this API makes the camera 
    //              recognize that file transmission is complete. 
    //          This operation need not be executed when using EdsDownloadThumbnail.
    //
    //  Parameters:
    //       In:    inDirItemRef - The reference of the directory item.
    //
    //      Out:    outStream    - None.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsDownloadComplete(nint inDirItemRef);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsDownloadThumbnail
    //
    //  Description:
    //      Extracts and downloads thumbnail information from image files in a camera. 
    //      Thumbnail information in the camera's image files is downloaded 
    //          to the host computer. 
    //      Downloaded thumbnails are sent directly to a file stream created in advance.
    //
    //  Parameters:
    //       In:    inDirItemRef - The reference of the directory item.
    //
    //      Out:    outStream - The reference of the stream.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsDownloadThumbnail(nint inDirItemRef, nint outStream);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsGetAttribute
    //
    //  Description:
    //      Gets attributes of files on a camera.
    //  
    //  Parameters:
    //       In:    inDirItemRef - The reference of the directory item.
    //      Out:    outFileAttribute  - Indicates the file attributes. 
    //                  As for the file attributes, OR values of the value defined
    //                  by enum EdsFileAttributes can be retrieved. Thus, when 
    //                  determining the file attributes, you must check 
    //                  if an attribute flag is set for target attributes. 
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern uint EdsGetAttribute(nint inDirItemRef, out EdsFileAttribute outFileAttribute);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsSetAttribute
    //
    //  Description:
    //      Changes attributes of files on a camera.
    //  
    //  Parameters:
    //       In:    inDirItemRef - The reference of the directory item.
    //              inFileAttribute  - Indicates the file attributes. 
    //                      As for the file attributes, OR values of the value 
    //                      defined by enum EdsFileAttributes can be retrieved. 
    //      Out:    None
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern uint EdsSetAttribute(nint inDirItemRef, EdsFileAttribute inFileAttribute);

    /*--------------------------------------------
      Stream operating functions
    ---------------------------------------------*/
    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsCreateFileStream
    //
    //  Description:
    //      Creates a new file on a host computer (or opens an existing file) 
    //          and creates a file stream for access to the file. 
    //      If a new file is designated before executing this API, 
    //          the file is actually created following the timing of writing 
    //          by means of EdsWrite or the like with respect to an open stream.
    //
    //  Parameters:
    //       In:    inFileName - Pointer to a null-terminated string that specifies
    //                           the file name.
    //              inCreateDisposition - Action to take on files that exist, 
    //                                and which action to take when files do not exist.  
    //              inDesiredAccess - Access to the stream (reading, writing, or both).
    //      Out:    outStream - The reference of the stream.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsCreateFileStream(string inFileName, EdsFileCreateDisposition inCreateDisposition, EdsAccess inDesiredAccess, out nint outStream);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsCreateMemoryStream
    //
    //  Description:
    //      Creates a stream in the memory of a host computer. 
    //      In the case of writing in excess of the allocated buffer size, 
    //          the memory is automatically extended.
    //
    //  Parameters:
    //       In:    inBufferSize - The number of bytes of the memory to allocate.
    //      Out:    outStream - The reference of the stream.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsCreateMemoryStream(ulong inBufferSize, out nint outStream);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsCreateStreamEx
    //
    //  Description:
    //      An extended version of EdsCreateStreamFromFile. 
    //      Use this function when working with Unicode file names.
    //
    //  Parameters:
    //       In:    inFileName - Designate the file name. 
    //              inCreateDisposition - Action to take on files that exist, 
    //                                and which action to take when files do not exist.  
    //              inDesiredAccess - Access to the stream (reading, writing, or both).
    //
    //      Out:    outStream - The reference of the stream.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsCreateStreamEx(string inFileName, EdsFileCreateDisposition inCreateDisposition, EdsAccess inDesiredAccess, out nint outStream);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsCreateMemoryStreamFromPointer        
    //
    //  Description:
    //      Creates a stream from the memory buffer you prepare. 
    //      Unlike the buffer size of streams created by means of EdsCreateMemoryStream, 
    //      the buffer size you prepare for streams created this way does not expand.
    //
    //  Parameters:
    //       In:    inBufferSize - The number of bytes of the memory to allocate.
    //      Out:    outStream - The reference of the stream.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsCreateMemoryStreamFromPointer(nint inUserBuffer, ulong inBufferSize, out nint outStream);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsGetPointer
    //
    //  Description:
    //      Gets the pointer to the start address of memory managed by the memory stream. 
    //      As the EDSDK automatically resizes the buffer, the memory stream provides 
    //          you with the same access methods as for the file stream. 
    //      If access is attempted that is excessive with regard to the buffer size
    //          for the stream, data before the required buffer size is allocated 
    //          is copied internally, and new writing occurs. 
    //      Thus, the buffer pointer might be switched on an unknown timing. 
    //      Caution in use is therefore advised. 
    //
    //  Parameters:
    //       In:    inStream - Designate the memory stream for the pointer to retrieve. 
    //      Out:    outPointer - If successful, returns the pointer to the buffer 
    //                  written in the memory stream.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsGetPointer(nint inStreamRef, out nint outPointer);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsRead
    //
    //  Description:
    //      Reads data the size of inReadSize into the outBuffer buffer, 
    //          starting at the current read or write position of the stream. 
    //      The size of data actually read can be designated in outReadSize.
    //
    //  Parameters:
    //       In:    inStreamRef - The reference of the stream or image.
    //              inReadSize -  The number of bytes to read.
    //      Out:    outBuffer - Pointer to the user-supplied buffer that is to receive
    //                          the data read from the stream. 
    //              outReadSize - The actually read number of bytes.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsRead(nint inStreamRef, ulong inReadSize, nint outBuffer, out ulong outReadSize);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsWrite
    //
    //  Description:
    //      Writes data of a designated buffer 
    //          to the current read or write position of the stream. 
    //
    //  Parameters:
    //       In:    inStreamRef  - The reference of the stream or image.
    //              inWriteSize - The number of bytes to write.
    //              inBuffer - A pointer to the user-supplied buffer that contains 
    //                         the data to be written to the stream.
    //      Out:    outWrittenSize - The actually written-in number of bytes.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern uint EdsWrite(nint inStreamRef, ulong inWriteSize, nint inBuffer, out uint outWrittenSize);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsSeek
    //
    //  Description:
    //      Moves the read or write position of the stream
                (that is, the file position indicator).
    //
    //  Parameters:
    //       In:    inStreamRef  - The reference of the stream or image. 
    //              inSeekOffset - Number of bytes to move the pointer. 
    //              inSeekOrigin - Pointer movement mode. Must be one of the following 
    //                             values.
    //                  kEdsSeek_Cur     Move the stream pointer inSeekOffset bytes 
    //                                   from the current position in the stream. 
    //                  kEdsSeek_Begin   Move the stream pointer inSeekOffset bytes
    //                                   forward from the beginning of the stream. 
    //                  kEdsSeek_End     Move the stream pointer inSeekOffset bytes
    //                                   from the end of the stream. 
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern uint EdsSeek(nint inStreamRef, long inSeekOffset, EdsSeekOrigin inSeekOrigin);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsGetPosition
    //
    //  Description:
    //       Gets the current read or write position of the stream
    //          (that is, the file position indicator).
    //
    //  Parameters:
    //       In:    inStreamRef - The reference of the stream or image.
    //      Out:    outPosition - The current stream pointer.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern uint EdsGetPosition(nint inStreamRef, out ulong outPosition);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsGetLength
    //
    //  Description:
    //      Gets the stream size.
    //
    //  Parameters:
    //       In:    inStreamRef - The reference of the stream or image.
    //      Out:    outLength - The length of the stream.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsGetLength(nint inStreamRef, out ulong outLength);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsCopyData
    //
    //  Description:
    //      Copies data from the copy source stream to the copy destination stream. 
    //      The read or write position of the data to copy is determined from 
    //          the current file read or write position of the respective stream. 
    //      After this API is executed, the read or write positions of the copy source 
    //          and copy destination streams are moved an amount corresponding to 
    //          inWriteSize in the positive direction. 
    //
    //  Parameters:
    //       In:    inStreamRef - The reference of the stream or image.
    //              inWriteSize - The number of bytes to copy.
    //      Out:    outStreamRef - The reference of the stream or image.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern uint EdsCopyData(nint inStreamRef, ulong inWriteSize, nint outStreamRef);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsSetProgressCallback
    //
    //  Description:
    //      Register a progress callback function. 
    //      An event is received as notification of progress during processing that 
    //          takes a relatively long time, such as downloading files from a
    //          remote camera. 
    //      If you register the callback function, the EDSDK calls the callback
    //          function during execution or on completion of the following APIs. 
    //      This timing can be used in updating on-screen progress bars, for example.
    //
    //  Parameters:
    //       In:    inRef - The reference of the stream or image.
    //              inProgressCallback - Pointer to a progress callback function.
    //              inProgressOption - The option about progress is specified.
    //                              Must be one of the following values.
    //                         kEdsProgressOption_Done 
    //                             When processing is completed,a callback function
    //                             is called only at once.
    //                         kEdsProgressOption_Periodically
    //                             A callback function is performed periodically.
    //              inContext - Application information, passed in the argument 
    //                      when the callback function is called. Any information 
    //                      required for your program may be added. 
    //      Out:    None
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsSetProgressCallback(nint inRef, EdsProgressCallback inProgressFunc, EdsProgressOption inProgressOption, nint inContext);


    /*--------------------------------------------
      Image operating functions
    ---------------------------------------------*/
    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsCreateImageRef
    //
    //  Description:
    //      Creates an image object from an image file. 
    //      Without modification, stream objects cannot be worked with as images. 
    //      Thus, when extracting images from image files, 
    //          you must use this API to create image objects. 
    //      The image object created this way can be used to get image information 
    //          (such as the height and width, number of color components, and
    //           resolution), thumbnail image data, and the image data itself.
    //
    //  Parameters:
    //       In:    inStreamRef - The reference of the stream.
    //
    //       Out:    outImageRef - The reference of the image.
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsCreateImageRef(nint inStreamRef, out nint outImageRef);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsGetImageInfo
    //
    //  Description:
    //      Gets image information from a designated image object. 
    //      Here, image information means the image width and height, 
    //          number of color components, resolution, and effective image area.
    //
    //  Parameters:
    //       In:    inStreamRef - Designate the object for which to get image information. 
    //              inImageSource - Of the various image data items in the image file,
    //                  designate the type of image data representing the 
    //                  information you want to get. Designate the image as
    //                  defined in Enum EdsImageSource. 
    //
    //                      kEdsImageSrc_FullView
    //                                  The image itself (a full-sized image) 
    //                      kEdsImageSrc_Thumbnail
    //                                  A thumbnail image 
    //                      kEdsImageSrc_Preview
    //                                  A preview image
    //       Out:    outImageInfo - Stores the image data information designated 
    //                      in inImageSource. 
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsGetImageInfo(nint inImageRef, EdsImageSource inImageSource, out EdsImageInfo outImageInfo);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsGetImage                         
    //
    //  Description:
    //      Gets designated image data from an image file, in the form of a
    //          designated rectangle. 
    //      Returns uncompressed results for JPEGs and processed results 
    //          in the designated pixel order (RGB, Top-down BGR, and so on) for
    //           RAW images. 
    //      Additionally, by designating the input/output rectangle, 
    //          it is possible to get reduced, enlarged, or partial images. 
    //      However, because images corresponding to the designated output rectangle 
    //          are always returned by the SDK, the SDK does not take the aspect 
    //          ratio into account. 
    //      To maintain the aspect ratio, you must keep the aspect ratio in mind 
    //          when designating the rectangle. 
    //
    //  Parameters:
    //      In:     
    //              inImageRef - Designate the image object for which to get 
    //                      the image data.
    //              inImageSource - Designate the type of image data to get from
    //                      the image file (thumbnail, preview, and so on). 
    //                      Designate values as defined in Enum EdsImageSource. 
    //              inImageType - Designate the output image type. Because
    //                      the output format of EdGetImage may only be RGB, only
    //                      kEdsTargetImageType_RGB or kEdsTargetImageType_RGB16
    //                      can be designated. 
    //                      However, image types exceeding the resolution of 
    //                      inImageSource cannot be designated. 
    //              inSrcRect - Designate the coordinates and size of the rectangle
    //                      to be retrieved (processed) from the source image. 
    //              inDstSize - Designate the rectangle size for output. 
    //
    //      Out:    
    //              outStreamRef - Designate the memory or file stream for output of
    //                      the image.
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsGetImage(nint inImageRef, EdsImageSource inImageSource, EdsTargetImageType inImageType, EdsRect inSrcRect, EdsSize inDstSize, nint outStreamRef);

    //----------------------------------------------
    //   Event handler registering functions
    //----------------------------------------------            
    /*-----------------------------------------------------------------------------
   //
   //  Function:   EdsSetCameraAddedHandler
   //
   //  Description:
   //      Registers a callback function for when a camera is detected.
   //
   //  Parameters:
   //       In:    cameraAddedHandler - Pointer to a callback function
   //                          called when a camera is connected physically
   //              inContext - Specifies an application-defined value to be sent to
   //                          the callback function pointed to by CallBack parameter.
   //      Out:    None
   //
   //  Returns:    Any of the sdk errors.
   -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsSetCameraAddedHandler(EdsCameraAddedHandler cameraAddedHandler, nint inContext);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsSetPropertyEventHandler
    //              
    //  Description:
    //       Registers a callback function for receiving status 
    //          change notification events for property states on a camera.
    //
    //  Parameters:
    //       In:    camera - Designate the camera object. 
    //              @event - Designate one or all events to be supplemented.
    //              inPropertyEventHandler - Designate the pointer to the callback
    //                      function for receiving property-related camera events.
    //              inContext - Designate application information to be passed by 
    //                      means of the callback function. Any data needed for
    //                      your application can be passed. 
    //      Out:    None
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsSetPropertyEventHandler(nint camera, PropertyEvent @event, EdsPropertyEventHandler? inPropertyEventHandler, nint inContext);

    /*-----------------------------------------------------------------------------
    //
    //  Function:   EdsSetObjectEventHandler
    //              
    //  Description:
    //       Registers a callback function for receiving status 
    //          change notification events for objects on a remote camera. 
    //      Here, object means volumes representing memory cards, files and directories, 
    //          and shot images stored in memory, in particular. 
    //
    //  Parameters:
    //       In:    camera - Designate the camera object. 
    //              @event - Designate one or all events to be supplemented.
    //                  To designate all events, use kEdsObjectEvent_All. 
    //              inObjectEventHandler - Designate the pointer to the callback function
    //                  for receiving object-related camera events.
    //              inContext - Passes inContext without modification,
    //                  as designated as an EdsSetObjectEventHandler argument. 
    //      Out:    None
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsSetObjectEventHandler(nint camera, EdsEvent @event, EdsObjectEventHandler? inObjectEventHandler, nint inContext);

    /*-----------------------------------------------------------------------------
    //
    //  Function:  EdsSetCameraStateEventHandler
    //              
    //  Description:
    //      Registers a callback function for receiving status 
    //          change notification events for property states on a camera.
    //
    //  Parameters:
    //       In:    camera - Designate the camera object. 
    //              @event - Designate one or all events to be supplemented.
    //                  To designate all events, use kEdsStateEvent_All. 
    //              inStateEventHandler - Designate the pointer to the callback function
    //                  for receiving events related to camera object states.
    //              inContext - Designate application information to be passed
    //                  by means of the callback function. Any data needed for
    //                  your application can be passed. 
    //      Out:    None
    //
    //  Returns:    Any of the sdk errors.
    -----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsSetCameraStateEventHandler(nint camera, StateEvent @event, EdsStateEventHandler? inStateEventHandler, nint inContext);

    /*-----------------------------------------------------------------------------
		//
		//  Function:   EdsCreateEvfImageRef         
		//  Description:
		//       Creates an object used to get the live view image data set. 
		//
		//  Parameters:
		//      In:     inStreamRef - The stream reference which opened to get EVF JPEG image.
		//      Out:    outEvfImageRef - The EVFData reference.
		//
		//  Returns:    Any of the sdk errors.
		-----------------------------------------------------------------------------*/
    [DllImport(_DLL_PATH)]
    public static extern SDKError EdsCreateEvfImageRef(nint inStreamRef, out nint outEvfImageRef);

    /*-----------------------------------------------------------------------------
		//
		//  Function:   EdsDownloadEvfImage         
		//  Description:
		//		Downloads the live view image data set for a camera currently in live view mode.
		//		Live view can be started by using the property ID:kEdsPropertyID_Evf_OutputDevice and
		//		data:EdsOutputDevice_PC to call EdsSetPropertyData.
		//		In addition to image data, information such as zoom, focus position, and histogram data
		//		is included in the image data set. Image data is saved in a stream maintained by EdsEvfImageRef.
		//		EdsGetPropertyData can be used to get information such as the zoom, focus position, etc.
		//		Although the information of the zoom and focus position can be obtained from EdsEvfImageRef,
		//		settings are applied to Edscamera.
		//
		//  Parameters:
		//      In:     camera - The Camera reference.
		//      In:     inEvfImageRef - The EVFData reference.
		//
		//  Returns:    Any of the sdk errors.
		-----------------------------------------------------------------------------*/
    public static SDKError DownloadEvfImage(Camera? camera, nint outEvfImageRef)
    {
        [DllImport(_DLL_PATH)]
        static extern SDKError EdsDownloadEvfImage(nint camera, nint outEvfImageRef);

        return EdsDownloadEvfImage(CheckValidCamera(camera), outEvfImageRef);
    }




    public const int EDS_MAX_NAME = 256;
    public const int EDS_TRANSFER_BLOCK_SIZE = 512;


    /*-----------------------------------------------------------------------
       ED-SDK Error Code Masks
    ------------------------------------------------------------------------*/
    public const uint EDS_ISSPECIFIC_MASK = 0x80000000;
    public const uint EDS_COMPONENTID_MASK = 0x7F000000;
    public const uint EDS_RESERVED_MASK = 0x00FF0000;
    public const uint EDS_ERRORID_MASK = 0x0000FFFF;

    /*-----------------------------------------------------------------------
       ED-SDK Base Component IDs
    ------------------------------------------------------------------------*/
    public const uint EDS_CMP_ID_CLIENT_COMPONENTID = 0x01000000;
    public const uint EDS_CMP_ID_LLSDK_COMPONENTID = 0x02000000;
    public const uint EDS_CMP_ID_HLSDK_COMPONENTID = 0x03000000;


    /*-----------------------------------------------------------------------------
		 EVF Output Device [Flag]
        Undocumented value, named consistent with SDK
		-----------------------------------------------------------------------------*/
    public const uint EvfOutputDevice_Disabled = 0;
}
