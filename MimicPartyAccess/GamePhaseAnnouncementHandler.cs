using System.Reflection;
using HarmonyLib;

namespace MimicPartyAccess;

/// <summary>
/// Installs a guarded observer for Mimic Party round-phase transitions.
/// </summary>
internal static class GamePhaseAnnouncementHandler
{
    private static readonly string[] ExpectedPhases =
    {
        "Idle",
        "Intro",
        "Announce",
        "Reveal",
        "Recording",
        "Playback",
        "PeerScoring",
        "RoundResults",
        "Wheel"
    };

    private static readonly IReadOnlyDictionary<string, string> PhaseMessageKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Idle"] = "phase_idle",
            ["Intro"] = "phase_intro",
            ["Announce"] = "phase_announce",
            ["Reveal"] = "phase_reveal",
            ["Recording"] = "phase_recording",
            ["Playback"] = "phase_playback",
            ["PeerScoring"] = "phase_peer_scoring",
            ["RoundResults"] = "phase_round_results",
            ["Wheel"] = "phase_wheel"
        };

    private static IModLogger? _logger;

    public static bool TryInstall(HarmonyLib.Harmony harmony, IModLogger logger)
    {
        _logger = logger;
        try
        {
            var screenType = ReflectionReader.FindTypeByName("Mimick.UI.GameScreen");
            var phaseType = ReflectionReader.FindTypeByName("Mimick.Data.RoundPhase");
            if (screenType == null || phaseType == null || !phaseType.IsEnum)
            {
                logger.Warning("Game phase hook skipped: expected Mimick.UI.GameScreen or Mimick.Data.RoundPhase type was not found.");
                return false;
            }

            var actualPhases = Enum.GetNames(phaseType);
            if (!ExpectedPhases.All(actualPhases.Contains))
            {
                logger.Warning("Game phase hook skipped: RoundPhase values differ from the researched game contract.");
                return false;
            }

            var targets = screenType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.Name == "OnPhaseChanged")
                .Where(method =>
                {
                    var parameters = method.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == phaseType;
                })
                .ToArray();

            if (targets.Length != 1)
            {
                logger.Warning($"Game phase hook skipped: expected one OnPhaseChanged(RoundPhase) method, found {targets.Length}.");
                return false;
            }

            var postfix = typeof(GamePhaseAnnouncementHandler).GetMethod(
                nameof(AfterPhaseChanged),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (postfix == null)
            {
                logger.Error("Game phase hook skipped: announcement callback was not found.");
                return false;
            }

            harmony.Patch(targets[0], postfix: new HarmonyMethod(postfix));
            logger.Info("Game phase announcement hook installed after validating the phase enum and method signature.");
            return true;
        }
        catch (Exception exception)
        {
            logger.Warning($"Game phase hook could not be installed: {exception.Message}");
            return false;
        }
    }

    private static void AfterPhaseChanged(object __instance, object[] __args)
    {
        if (__args.Length == 0 || __args[0] == null)
            return;

        var phaseName = __args[0].ToString();
        if (phaseName == null || !PhaseMessageKeys.TryGetValue(phaseName, out var messageKey))
            return;

        try
        {
            var text = Loc.Get(messageKey);
            if (phaseName == "RoundResults")
            {
                var detail = GameStateAnnouncementHandler.TakeRoundScoreSummary()
                    ?? BuildStandingsDetail(__instance);
                if (!string.IsNullOrWhiteSpace(detail))
                    text = $"{text} {detail}";
            }

            _logger?.Info($"Announcing: {text}");
            ScreenReader.Say(text);
        }
        catch (Exception exception)
        {
            _logger?.Warning($"Could not announce phase {phaseName}: {exception.Message}");
            ScreenReader.Say(Loc.Get(messageKey));
        }
    }

    /// <summary>
    /// Fallback round-result detail: leaderboard leader plus the local player's total score.
    /// </summary>
    private static string? BuildStandingsDetail(object gameScreen)
    {
        try
        {
            var controller = ReflectionReader.ReadMember(gameScreen, "roundController");
            var roster = ReflectionReader.ReadMember(controller, "Roster");
            string? leader = null;
            var leaderScore = int.MinValue;
            string? localName = null;
            var localScore = 0;

            foreach (var player in ReflectionReader.Enumerate(roster))
            {
                var name = ReflectionReader.ReadText(ReflectionReader.ReadMember(player, "DisplayName"));
                var value = ReflectionReader.ReadMember(player, "Score");
                if (string.IsNullOrWhiteSpace(name) || value == null)
                    continue;

                var score = (int)Math.Round(Convert.ToSingle(value));
                if (score > leaderScore)
                {
                    leaderScore = score;
                    leader = name;
                }

                if (ReflectionReader.ReadMember(player, "IsLocal") is true)
                {
                    localName = name;
                    localScore = score;
                }
            }

            if (leader == null)
                return null;

            var parts = new List<string> { Loc.Get("round_leader", leader, leaderScore) };
            if (localName != null && !string.Equals(localName, leader, StringComparison.Ordinal))
                parts.Add(Loc.Get("round_your_score", localScore));
            return string.Join(" ", parts);
        }
        catch (Exception exception)
        {
            _logger?.Warning($"Could not read the round standings: {exception.Message}");
            return null;
        }
    }
}
