﻿namespace EDSDK.NET;


/// <summary>
/// Helper to convert between ID and string camera values
/// </summary>
public static class CameraValues
{
    /// <summary>
    /// Gets the Av string value from an Av ID
    /// </summary>
    /// <param name="v">The Av ID</param>
    /// <returns>the Av string</returns>
    public static string AV(uint v) => v switch
    {
        0x00 => "Auto",
        0x08 => "1",
        0x40 => "11",
        0x0B => "1.1",
        0x43 => "13 (1/3)",
        0x0C => "1.2",
        0x44 => "13",
        0x0D => "1.2 (1/3)",
        0x45 => "14",
        0x10 => "1.4",
        0x48 => "16",
        0x13 => "1.6",
        0x4B => "18",
        0x14 => "1.8",
        0x4C => "19",
        0x15 => "1.8 (1/3)",
        0x4D => "20",
        0x18 => "2",
        0x50 => "22",
        0x1B => "2.2",
        0x53 => "25",
        0x1C => "2.5",
        0x54 => "27",
        0x1D => "2.5 (1/3)",
        0x55 => "29",
        0x20 => "2.8",
        0x58 => "32",
        0x23 => "3.2",
        0x5B => "36",
        0x24 => "3.5",
        0x5C => "38",
        0x25 => "3.5 (1/3)",
        0x5D => "40",
        0x28 => "4",
        0x60 => "45",
        0x2B => "4.5",
        0x63 => "51",
        0x2C => "4.5 (1/3)",
        0x64 => "54",
        0x2D => "5.0",
        0x65 => "57",
        0x30 => "5.6",
        0x68 => "64",
        0x33 => "6.3",
        0x6B => "72",
        0x34 => "6.7",
        0x6C => "76",
        0x35 => "7.1",
        0x6D => "80",
        0x38 => " 8",
        0x70 => "91",
        0x3B => "9",
        0x3C => "9.5",
        0x3D => "10",
        _ => "N/A",
    };

    /// <summary>
    /// Gets the ISO string value from an ISO ID
    /// </summary>
    /// <param name="v">The ISO ID</param>
    /// <returns>the ISO string</returns>
    public static string ISO(uint v) => v switch
    {
        0x00000000 => "Auto ISO",
        0x00000028 => "ISO 6",
        0x00000030 => "ISO 12",
        0x00000038 => "ISO 25",
        0x00000040 => "ISO 50",
        0x00000048 => "ISO 100",
        0x0000004b => "ISO 125",
        0x0000004d => "ISO 160",
        0x00000050 => "ISO 200",
        0x00000053 => "ISO 250",
        0x00000055 => "ISO 320",
        0x00000058 => "ISO 400",
        0x0000005b => "ISO 500",
        0x0000005d => "ISO 640",
        0x00000060 => "ISO 800",
        0x00000063 => "ISO 1000",
        0x00000065 => "ISO 1250",
        0x00000068 => "ISO 1600",
        0x00000070 => "ISO 3200",
        0x00000078 => "ISO 6400",
        0x00000080 => "ISO 12800",
        0x00000088 => "ISO 25600",
        0x00000090 => "ISO 51200",
        0x00000098 => "ISO 102400",
        _ => "N/A",
    };

