using System.Runtime.InteropServices;

namespace MimicPartyAccess;

/// <summary>
/// Sends concise announcements to supported screen readers through Tolk.
/// </summary>
internal static class ScreenReader
{
    private static IModLogger? _logger;
    private static bool _initialized;
    private static bool _loaded;
    private static bool _available;
    private static string? _lastText;

    [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void Tolk_Load();

    [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void Tolk_Unload();

    [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool Tolk_IsLoaded();

    [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool Tolk_HasSpeech();

    [DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool Tolk_Output(string text, [MarshalAs(UnmanagedType.I1)] bool interrupt);

    public static void Initialize(IModLogger logger)
    {
        if (_initialized)
            return;

        _logger = logger;
        _initialized = true;

        try
        {
            Tolk_Load();
            _loaded = true;
            _available = Tolk_IsLoaded() && Tolk_HasSpeech();
            logger.Info(_available
                ? "Tolk screen-reader output is available."
                : "Tolk loaded, but no speech-capable screen reader was detected.");
        }
        catch (DllNotFoundException)
        {
            logger.Warning("Tolk.dll was not found. Screen-reader announcements are disabled until Tolk is installed.");
        }
        catch (Exception exception)
        {
            logger.Warning($"Could not initialize Tolk: {exception.Message}");
        }
    }

    public static void Say(string text, bool interrupt = true)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        _lastText = text;
        if (!_available)
        {
            _logger?.Debug($"[Screen reader unavailable] {text}");
            return;
        }

        try
        {
            Tolk_Output(text, interrupt);
        }
        catch (Exception exception)
        {
            _available = false;
            _logger?.Warning($"Screen-reader output failed and was disabled: {exception.Message}");
        }
    }

    public static void Repeat()
    {
        if (!string.IsNullOrWhiteSpace(_lastText))
            Say(_lastText, interrupt: true);
    }

    public static void Shutdown()
    {
        if (!_initialized)
            return;

        try
        {
            if (_loaded)
                Tolk_Unload();
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Tolk shutdown reported an error: {exception.Message}");
        }
        finally
        {
            _available = false;
            _loaded = false;
            _initialized = false;
            _logger = null;
        }
    }
}
