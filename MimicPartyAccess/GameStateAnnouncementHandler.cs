using System.Reflection;
using HarmonyLib;

namespace MimicPartyAccess;

/// <summary>
/// Announces round scores, lobby roster changes, and final rankings from researched game UI callbacks.
/// </summary>
internal static class GameStateAnnouncementHandler
{
    private static IModLogger? _logger;
    private static int _currentRound = 1;
    private static string? _lastLobbyAnnouncement;
    private static string? _lastResultsAnnouncement;
    private static readonly List<(string Name, int Points)> RoundScores = new();

    public static void TryInstall(HarmonyLib.Harmony harmony, IModLogger logger)
    {
        _logger = logger;
        InstallRoundHooks(harmony, logger);
        InstallLobbyHook(harmony, logger);
        InstallResultsHook(harmony, logger);
    }

    private static void InstallRoundHooks(HarmonyLib.Harmony harmony, IModLogger logger)
    {
        var gameScreen = ReflectionReader.FindTypeByName("Mimick.UI.GameScreen");
        var playerType = ReflectionReader.FindTypeByName("Mimick.Data.MimickPlayer");
        var recordingType = ReflectionReader.FindTypeByName("Mimick.Data.PlayerRecording");
        var scoreType = ReflectionReader.FindTypeByName("Mimick.Gameplay.ScoreResult")
            ?? ReflectionReader.FindTypeByName("Mimick.Scoring.ScoreResult");

        if (gameScreen == null)
        {
            logger.Warning("Round and score announcements unavailable: Mimick.UI.GameScreen was not found.");
            return;
        }

        var roundMethod = FindMethod(gameScreen, "OnRoundStarted", parameters =>
            parameters.Length == 1 && parameters[0].ParameterType == typeof(int));
        var roundPostfix = typeof(GameStateAnnouncementHandler).GetMethod(
            nameof(AfterRoundStarted), BindingFlags.Static | BindingFlags.NonPublic);
        if (roundMethod != null && roundPostfix != null)
        {
            harmony.Patch(roundMethod, postfix: new HarmonyMethod(roundPostfix));
            logger.Info("Round counter announcement hook installed.");
        }
        else
        {
            logger.Warning("Round counter announcement skipped: OnRoundStarted(int) did not match the researched signature.");
        }

        var scoreMethod = FindMethod(gameScreen, "OnScored", parameters =>
            parameters.Length == 3 && playerType != null && recordingType != null && scoreType != null &&
            parameters[0].ParameterType == playerType &&
            parameters[1].ParameterType == recordingType &&
            parameters[2].ParameterType == scoreType);
        var scorePostfix = typeof(GameStateAnnouncementHandler).GetMethod(
            nameof(AfterScored), BindingFlags.Static | BindingFlags.NonPublic);
        if (scoreMethod != null && scorePostfix != null)
        {
            harmony.Patch(scoreMethod, postfix: new HarmonyMethod(scorePostfix));
            logger.Info("Player score announcement hook installed.");
        }
        else
        {
            logger.Warning("Player score announcement skipped: OnScored signature did not match the researched game contract.");
        }

        var recordTurnMethod = FindMethod(gameScreen, "OnRecordTurnStarted", parameters =>
            parameters.Length == 1 && playerType != null && parameters[0].ParameterType == playerType);
        var recordTurnPostfix = typeof(GameStateAnnouncementHandler).GetMethod(
            nameof(AfterRecordTurnStarted), BindingFlags.Static | BindingFlags.NonPublic);
        if (recordTurnMethod != null && recordTurnPostfix != null && recordTurnMethod.DeclaringType == gameScreen)
        {
            harmony.Patch(recordTurnMethod, postfix: new HarmonyMethod(recordTurnPostfix));
            logger.Info("Recording turn announcement hook installed.");
        }
        else
        {
            logger.Warning("Recording turn announcement skipped: OnRecordTurnStarted(MimickPlayer) was not declared by GameScreen.");
        }

        var countdownMethod = FindMethod(gameScreen, "OnCountdownTick", parameters =>
            parameters.Length == 1 && parameters[0].ParameterType == typeof(int));
        var countdownPostfix = typeof(GameStateAnnouncementHandler).GetMethod(
            nameof(AfterCountdownTick), BindingFlags.Static | BindingFlags.NonPublic);
        if (countdownMethod != null && countdownPostfix != null && countdownMethod.DeclaringType == gameScreen)
        {
            harmony.Patch(countdownMethod, postfix: new HarmonyMethod(countdownPostfix));
            logger.Info("Recording countdown announcement hook installed.");
        }
        else
        {
            logger.Warning("Recording countdown announcement skipped: OnCountdownTick(int) was not declared by GameScreen.");
        }
    }

