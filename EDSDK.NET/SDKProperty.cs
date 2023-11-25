namespace EDSDK.NET;

public class SDKProperty(string name, uint value, bool matched = true)
{
    public string Name { get; private set; } = name;
    public uint Value { get; private set; } = value;

    /// <summary>
    /// Whether the property matched one of the stored properties
    /// </summary>
    public bool Matched { get; private set; } = matched;

    internal object ValueToString() => "0x" + Value.ToString("X");
}
