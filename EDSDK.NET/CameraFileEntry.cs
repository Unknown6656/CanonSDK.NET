using System.Collections.Generic;
using System.Drawing;

using EDSDK.Native;

namespace EDSDK.NET;


public enum CameraFileEntryTypes
{
    Camera = 5,
    Volume = 10,
    Folder = 20,
    File = 30,
}

/// <summary>
/// A storage for a camera filesystem entry
/// </summary>
/// <remarks>
/// Creates a new instance of the CameraFileEntry class
/// </remarks>
/// <param name="name">Name of this entry</param>
/// <param name="IsFolder">True if this entry is a folder, false otherwise</param>
public class CameraFileEntry(string name, CameraFileEntryTypes type, nint handle)
{
    /// <summary>
    /// Name of this entry
    /// </summary>
    public string Name { get; } = name;

    public CameraFileEntryTypes Type { get; } = type;

    public nint Handle { get; } = handle;

    /// <summary>
    /// Thumbnail of this entry (might be null if not available)
    /// </summary>
    public Bitmap? Thumbnail { get; set; }

    /// <summary>
    /// Subentries of this entry (i.e. subfolders)
    /// </summary>
    public CameraFileEntry[] Entries { get; private set; } = [];

    public EdsVolumeInfo Volume { get; set; }

    public void AddSubEntries(IEnumerable<CameraFileEntry> entries) => Entries = [.. Entries, .. entries];
}
