using System.Runtime.Versioning;
using System.Threading;
using System;

using Microsoft.Extensions.Logging;

namespace EDSDK.NET;


/// <summary>
/// Helper class to create or run code on STA threads
/// </summary>
public static class STAThread
{
    /// <summary>
    /// States if the execution thread is currently running
    /// </summary>
    private static volatile bool _is_running = false;

    /// <summary>
    /// Lock object to make sure only one command at a time is executed
    /// </summary>
    private static readonly object _run_lock = new();

    /// <summary>
    /// Lock object to synchronize between execution and calling thread
    /// </summary>
    private static readonly object _thread_lock = new();

    /// <summary>
    /// The main thread where everything will be executed on
    /// </summary>
    private static Thread _main;

    /// <summary>
    /// The action to be executed
    /// </summary>
    private static Action _run_action;

    /// <summary>
    /// Storage for an exception that might have happened on the execution thread
    /// </summary>
    private static Exception _run_exception;

    private static ILogger _logger;

    public static event EventHandler<EventArgs> FatalError;


    private static void OnFatalError() => FatalError?.Invoke(null, EventArgs.Empty);

    public static void SetLogAction(ILogger logger) => _logger = logger;

    /// <summary>
    /// The object that is used to lock the live view thread
    /// </summary>
    public static readonly object ExecLock = new();

    /// <summary>
    /// States if the calling thread is an STA thread or not
    /// </summary>
    public static bool IsSTAThread => Thread.CurrentThread.GetApartmentState() == ApartmentState.STA;

    /// <summary>
    /// Starts the execution thread
    /// </summary>
    internal static void Init()
    {
        if (!_is_running)
        {
            _main = Create(SafeExecutionLoop);
            _is_running = true;
            _main.Start();
        }
    }

    /// <summary>
    /// Shuts down the execution thread
    /// </summary>
    internal static void Shutdown()
    {
        if (_is_running)
        {
            _logger.LogInformation("Shutdown");

            bool locked = _is_running = false;

            try
            {
                Monitor.TryEnter(_thread_lock, TimeSpan.FromSeconds(30), ref locked);

                if (locked)
                    Monitor.Pulse(_thread_lock);
                else
                    _logger?.LogError("Lock request timeout expired. LockObject: {LockObject}", nameof(_thread_lock));
            }
            finally
            {
                if (locked)
                    Monitor.Exit(_thread_lock);
            }

            _main.Join(TimeSpan.FromSeconds(30));
        }
    }

    /// <summary>
    /// Creates an STA thread that can safely execute SDK commands
    /// </summary>
    /// <param name="a">The command to run on this thread</param>
    /// <returns>An STA thread</returns>
    [SupportedOSPlatform("windows")]
    public static Thread Create(Action a) => Create(a, $"STA thread: {Guid.NewGuid()}");

    /// <summary>
    /// Creates an STA thread that can safely execute SDK commands
    /// </summary>
    /// <param name="a">The command to run on this thread</param>
    /// <param name="threadName">The name of this thread</param>
    /// <returns>An STA thread</returns>
    [SupportedOSPlatform("windows")]
    public static Thread Create(Action a, string threadName)
    {
        Thread thread = new(new ThreadStart(a));

        thread.SetApartmentState(ApartmentState.STA);
        thread.Name = threadName;

        _logger.LogInformation($"Created STA Thread. ThreadName: {thread.Name}, ApartmentState: {thread.GetApartmentState()}");

        return thread;
    }

    public static void TryLockAndExecute(object lockObject, string lockObjectName, TimeSpan timeout, Action action)
    {
        bool locked = false;

        try
        {
            Monitor.TryEnter(lockObject, timeout, ref locked);

            if (locked)
                action();
            else
                _logger?.LogError($"Lock request timeout expired. LockObject: {lockObjectName}");
        }
        finally
        {
            if (locked)
                Monitor.Exit(lockObject);
        }
    }


    /// <summary>
    /// Safely executes an SDK command
    /// </summary>
    /// <param name="a">The SDK command</param>
    public static void ExecuteSafely(Action a) => TryLockAndExecute(_run_lock, nameof(_run_lock), TimeSpan.FromSeconds(30), delegate
    {
        if (!_is_running)
            return;

        if (IsSTAThread)
        {
            _run_action = a;

            TryLockAndExecute(_thread_lock, nameof(_thread_lock), TimeSpan.FromSeconds(30), delegate
            {
                Monitor.Pulse(_thread_lock);
                Monitor.Wait(_thread_lock);
            });

            if (_run_exception != null)
                throw _run_exception;
        }
        else
            TryLockAndExecute(ExecLock, nameof(ExecLock), TimeSpan.FromSeconds(30), a);
    });

    /// <summary>
    /// Safely executes an SDK command with return value
    /// </summary>
    /// <param name="func">The SDK command</param>
    /// <returns>the return value of the function</returns>
    public static T ExecuteSafely<T>(Func<T> func)
    {
        T result = default;

        ExecuteSafely(() => result = func());

        return result;
    }

    private static void SafeExecutionLoop() => TryLockAndExecute(_thread_lock, nameof(_thread_lock), TimeSpan.FromSeconds(30), delegate
    {
        Thread cThread = Thread.CurrentThread;

        while (true)
        {
            Monitor.Wait(_thread_lock);

            if (!_is_running)
                return;

            _run_exception = null;

            try
            {
                TryLockAndExecute(ExecLock, nameof(ExecLock), TimeSpan.FromSeconds(30), delegate
                {
                    _logger.LogInformation($"Executing action on ThreadName: {cThread.Name}, ApartmentState: {cThread.GetApartmentState()}");
                    _run_action();
                });
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Exception on ThreadName: {cThread.Name}, ApartmentState: {cThread.GetApartmentState()}");
                _run_exception = ex;
            }

            Monitor.Pulse(_thread_lock);
        }
    });
}