    /// <summary>
    /// Gets the Tv string value from an Tv ID
    /// </summary>
    /// <param name="v">The Tv ID</param>
    /// <returns>the Tv string</returns>
    public static string TV(uint v) => v switch
    {
        0x00 => "Auto",
        0x0C => "Bulb",
        0x5D => "1/25",
        0x10 => "30\"",
        0x60 => "1/30",
        0x13 => "25\"",
        0x63 => "1/40",
        0x14 => "20\"",
        0x64 => "1/45",
        0x15 => "20\" (1/3)",
        0x65 => "1/50",
        0x18 => "15\"",
        0x68 => "1/60",
        0x1B => "13\"",
        0x6B => "1/80",
        0x1C => "10\"",
        0x6C => "1/90",
        0x1D => "10\" (1/3)",
        0x6D => "1/100",
        0x20 => "8\"",
        0x70 => "1/125",
        0x23 => "6\" (1/3)",
        0x73 => "1/160",
        0x24 => "6\"",
        0x74 => "1/180",
        0x25 => "5\"",
        0x75 => "1/200",
        0x28 => "4\"",
        0x78 => "1/250",
        0x2B => "3\"2",
        0x7B => "1/320",
        0x2C => "3\"",
        0x7C => "1/350",
        0x2D => "2\"5",
        0x7D => "1/400",
        0x30 => "2\"",
        0x80 => "1/500",
        0x33 => "1\"6",
        0x83 => "1/640",
        0x34 => "1\"5",
        0x84 => "1/750",
        0x35 => "1\"3",
        0x85 => "1/800",
        0x38 => "1\"",
        0x88 => "1/1000",
        0x3B => "0\"8",
        0x8B => "1/1250",
        0x3C => "0\"7",
        0x8C => "1/1500",
        0x3D => "0\"6",
        0x8D => "1/1600",
        0x40 => "0\"5",
        0x90 => "1/2000",
        0x43 => "0\"4",
        0x93 => "1/2500",
        0x44 => "0\"3",
        0x94 => "1/3000",
        0x45 => "0\"3 (1/3)",
        0x95 => "1/3200",
        0x48 => "1/4",
        0x98 => "1/4000",
        0x4B => "1/5",
        0x9B => "1/5000",
        0x4C => "1/6",
        0x9C => "1/6000",
        0x4D => "1/6 (1/3)",
        0x9D => "1/6400",
        0x50 => "1/8",
        0xA0 => "1/8000",
        0x53 => "1/10 (1/3)",
        0x54 => "1/10",
        0x55 => "1/13",
        0x58 => "1/15",
        0x5B => "1/20 (1/3)",
        0x5C => "1/20",
        _ => "N/A",
    };

    /// <summary>
    /// Gets the Av ID from an Av string value
    /// </summary>
    /// <param name="v">The Av string</param>
    /// <returns>the Av ID</returns>
    public static uint AV(string v) => v switch
    {
        "Auto" => 0x00,
        "1" => 0x08,
        "11" => 0x40,
        "1.1" => 0x0B,
        "13 (1/3)" => 0x43,
        "1.2" => 0x0C,
        "13" => 0x44,
        "1.2 (1/3)" => 0x0D,
        "14" => 0x45,
        "1.4" => 0x10,
        "16" => 0x48,
        "1.6" => 0x13,
        "18" => 0x4B,
        "1.8" => 0x14,
        "19" => 0x4C,
        "1.8 (1/3)" => 0x15,
        "20" => 0x4D,
        "2" => 0x18,
        "22" => 0x50,
        "2.2" => 0x1B,
        "25" => 0x53,
        "2.5" => 0x1C,
        "27" => 0x54,
        "2.5 (1/3)" => 0x1D,
        "29" => 0x55,
        "2.8" => 0x20,
        "32" => 0x58,
        "3.2" => 0x23,
        "36" => 0x5B,
        "3.5" => 0x24,
        "38" => 0x5C,
        "3.5 (1/3)" => 0x25,
        "40" => 0x5D,
        "4" => 0x28,
        "45" => 0x60,
        "4.5" => 0x2B,
        "51" => 0x63,
        "4.5 (1/3)" => 0x2C,
        "54" => 0x64,
        "5.0" => 0x2D,
        "57" => 0x65,
        "5.6" => 0x30,
        "64" => 0x68,
        "6.3" => 0x33,
        "72" => 0x6B,
        "6.7" => 0x34,
        "76" => 0x6C,
        "7.1" => 0x35,
        "80" => 0x6D,
        " 8" => 0x38,
        "91" => 0x70,
        "9" => 0x3B,
        "9.5" => 0x3C,
        "10" => 0x3D,
        _ => 0xffffffff,
    };