    private static void InstallLobbyHook(HarmonyLib.Harmony harmony, IModLogger logger)
    {
        var lobbyType = ReflectionReader.FindTypeByName("Mimick.UI.LobbyScreen");
        var method = lobbyType == null ? null : FindMethod(lobbyType, "RefreshRoster", parameters => parameters.Length == 0);
        var startMethod = lobbyType == null ? null : FindMethod(lobbyType, "Start", parameters => parameters.Length == 0);
        var postfix = typeof(GameStateAnnouncementHandler).GetMethod(
            nameof(AfterLobbyRefresh), BindingFlags.Static | BindingFlags.NonPublic);
        var prefix = typeof(GameStateAnnouncementHandler).GetMethod(
            nameof(BeforeLobbyStart), BindingFlags.Static | BindingFlags.NonPublic);

        if (method == null || postfix == null || startMethod == null || prefix == null)
        {
            logger.Warning("Lobby roster announcements skipped: LobbyScreen.Start() or RefreshRoster() did not match the researched contract.");
            return;
        }

        harmony.Patch(method, postfix: new HarmonyMethod(postfix));
        harmony.Patch(startMethod, prefix: new HarmonyMethod(prefix));
        logger.Info("Lobby roster announcement hook installed.");
    }

    private static void InstallResultsHook(HarmonyLib.Harmony harmony, IModLogger logger)
    {
        var resultsType = ReflectionReader.FindTypeByName("Mimick.UI.ResultsScreen");
        var method = resultsType == null ? null : FindMethod(resultsType, "OpenReplayWindow", parameters => parameters.Length == 0);
        var startMethod = resultsType == null ? null : FindMethod(resultsType, "Start", parameters => parameters.Length == 0);
        var postfix = typeof(GameStateAnnouncementHandler).GetMethod(
            nameof(AfterResultsReady), BindingFlags.Static | BindingFlags.NonPublic);
        var prefix = typeof(GameStateAnnouncementHandler).GetMethod(
            nameof(BeforeResultsStart), BindingFlags.Static | BindingFlags.NonPublic);

        if (method == null || postfix == null || startMethod == null || prefix == null)
        {
            logger.Warning("Final ranking announcements skipped: ResultsScreen.Start() or OpenReplayWindow() did not match the researched contract.");
            return;
        }

        harmony.Patch(method, postfix: new HarmonyMethod(postfix));
        harmony.Patch(startMethod, prefix: new HarmonyMethod(prefix));
        logger.Info("Final ranking announcement hook installed.");
    }

    private static void AfterRoundStarted(object[] __args)
    {
        if (__args.Length == 0 || !int.TryParse(__args[0]?.ToString(), out var round))
            return;

        _currentRound = Math.Clamp(round, 1, 5);
        _lastResultsAnnouncement = null;
        lock (RoundScores)
        {
            RoundScores.Clear();
        }

        var text = Loc.Get("round_started", _currentRound);
        _logger?.Info($"Announcing: {text}");
        ScreenReader.Say(text);
    }

    /// <summary>
    /// Returns the localized per-round score summary collected during scoring, or null when nothing was collected.
    /// </summary>
    public static string? TakeRoundScoreSummary()
    {
        lock (RoundScores)
        {
            if (RoundScores.Count == 0)
                return null;

            var summary = Loc.Get("round_scores", string.Join(", ", RoundScores.Select(entry => $"{entry.Name} {entry.Points}")));
            RoundScores.Clear();
            return summary;
        }
    }

