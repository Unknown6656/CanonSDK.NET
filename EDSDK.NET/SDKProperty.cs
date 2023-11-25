namespace EDSDK.NET;

public class SDKProperty
{
    public SDKProperty(string name, uint value, bool matched = true)
    {
        Name = name;
        Value = value;
        Matched = matched;
    }
    public string Name { get; private set; }
    public uint Value { get; private set; }

    /// <summary>
    /// Whether the property matched one of the stored properties
    /// </summary>
    public bool Matched { get; private set; }

    internal object ValueToString() => "0x" + Value.ToString("X");
}