    /// <summary>
    /// Gets the ISO ID from an ISO string value
    /// </summary>
    /// <param name="v">The ISO string</param>
    /// <returns>the ISO ID</returns>
    public static uint ISO(string v) => v switch
    {
        "Auto ISO" => 0x00000000,
        "ISO 6" => 0x00000028,
        "ISO 12" => 0x00000030,
        "ISO 25" => 0x00000038,
        "ISO 50" => 0x00000040,
        "ISO 100" => 0x00000048,
        "ISO 125" => 0x0000004b,
        "ISO 160" => 0x0000004d,
        "ISO 200" => 0x00000050,
        "ISO 250" => 0x00000053,
        "ISO 320" => 0x00000055,
        "ISO 400" => 0x00000058,
        "ISO 500" => 0x0000005b,
        "ISO 640" => 0x0000005d,
        "ISO 800" => 0x00000060,
        "ISO 1000" => 0x00000063,
        "ISO 1250" => 0x00000065,
        "ISO 1600" => 0x00000068,
        "ISO 3200" => 0x00000070,
        "ISO 6400" => 0x00000078,
        "ISO 12800" => 0x00000080,
        "ISO 25600" => 0x00000088,
        "ISO 51200" => 0x00000090,
        "ISO 102400" => 0x00000098,
        _ => 0xffffffff,
    };

    /// <summary>
    /// Gets the Tv ID from an Tv string value
    /// </summary>
    /// <param name="v">The Tv string</param>
    /// <returns>the Tv ID</returns>
    public static uint TV(string v) => v switch
    {
        "Auto" => 0x00,
        "Bulb" => 0x0C,
        "1/25" => 0x5D,
        "30\"" => 0x10,
        "1/30" => 0x60,
        "25\"" => 0x13,
        "1/40" => 0x63,
        "20\"" => 0x14,
        "1/45" => 0x64,
        "20\" (1/3)" => 0x15,
        "1/50" => 0x65,
        "15\"" => 0x18,
        "1/60" => 0x68,
        "13\"" => 0x1B,
        "1/80" => 0x6B,
        "10\"" => 0x1C,
        "1/90" => 0x6C,
        "10\" (1/3)" => 0x1D,
        "1/100" => 0x6D,
        "8\"" => 0x20,
        "1/125" => 0x70,
        "6\" (1/3)" => 0x23,
        "1/160" => 0x73,
        "6\"" => 0x24,
        "1/180" => 0x74,
        "5\"" => 0x25,
        "1/200" => 0x75,
        "4\"" => 0x28,
        "1/250" => 0x78,
        "3\"2" => 0x2B,
        "1/320" => 0x7B,
        "3\"" => 0x2C,
        "1/350" => 0x7C,
        "2\"5" => 0x2D,
        "1/400" => 0x7D,
        "2\"" => 0x30,
        "1/500" => 0x80,
        "1\"6" => 0x33,
        "1/640" => 0x83,
        "1\"5" => 0x34,
        "1/750" => 0x84,
        "1\"3" => 0x35,
        "1/800" => 0x85,
        "1\"" => 0x38,
        "1/1000" => 0x88,
        "0\"8" => 0x3B,
        "1/1250" => 0x8B,
        "0\"7" => 0x3C,
        "1/1500" => 0x8C,
        "0\"6" => 0x3D,
        "1/1600" => 0x8D,
        "0\"5" => 0x40,
        "1/2000" => 0x90,
        "0\"4" => 0x43,
        "1/2500" => 0x93,
        "0\"3" => 0x44,
        "1/3000" => 0x94,
        "0\"3 (1/3)" => 0x45,
        "1/3200" => 0x95,
        "1/4" => 0x48,
        "1/4000" => 0x98,
        "1/5" => 0x4B,
        "1/5000" => 0x9B,
        "1/6" => 0x4C,
        "1/6000" => 0x9C,
        "1/6 (1/3)" => 0x4D,
        "1/6400" => 0x9D,
        "1/8" => 0x50,
        "1/8000" => 0xA0,
        "1/10 (1/3)" => 0x53,
        "1/10" => 0x54,
        "1/13" => 0x55,
        "1/15" => 0x58,
        "1/20 (1/3)" => 0x5B,
        "1/20" => 0x5C,
        _ => 0xffffffff,
    };
}
