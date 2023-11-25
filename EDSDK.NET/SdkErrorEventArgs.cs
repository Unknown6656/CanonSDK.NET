using Microsoft.Extensions.Logging;

using System;

namespace EDSDK.NET;

public class SdkErrorEventArgs : EventArgs
{
    public string Error { get; set; }
    public LogLevel ErrorLevel { get; set; }
}
