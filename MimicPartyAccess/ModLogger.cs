namespace MimicPartyAccess;

/// <summary>
/// Minimal logging surface shared by BepInEx and MelonLoader entry points.
/// </summary>
internal interface IModLogger
{
    void Info(string message);
    void Warning(string message);
    void Error(string message);
    void Debug(string message);
}

/// <summary>
/// Adapts loader logging callbacks to the mod's shared handlers.
/// </summary>
internal sealed class ModLogger : IModLogger
{
    private readonly Action<string> _info;
    private readonly Action<string> _warning;
    private readonly Action<string> _error;
    private readonly Action<string> _debug;

    public ModLogger(Action<string> info, Action<string> warning, Action<string> error, Action<string> debug)
    {
        _info = info;
        _warning = warning;
        _error = error;
        _debug = debug;
    }

    public void Info(string message) => _info(message);
    public void Warning(string message) => _warning(message);
    public void Error(string message) => _error(message);
    public void Debug(string message) => _debug(message);
}
