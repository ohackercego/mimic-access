using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(MimicPartyAccess.MelonPlugin), "Mimic Party Access", "0.1.0", "OpenCode", null)]
[assembly: MelonGame("FoliesAPP", "Mimic Party")]

namespace MimicPartyAccess;

/// <summary>
/// MelonLoader IL2CPP entry point for Mimic Party Access.
/// </summary>
public sealed class MelonPlugin : MelonMod
{
    private HarmonyLib.Harmony? _harmony;
    private IModLogger? _modLogger;

    public override void OnInitializeMelon()
    {
        _modLogger = new ModLogger(
            MelonLogger.Msg,
            MelonLogger.Warning,
            MelonLogger.Error,
            MelonLogger.Msg);

        BuildFingerprint.Log(_modLogger);
        ScreenReader.Initialize(_modLogger);

        _harmony = new HarmonyLib.Harmony("org.mimicparty.accessibility");
        var phaseHooksInstalled = GamePhaseAnnouncementHandler.TryInstall(_harmony, _modLogger);
        AccessibleMenuHandler.TryInstall(_harmony, _modLogger);
        GameStateAnnouncementHandler.TryInstall(_harmony, _modLogger);

        if (!phaseHooksInstalled)
            ScreenReader.Say(Loc.Get("phase_hook_unavailable"), interrupt: false);

        ScreenReader.Say(Loc.Get("mod_loaded"));
        _modLogger.Info("Mimic Party Access initialized through MelonLoader. No native game binary is patched.");
    }

    public override void OnApplicationQuit()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        ScreenReader.Shutdown();
        _modLogger = null;
    }
}