    private static void BeforeLobbyStart()
    {
        _lastLobbyAnnouncement = null;
    }

    private static void BeforeResultsStart()
    {
        _lastResultsAnnouncement = null;
    }

    private static void AfterScored(object[] __args)
    {
        if (__args.Length < 3 || __args[0] == null || __args[2] == null)
            return;

        try
        {
            var playerName = ReflectionReader.ReadMember(__args[0], "DisplayName") as string;
            var scoreValue = ReflectionReader.ReadMember(__args[2], "Score");
            if (string.IsNullOrWhiteSpace(playerName) || scoreValue == null)
            {
                _logger?.Info($"Announcing score: member read failed (name='{playerName}', score={(scoreValue == null ? "null" : scoreValue.ToString())}).");
                ScreenReader.Say(Loc.Get("score_unavailable"), interrupt: false);
                return;
            }

            var score = (int)Math.Round(Convert.ToSingle(scoreValue));
            lock (RoundScores)
            {
                RoundScores.Add((playerName, score));
            }

            var text = Loc.Get("score_result", _currentRound, playerName, score);
            _logger?.Info($"Announcing: {text}");
            ScreenReader.Say(text, interrupt: false);
        }
        catch (Exception exception)
        {
            ScreenReader.Say(Loc.Get("score_unavailable"), interrupt: false);
            _logger?.Warning($"Could not read a player's score: {exception.Message}");
        }
    }

    private static void AfterRecordTurnStarted(object __instance, object[] __args)
    {
        try
        {
            var player = __args.Length > 0 ? __args[0] : null;
            var name = ReflectionReader.ReadMember(player, "DisplayName") as string;
            var isLocal = ReflectionReader.ReadMember(player, "IsLocal") is true;
            var recording = ReadRecordingState(__instance);
            _logger?.Info($"Record turn started: player='{name}'; local={isLocal}; recording={recording}");

            if (!isLocal)
                return;

            var text = Loc.Get("record_your_turn");
            _logger?.Info($"Announcing: {text} (recording={recording})");
            ScreenReader.Say(text);
        }
        catch (Exception exception)
        {
            _logger?.Warning($"Could not announce the recording turn: {exception.Message}");
        }
    }

    private static void AfterCountdownTick(object __instance, object[] __args)
    {
        try
        {
            if (__args.Length == 0 || __args[0] == null || !int.TryParse(__args[0].ToString(), out var tick))
                return;

            var recording = ReadRecordingState(__instance);
            _logger?.Info($"Recording countdown tick: {tick}; recording={recording}");
            if (recording == true)
                return;

            var text = Loc.Get("record_countdown", tick);
            _logger?.Info($"Announcing: {text}");
            ScreenReader.Say(text, interrupt: false);
        }
        catch (Exception exception)
        {
            _logger?.Warning($"Could not announce the recording countdown: {exception.Message}");
        }
    }

    private static bool? ReadRecordingState(object gameScreen)
    {
        var controller = ReflectionReader.ReadMember(gameScreen, "roundController");
        var value = ReflectionReader.ReadMember(controller, "IsRecording");
        return value is bool flag ? flag : null;
    }

    private static void AfterLobbyRefresh(object __instance)
    {
        try
        {
            var players = ReadLobbyRows(__instance);
            var summary = players.Count > 0
                ? string.Join(". ", players)
                : ReadLobbyFallback(__instance);

            if (string.IsNullOrWhiteSpace(summary) || string.Equals(summary, _lastLobbyAnnouncement, StringComparison.Ordinal))
                return;

            _lastLobbyAnnouncement = summary;
            ScreenReader.Say(Loc.Get("lobby_players", summary), interrupt: false);
        }
        catch (Exception exception)
        {
            _logger?.Warning($"Could not read the lobby roster: {exception.Message}");
        }
    }

    private static List<string> ReadLobbyRows(object lobbyScreen)
    {
        var rows = new List<string>();
        var rosterList = ReflectionReader.ReadMember(lobbyScreen, "rosterList");
        var childCountValue = ReflectionReader.ReadMember(rosterList, "childCount");
        if (rosterList == null || childCountValue == null)
            return rows;

        var childCount = Convert.ToInt32(childCountValue);
        var transformType = rosterList.GetType();
        var getChild = transformType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method => method.Name == "GetChild" && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(int));
        var rowType = ReflectionReader.FindTypeByName("Mimick.UI.PlayerLobbyRowUI");
        var getComponent = rowType == null ? null : FindGetComponentInChildren(rosterList.GetType(), rowType);
        if (getChild == null || getComponent == null)
            return rows;

        for (var index = 0; index < childCount; index++)
        {
            var child = getChild.Invoke(rosterList, new object[] { index });
            var row = child == null ? null : getComponent.Invoke(child, new object[] { rowType! });
            var label = ReflectionReader.ReadText(ReflectionReader.ReadMember(row, "label"));
            var status = ReflectionReader.ReadText(ReflectionReader.ReadMember(row, "status"));
            if (string.IsNullOrWhiteSpace(label))
                continue;

            rows.Add(string.IsNullOrWhiteSpace(status) ? label : $"{label}, {status}");
        }

        return rows;
    }

    private static string ReadLobbyFallback(object lobbyScreen)
    {
        var playerLabel = ReflectionReader.ReadText(ReflectionReader.ReadMember(lobbyScreen, "playerListLabel"));
        var themeLabel = ReflectionReader.ReadText(ReflectionReader.ReadMember(lobbyScreen, "themeLabel"));
        var hintLabel = ReflectionReader.ReadText(ReflectionReader.ReadMember(lobbyScreen, "hintLabel"));
        return string.Join(". ", new[] { playerLabel, themeLabel, hintLabel }.Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static void AfterResultsReady(object __instance)
    {
        try
        {
            var entries = ReadResultCards(__instance);
            var summary = entries.Count > 0
                ? string.Join(" ", entries)
                : ReadResultTextFallback(__instance);

            if (string.IsNullOrWhiteSpace(summary) || string.Equals(summary, _lastResultsAnnouncement, StringComparison.Ordinal))
                return;

            _lastResultsAnnouncement = summary;
            var text = Loc.Get("results_summary", summary);
            _logger?.Info($"Announcing: {text}");
            ScreenReader.Say(text);
        }
        catch (Exception exception)
        {
            _logger?.Warning($"Could not read the final ranking: {exception.Message}");
        }
    }

    private static List<string> ReadResultCards(object resultsScreen)
    {
        var entries = new List<string>();
        var cards = ReflectionReader.ReadMember(resultsScreen, "cards");
        var fallbackRank = 0;

        foreach (var card in ReflectionReader.Enumerate(cards))
        {
            var rank = ReflectionReader.ReadText(ReflectionReader.ReadMember(card, "rankLabel"));
            var name = ReflectionReader.ReadText(ReflectionReader.ReadMember(card, "nameLabel"));
            var score = ReflectionReader.ReadText(ReflectionReader.ReadMember(card, "scoreLabel"));
            if (string.IsNullOrWhiteSpace(name))
                continue;

            fallbackRank++;
            entries.Add(Loc.Get("results_player", rank ?? fallbackRank.ToString(), name, score ?? Loc.Get("score_unavailable")));
        }

        return entries;
    }

    private static string ReadResultTextFallback(object resultsScreen)
    {
        var podium = ReflectionReader.ReadText(ReflectionReader.ReadMember(resultsScreen, "podiumLabel"));
        var stats = ReflectionReader.ReadText(ReflectionReader.ReadMember(resultsScreen, "statsLabel"));
        return string.Join(". ", new[] { podium, stats }.Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static MethodInfo? FindMethod(Type type, string name, Func<ParameterInfo[], bool> signatureMatches)
    {
        var matches = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == name && signatureMatches(method.GetParameters()))
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static MethodInfo? FindGetComponentInChildren(Type type, Type componentType)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method =>
            {
                var parameters = method.GetParameters();
                return method.Name == "GetComponentInChildren" && parameters.Length == 1 && parameters[0].ParameterType == typeof(Type);
            });
    }
}
