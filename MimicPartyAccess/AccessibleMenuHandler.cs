using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace MimicPartyAccess;

/// <summary>
/// Adds screen-reader announcements and keyboard navigation for game UI controls.
/// </summary>
internal static class AccessibleMenuHandler
{
    private static readonly string[] ScreenTypes =
    {
        "Mimick.UI.PlaytestPopup",
        "Mimick.UI.PauseMenu",
        "Mimick.UI.ConfirmDialog",
        "Mimick.UI.GameModePanel",
        "Mimick.UI.RoomBrowserPanel",
        "Mimick.UI.AccountLinkPanel",
        "Mimick.UI.SkinPanel",
        "Mimick.UI.AuraPanel",
        "Mimick.UI.RevealAnimationPanel",
        "Mimick.UI.InviteFriendPanel",
        "Mimick.UI.FriendsPanel",
        "Mimick.UI.MimiBucksShopPanel",
        "Mimick.UI.SeasonPassPanel",
        "Mimick.UI.ObjectivesPanel",
        "Mimick.UI.NamePanel",
        "Mimick.UI.ThemePreviewPanel",
        "Mimick.UI.DiscordPanel",
        "Mimick.UI.PlayerReportPanel",
        "Mimick.UI.CountryPreferencePanel",
        "Mimick.UI.PackFeedbackPanel",
        "Mimick.UI.ThemeFeedbackPanel",
        "Mimick.UI.ThemeDiscordPopup",
        "Mimick.UI.MainMenuScreen",
        "Mimick.UI.MatchmakingScreen",
        "Mimick.UI.LobbyScreen",
        "Mimick.UI.SoloScreen",
        "Mimick.UI.GameScreen",
        "Mimick.UI.ResultsScreen",
        "Mimick.UI.ThemeCreatorScreen"
    };

    private static readonly Dictionary<string, string> ScreenMessageKeys =
        new(StringComparer.Ordinal)
        {
            ["MainMenuScreen"] = "screen_main_menu",
            ["MatchmakingScreen"] = "screen_matchmaking",
            ["LobbyScreen"] = "screen_lobby",
            ["SoloScreen"] = "screen_solo",
            ["GameScreen"] = "screen_game",
            ["ResultsScreen"] = "screen_results",
            ["ThemeCreatorScreen"] = "screen_theme_creator",
            ["PlaytestPopup"] = "screen_playtest_popup",
            ["PauseMenu"] = "screen_settings",
            ["ConfirmDialog"] = "screen_confirm",
            ["AccountLinkPanel"] = "screen_account_link",
            ["GameModePanel"] = "screen_game_mode",
            ["RoomBrowserPanel"] = "screen_room_browser",
            ["SkinPanel"] = "screen_skin_panel",
            ["AuraPanel"] = "screen_aura_panel",
            ["RevealAnimationPanel"] = "screen_reveal_animation_panel",
            ["InviteFriendPanel"] = "screen_invite_friend_panel",
            ["FriendsPanel"] = "screen_friends_panel",
            ["MimiBucksShopPanel"] = "screen_shop_panel",
            ["SeasonPassPanel"] = "screen_season_pass_panel",
            ["ObjectivesPanel"] = "screen_objectives_panel",
            ["NamePanel"] = "screen_name_panel",
            ["ThemePreviewPanel"] = "screen_theme_preview_panel",
            ["DiscordPanel"] = "screen_discord_panel",
            ["PlayerReportPanel"] = "screen_player_report_panel",
            ["CountryPreferencePanel"] = "screen_country_preference_panel",
            ["PackFeedbackPanel"] = "screen_pack_feedback_panel",
            ["ThemeFeedbackPanel"] = "screen_theme_feedback_panel",
            ["ThemeDiscordPopup"] = "screen_theme_discord_popup"
        };

    private static readonly Dictionary<string, object> KeyCodes = new(StringComparer.Ordinal);
    private static IModLogger? _logger;
    private static object? _activeScreen;
    private static object? _inlinePanel;
    private static object? _focusedInputControl;
    private static object? _expectedInputFocus;
    private static string? _hierarchyFailure;
    private static List<MenuOption> _options = new();
    private static readonly List<(object Control, string Fallback, ControlKind Kind)> _hierarchyControls = new();
    private static int _lastLoggedOptionCount = -1;
    private static List<(string Name, Type Type, object? Value)> _controlMembers = new();
    private static readonly Dictionary<IntPtr, string> _labelCache = new();
    private static DateTime _nextLabelRefresh;
    private static DateTime _nextHierarchyRefresh;
    private static int _labelLanguageRevision;
    private static int _currentIndex;
    private static string? _pendingNavigationKey;
    private static Type? _inputType;
    private static MethodInfo? _getKey;
    private static MethodInfo? _getKeyDown;
    private static MethodInfo? _findObjectOfType;
    private static DateTime _nextScreenDiscovery;
    private static DateTime _nextInputInitialization;
    private static DateTime _nextOptionsRefresh;
    private static DateTime _nextLanguageRefresh;
    private static bool _captureAnnounced;
    private static string? _pushToTalkKeyBeforeCapture;
    private static List<Type>? _genericScreenTypes;
    private static readonly Dictionary<string, object> _knownScreens = new(StringComparer.Ordinal);
    private static object? _lastStartedScreen;
    private static bool _inputReady;
    private static string? _lastExpandedDropdownValue;

    public static bool TryInstall(HarmonyLib.Harmony harmony, IModLogger logger)
    {
        _logger = logger;
        var patchedScreens = 0;

        foreach (var typeName in ScreenTypes)
        {
            try
            {
                var screenType = ReflectionReader.FindTypeByName(typeName);
                if (screenType == null)
                {
                    logger.Warning($"Accessible menu support skipped missing screen type: {typeName}.");
                    continue;
                }

                var activation = new[] { "Start", "Open", "OnEnable", "Update" }
                    .Select(name => FindDeclaredNoArgumentMethod(screenType, name))
                    .FirstOrDefault(method => method != null);
                if (activation == null)
                {
                    logger.Warning($"Accessible menu support skipped {typeName}: no unique Start(), Open(), OnEnable(), or Update() declared by the screen was found.");
                    continue;
                }

                var started = typeof(AccessibleMenuHandler).GetMethod(
                    nameof(OnScreenStarted), BindingFlags.Static | BindingFlags.NonPublic);
                if (started == null)
                    continue;

                harmony.Patch(activation, postfix: new HarmonyMethod(started));
                var onEnable = FindDeclaredNoArgumentMethod(screenType, "OnEnable");
                if (onEnable != null && onEnable != activation)
                    harmony.Patch(onEnable, postfix: new HarmonyMethod(started));

                var onDestroy = FindDeclaredNoArgumentMethod(screenType, "Close")
                    ?? FindDeclaredNoArgumentMethod(screenType, "OnDestroy");
                var destroyed = typeof(AccessibleMenuHandler).GetMethod(
                    nameof(OnScreenDestroyed), BindingFlags.Static | BindingFlags.NonPublic);
                if (onDestroy != null && destroyed != null)
                    harmony.Patch(onDestroy, postfix: new HarmonyMethod(destroyed));
                else
                    logger.Info($"Accessible menu: {typeName} declares no Close()/OnDestroy() of its own; closure is detected from the active state.");

                patchedScreens++;
            }
            catch (Exception exception)
            {
                logger.Warning($"Could not install accessible menu hook for {typeName}: {exception.Message}");
            }
        }

        try
        {
            var eventSystemType = ReflectionReader.FindTypeByName("UnityEngine.EventSystems.EventSystem");
            var update = eventSystemType == null ? null : FindUniqueNoArgumentMethod(eventSystemType, "Update");
            var prefix = typeof(AccessibleMenuHandler).GetMethod(
                nameof(BeforeEventSystemUpdate), BindingFlags.Static | BindingFlags.NonPublic);
            var postfix = typeof(AccessibleMenuHandler).GetMethod(
                nameof(AfterEventSystemUpdate), BindingFlags.Static | BindingFlags.NonPublic);

            if (update != null && prefix != null && postfix != null)
            {
                harmony.Patch(update, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                InitializeInputReflection();
            }
            else
            {
                logger.Warning("Keyboard navigation is unavailable: Unity EventSystem.Update() was not found.");
            }
        }
        catch (Exception exception)
        {
            logger.Warning($"Could not install keyboard navigation: {exception.Message}");
        }

        if (patchedScreens == 0)
            logger.Warning("No Mimic Party screen hooks were installed; menu navigation remains inactive.");
        else
            logger.Info($"Accessible menu announcements installed for {patchedScreens} Mimic Party screens.");

        return patchedScreens > 0;
    }

    private static bool BeforeEventSystemUpdate()
    {
        return !ShouldHandleNavigationKey();
    }

    private static void AfterEventSystemUpdate()
    {
        var navigationKey = _pendingNavigationKey;
        _pendingNavigationKey = null;

        if (!_inputReady)
            InitializeInputReflection();

        if (_activeScreen != null && !IsScreenActive(_activeScreen))
            ClearActiveScreen();
        DiscoverActiveScreen();
        RefreshInlinePanel();

        if (_activeScreen != null && DateTime.UtcNow >= _nextOptionsRefresh)
        {
            RefreshOptions();
        }

        if (_activeScreen != null && AnnounceExpandedDropdownChange())
            return;
        RefreshGameLanguage();
        var focusedInput = GetFocusedInput();
        var textInputFocused = false;
        if (focusedInput != null && GetControlKind(focusedInput.GetType()) == ControlKind.InputField)
        {
            if (AreSameControl(focusedInput, _expectedInputFocus))
            {
                textInputFocused = true;
            }
            else
            {
                BlurInput(focusedInput);
                focusedInput = null;
            }
        }
        else if (focusedInput != null)
        {
            textInputFocused = true;
        }
        else
        {
            _expectedInputFocus = null;
        }

        if (textInputFocused && !AreSameControl(focusedInput, _focusedInputControl))
        {
            _focusedInputControl = focusedInput;
            string? focusedLabel = null;
            foreach (var option in _options)
            {
                if (AreSameControl(option.Control, focusedInput))
                {
                    focusedLabel = option.Label;
                    break;
                }
            }

            ScreenReader.Say(Loc.Get("menu_input_focus", focusedLabel ?? Loc.Get("menu_unknown_button")));
        }
        else if (!textInputFocused)
        {
            _focusedInputControl = null;
        }

        if (textInputFocused && IsKeyDown("Escape"))
        {
            BlurInput(focusedInput!);
            _expectedInputFocus = null;
            _focusedInputControl = null;
            ScreenReader.Say(Loc.Get("menu_input_blur"));
            return;
        }
        var capturingKey = _activeScreen != null && IsCapturingKey();
        if (capturingKey && !_captureAnnounced)
        {
            _captureAnnounced = true;
            _pushToTalkKeyBeforeCapture = _activeScreen == null
                ? null
                : ReflectionReader.ReadText(ReadMember(_activeScreen, "pushToTalkKeyState"));
            ScreenReader.Say(Loc.Get("menu_press_key"));
            return;
        }
        if (!capturingKey && _captureAnnounced)
        {
            _captureAnnounced = false;
            AnnouncePushToTalkKeySet();
        }
        if (navigationKey != null)
        {
            if (_activeScreen == null || textInputFocused || capturingKey)
                return;

            try
            {
                switch (navigationKey)
                {
                    case "UpArrow": Navigate(-1); break;
                    case "DownArrow": Navigate(1); break;
                    case "LeftArrow": AdjustCurrent(-1); break;
                    case "RightArrow": AdjustCurrent(1); break;
                    case "Return":
                    case "KeypadEnter": ActivateCurrent(); break;
                    case "Escape": CloseActiveOverlay(); break;
                }
            }
            catch (Exception exception)
            {
                _logger?.Warning($"Accessible menu input failed: {exception.Message}");
            }

            return;
        }

        if (_activeScreen == null || textInputFocused || !HasAccessibilityModifiers())
            return;

        try
        {
            if (IsKeyDown("F1"))
                AnnounceHelp();
            else if (IsKeyDown("R"))
                ScreenReader.Repeat();
        }
        catch (Exception exception)
        {
            _logger?.Warning($"Accessible menu input failed: {exception.Message}");
        }
    }

    private static bool ShouldHandleNavigationKey()
    {
        _pendingNavigationKey = null;
        if (!_inputReady)
            InitializeInputReflection();

        if (_activeScreen == null || !IsScreenActive(_activeScreen))
            return false;
        if (IsTextInputFocused() || IsCapturingKey())
            return false;

        foreach (var key in new[] { "UpArrow", "DownArrow", "LeftArrow", "RightArrow", "Return", "KeypadEnter" })
        {
            if (!IsKeyDown(key))
                continue;

            _pendingNavigationKey = key;
            return true;
        }

        if (_activeScreen != null && (_inlinePanel != null || IsOverlayScreen(_activeScreen)) && IsKeyDown("Escape"))
        {
            _pendingNavigationKey = "Escape";
            return true;
        }

        return false;
    }

    private static void OnScreenStarted(object __instance)
    {
        try
        {
            var instanceKey = __instance.GetType().FullName ?? __instance.GetType().Name;
            _knownScreens[instanceKey] = __instance;
            _lastStartedScreen = __instance;

            if (!IsScreenActive(__instance))
                return;

            if (Equals(_activeScreen, __instance))
                return;

            _activeScreen = __instance;
            _inlinePanel = null;
            _options.Clear();
            _hierarchyControls.Clear();
            _nextHierarchyRefresh = DateTime.MinValue;
            _labelCache.Clear();
            _currentIndex = 0;
            _lastLoggedOptionCount = -1;
            RefreshOptions(forceRefresh: true);

            var typeName = __instance.GetType().Name;
            _logger?.Info($"Accessible screen active: {typeName}; {_options.Count} controls discovered.");
            var messageKey = ScreenMessageKeys.TryGetValue(typeName, out var key) ? key : "screen_generic";
            ScreenReader.Say(Loc.Get("menu_opened", Loc.Get(messageKey)));

            if (_options.Count > 0)
            {
                AnnounceCurrentOption();
            }
            else
                ScreenReader.Say(Loc.Get("menu_no_buttons"), interrupt: false);

            if (typeName == "MainMenuScreen")
                AnnounceGates();
        }
        catch (Exception exception)
        {
            _logger?.Warning($"OnScreenStarted hook failed for {__instance?.GetType().Name}: {exception.Message}");
        }
    }

    private static void OnScreenDestroyed(object __instance)
    {
        try
        {
            var instanceKey = __instance.GetType().FullName ?? __instance.GetType().Name;
            if (_knownScreens.TryGetValue(instanceKey, out var known) && Equals(known, __instance))
                _knownScreens.Remove(instanceKey);
            if (Equals(_lastStartedScreen, __instance))
                _lastStartedScreen = null;

            if (Equals(_activeScreen, __instance))
                ClearActiveScreen();
        }
        catch (Exception exception)
        {
            _logger?.Warning($"OnScreenDestroyed hook failed for {__instance?.GetType().Name}: {exception.Message}");
        }
    }

    private static bool IsOverlayScreen(object screen)
    {
        var name = screen.GetType().Name;
        return name is "PlaytestPopup" or "PauseMenu" or "RoomBrowserPanel" or "GameModePanel" or "AccountLinkPanel"
            or "SkinPanel" or "AuraPanel" or "RevealAnimationPanel" or "InviteFriendPanel" or "FriendsPanel"
            or "MimiBucksShopPanel" or "SeasonPassPanel" or "ObjectivesPanel" or "NamePanel" or "ThemePreviewPanel"
            or "DiscordPanel" or "PlayerReportPanel" or "CountryPreferencePanel" or "PackFeedbackPanel"
            or "ThemeFeedbackPanel" or "ThemeDiscordPopup";
    }

    private static void RefreshInlinePanel()
    {
        if (_activeScreen?.GetType().Name != "MainMenuScreen")
            return;

        var panel = ReadMember(_activeScreen, "joinPanel");
        if (ReadMember(panel, "activeInHierarchy") is not true)
            panel = null;
        if (ReferenceEquals(panel, _inlinePanel) || (panel != null && AreSameUnityObject(panel, _inlinePanel)))
            return;

        _inlinePanel = panel;
        _options.Clear();
        _hierarchyControls.Clear();
        _nextHierarchyRefresh = DateTime.MinValue;
        _lastLoggedOptionCount = -1;
        _currentIndex = 0;
        RefreshOptions(forceRefresh: true);
        var context = panel == null ? "screen_main_menu" : "screen_room_browser";
        _logger?.Info($"Accessible UI context: {context}; {_options.Count} controls; source={(panel == null ? "main menu" : "visible joinPanel hierarchy")}.");
        ScreenReader.Say(Loc.Get("menu_opened", Loc.Get(context)));
        AnnounceCurrentOption();
    }

    private static void AnnounceGates()
    {
        try
        {
            var gates = new[]
            {
                ("Mimick.UI.BanGate", "Raised", "gate_ban"),
                ("Mimick.UI.MaintenanceGate", "Raised", "gate_maintenance"),
                ("Mimick.UI.OwnershipGate", "Raised", "gate_ownership"),
                ("Mimick.UI.UpdateGate", "raised", "gate_update")
            };

            foreach (var (typeName, memberName, messageKey) in gates)
            {
                var gateType = ReflectionReader.FindTypeByName(typeName);
                if (gateType != null && ReadStaticBool(gateType, memberName))
                    ScreenReader.Say(Loc.Get(messageKey), interrupt: false);
            }
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Gate status check deferred: {exception.Message}");
        }
    }

    private static bool ReadStaticBool(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var property = type.GetProperty(name, flags);
        if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
            return property.GetValue(null) is bool value && value;

        var field = type.GetField(name, flags);
        return field != null && field.GetValue(null) is bool fieldValue && fieldValue;
    }

    private static bool IsCapturingKey()
    {
        if (_activeScreen == null)
            return false;

        try
        {
            return ReadMember(_activeScreen, "capturingKey") is bool capturing && capturing;
        }
        catch
        {
            return false;
        }
    }

    private static string? _lastLanguageDiagnostic;
    private static string? _lastAnnouncedLanguage;

    private static void RefreshGameLanguage()
    {
        if (DateTime.UtcNow < _nextLanguageRefresh)
            return;

        _nextLanguageRefresh = DateTime.UtcNow.AddSeconds(2);
        try
        {
            var gameLocale = ReadGameLocale();
            var changed = Loc.SetBaseLanguage(gameLocale);
            var resolved = Loc.CurrentLanguage;

            if (changed && _lastAnnouncedLanguage != null && _lastAnnouncedLanguage != resolved)
                ScreenReader.Say(Loc.Get("language_changed"));

            _lastAnnouncedLanguage = resolved;
            LogLanguageDiagnostic(
                $"resolved={resolved}; game={gameLocale ?? "unknown"}; os={Loc.OsLanguageCode}; base={Loc.BaseLanguage ?? "none"}; culture={CultureInfo.CurrentUICulture.Name}");
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Game language detection deferred: {exception.Message}");
        }
    }

    private static void LogLanguageDiagnostic(string message)
    {
        if (string.Equals(message, _lastLanguageDiagnostic, StringComparison.Ordinal))
            return;

        _lastLanguageDiagnostic = message;
        _logger?.Info($"Announcement language: {message}");
    }

    private static string? ReadGameLocale()
    {
        try
        {
            var localizedType = ReflectionReader.FindTypeByName("Mimick.Localization.Localized");
            if (localizedType != null)
            {
                var current = localizedType.GetProperty("Current", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                foreach (var member in new[] { "Identifier", "Code", "Name" })
                {
                    var language = NormalizeLanguage(ReflectionReader.ReadMember(current, member) as string);
                    if (language != null)
                        return language;
                }
            }
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Current locale read deferred: {exception.Message}");
        }

        try
        {
            var preferenceType = ReflectionReader.FindTypeByName("Mimick.Localization.LanguagePreference");
            var saved = preferenceType?.GetProperty("Saved", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as string;
            var language = NormalizeLanguage(saved);
            if (language != null)
                return language;
            if (!string.IsNullOrWhiteSpace(saved))
                _logger?.Info($"Game language preference is '{saved}', which is not pt/en.");
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Saved locale read deferred: {exception.Message}");
        }

        return null;
    }

    private static string? NormalizeLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("port", StringComparison.Ordinal) || normalized.StartsWith("pt", StringComparison.Ordinal))
            return "pt";
        if (normalized.StartsWith("engl", StringComparison.Ordinal)
            || normalized.StartsWith("ingl", StringComparison.Ordinal)
            || normalized.StartsWith("en", StringComparison.Ordinal))
            return "en";

        return null;
    }

    private static string? ReadDropdownValue(object? dropdown)
    {
        if (dropdown == null)
            return null;

        var options = new List<string>();
        foreach (var option in ReflectionReader.Enumerate(ReadMember(dropdown, "options")))
        {
            var text = ReadMember(option, "text") as string;
            if (!string.IsNullOrWhiteSpace(text))
                options.Add(ReflectionReader.CleanText(text));
        }

        if (options.Count == 0)
            return null;

        var value = Convert.ToInt32(ReadMember(dropdown, "value") ?? 0);
        return options[Math.Clamp(value, 0, options.Count - 1)];
    }

    private static void CloseActiveOverlay()
    {
        if (_inlinePanel != null && _activeScreen != null)
        {
            var close = FindUniqueNoArgumentMethod(_activeScreen.GetType(), "CloseJoinPanel");
            if (close != null)
                close.Invoke(_activeScreen, null);
            else
            {
                var setActive = _inlinePanel.GetType().GetMethod("SetActive", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(bool) }, null);
                setActive?.Invoke(_inlinePanel, new object[] { false });
            }
            RefreshInlinePanel();
            return;
        }
        if (_activeScreen == null || !IsOverlayScreen(_activeScreen))
            return;

        try
        {
            var screenName = _activeScreen.GetType().Name;
            var messageKey = ScreenMessageKeys.TryGetValue(screenName, out var key) ? key : "screen_generic";
            InvokeNoArgument(_activeScreen, "Close");
            ScreenReader.Say(Loc.Get("menu_closed", Loc.Get(messageKey)));
        }
        catch (Exception exception)
        {
            _logger?.Warning($"Could not close the active overlay: {exception.Message}");
        }
    }

    private static void DiscoverActiveScreen()
    {
        if (DateTime.UtcNow < _nextScreenDiscovery)
            return;

        _nextScreenDiscovery = DateTime.UtcNow.AddMilliseconds(250);
        try
        {
            // Optional fallback: the generated interop proxy for UnityEngine.Object does not
            // always expose FindObjectOfType(Type) with a System.Type parameter, so screen
            // discovery primarily relies on instances tracked through the method hooks.
            // A null fallback must not skip the tracked-instance scan below.
            var unityObjectType = ReflectionReader.FindTypeByName("UnityEngine.Object");
            _findObjectOfType ??= unityObjectType?.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(method =>
                {
                    var parameters = method.GetParameters();
                    return method.Name == "FindObjectOfType" && parameters.Length == 1 && parameters[0].ParameterType == typeof(Type);
                });

            // A still-active, most-recently-started screen wins: the game keeps the
            // underlying screen active while a panel opens on top of it.
            if (_lastStartedScreen != null && IsScreenActive(_lastStartedScreen))
            {
                OnScreenStarted(_lastStartedScreen);
                return;
            }

            foreach (var typeName in ScreenTypes)
            {
                var screenType = ReflectionReader.FindTypeByName(typeName);
                var candidate = screenType == null ? null : FindScreenInstance(screenType, typeName);
                if (candidate != null && IsScreenActive(candidate))
                {
                    OnScreenStarted(candidate);
                    return;
                }
            }

            foreach (var genericType in GetGenericScreenTypes())
            {
                var candidate = FindScreenInstance(genericType, genericType.FullName ?? genericType.Name);
                if (candidate != null && IsScreenActive(candidate))
                {
                    OnScreenStarted(candidate);
                    return;
                }
            }
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Active-screen discovery deferred: {exception.Message}");
        }
    }

    private static object? FindScreenInstance(Type screenType, string typeName)
    {
        var key = screenType.FullName ?? typeName;
        if (_knownScreens.TryGetValue(key, out var known))
            return known;

        if (!_knownScreens.TryGetValue(typeName, out known))
            known = null;

        if (known == null)
        {
            for (var current = screenType; current != null && known == null; current = current.BaseType)
            {
                var instance = current.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (instance?.PropertyType == screenType)
                    known = instance.GetValue(null);
            }
        }

        if (known == null && _findObjectOfType != null)
        {
            try
            {
                known = _findObjectOfType.Invoke(null, new object[] { screenType });
            }
            catch (Exception exception)
            {
                _logger?.Debug($"Screen lookup deferred for {typeName}: {exception.Message}");
            }
        }

        return known;
    }

    private static List<Type> GetGenericScreenTypes()
    {
        if (_genericScreenTypes != null)
            return _genericScreenTypes;

        var found = new List<Type>();
        try
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(type => type != null).ToArray()!;
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface)
                        continue;
                    if (type.FullName == null || !type.FullName.StartsWith("Il2CppMimick.UI.", StringComparison.Ordinal))
                        continue;
                    if (type.FullName.Contains('+') || type.FullName.Contains('/'))
                        continue;
                    if (!type.Name.EndsWith("Panel", StringComparison.Ordinal) &&
                        !type.Name.EndsWith("Popup", StringComparison.Ordinal) &&
                        !type.Name.EndsWith("Screen", StringComparison.Ordinal) &&
                        !type.Name.EndsWith("Dialog", StringComparison.Ordinal))
                        continue;
                    if (ScreenTypes.Any(known => "Il2Cpp" + known == type.FullName))
                        continue;

                    found.Add(type);
                }
            }
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Generic screen scan deferred: {exception.Message}");
        }

        found.Sort((first, second) =>
        {
            var rankFirst = first.Name.EndsWith("Dialog", StringComparison.Ordinal) || first.Name.EndsWith("Popup", StringComparison.Ordinal) ? 0 : 1;
            var rankSecond = second.Name.EndsWith("Dialog", StringComparison.Ordinal) || second.Name.EndsWith("Popup", StringComparison.Ordinal) ? 0 : 1;
            var rank = rankFirst.CompareTo(rankSecond);
            return rank != 0 ? rank : string.Compare(first.Name, second.Name, StringComparison.Ordinal);
        });

        _genericScreenTypes = found;
        return found;
    }

    private static void Navigate(int direction)
    {
        RefreshOptions(forceRefresh: true);
        if (_options.Count == 0)
        {
            ScreenReader.Say(Loc.Get("menu_no_buttons"));
            return;
        }

        _currentIndex = (_currentIndex + direction + _options.Count) % _options.Count;
        AnnounceCurrentOption();
    }

    private static void ActivateCurrent()
    {
        RefreshOptions(forceRefresh: true);
        if (_options.Count == 0 || _currentIndex < 0 || _currentIndex >= _options.Count)
        {
            ScreenReader.Say(Loc.Get("menu_no_selection"));
            return;
        }

        var option = _options[_currentIndex];
        try
        {
            switch (option.Kind)
            {
                case ControlKind.Button:
                    ActivateButton(option);
                    AnnouncePushToTalkState(option);
                    break;
                case ControlKind.Toggle:
                    var toggleState = ReadMember(option.Control, "isOn") is bool isOn && isOn;
                    SetMember(option.Control, "isOn", !toggleState);
                    ScreenReader.Say(Loc.Get("menu_toggle_changed", option.Label, !toggleState ? Loc.Get("state_on") : Loc.Get("state_off")));
                    break;
                case ControlKind.InputField:
                    _expectedInputFocus = option.Control;
                    _focusedInputControl = option.Control;
                    SelectCurrentOption();
                    InvokeNoArgument(option.Control, "ActivateInputField");
                    ScreenReader.Say(Loc.Get("menu_input_focus", option.Label));
                    break;
                case ControlKind.Dropdown:
                    AnnounceDropdownOpened(option);
                    InvokeNoArgument(option.Control, "Show");
                    break;
                case ControlKind.Slider:
                    AnnounceValue(option);
                    break;
            }
        }
        catch (Exception exception)
        {
            ScreenReader.Say(Loc.Get("menu_cannot_activate", option.Label));
            _logger?.Warning($"Could not activate control '{option.Label}': {exception.Message}");
        }
    }

    private static void ActivateButton(MenuOption option)
    {
        if (option.Control.GetType().Name == "SettingsButton")
        {
            ScreenReader.Say(Loc.Get("menu_activating", option.Label));
            InvokeNoArgument(option.Control, "Open");
            return;
        }

        var clickEvent = ReadMember(option.Control, "onClick");
        var invoke = clickEvent?.GetType().GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (invoke == null)
        {
            ScreenReader.Say(Loc.Get("menu_cannot_activate", option.Label));
                _logger?.Warning($"Button activation method was not found for '{option.Label}'.");
            return;
        }

        ScreenReader.Say(Loc.Get("menu_activating", option.Label));
        invoke.Invoke(clickEvent, null);
    }

    private static void AnnounceDropdownOpened(MenuOption option)
    {
        try
        {
            var options = new List<string>();
            foreach (var entry in ReflectionReader.Enumerate(ReadMember(option.Control, "options")))
            {
                var text = ReadMember(entry, "text") as string;
                if (!string.IsNullOrWhiteSpace(text))
                    options.Add(ReflectionReader.CleanText(text));
            }

            if (options.Count > 0)
                ScreenReader.Say(Loc.Get("menu_dropdown_opened_options", option.Label, options.Count, string.Join(", ", options)));
            else
                ScreenReader.Say(Loc.Get("menu_dropdown_opened", option.Label));
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Could not read dropdown options: {exception.Message}");
            ScreenReader.Say(Loc.Get("menu_dropdown_opened", option.Label));
        }
    }

    private static void AdjustCurrent(int direction)
    {
        RefreshOptions(forceRefresh: true);
        if (_options.Count == 0 || _currentIndex < 0 || _currentIndex >= _options.Count)
            return;

        var option = _options[_currentIndex];
        if (option.Kind == ControlKind.Slider)
        {
            var current = Convert.ToDouble(ReadMember(option.Control, "value") ?? 0d);
            var minimum = Convert.ToDouble(ReadMember(option.Control, "minValue") ?? 0d);
            var maximum = Convert.ToDouble(ReadMember(option.Control, "maxValue") ?? 1d);
            var wholeNumbers = ReadMember(option.Control, "wholeNumbers") is bool whole && whole;
            var step = wholeNumbers ? 1d : Math.Max((maximum - minimum) / 20d, 0.01d);
            var next = Math.Clamp(current + (direction * step), minimum, maximum);
            SetMember(option.Control, "value", Convert.ChangeType(next, ReadMember(option.Control, "value")?.GetType() ?? typeof(float)));
            AnnounceValue(option);
            return;
        }

        if (option.Kind == ControlKind.Dropdown)
        {
            var optionList = ReadMember(option.Control, "options");
            var count = ReflectionReader.Enumerate(optionList).Count();
            if (count == 0)
            {
                ScreenReader.Say(Loc.Get("menu_empty_dropdown", option.Label));
                return;
            }

            var value = Convert.ToInt32(ReadMember(option.Control, "value") ?? 0);
            var next = (value + direction + count) % count;
            SetMember(option.Control, "value", next);
            InvokeNoArgument(option.Control, "RefreshShownValue");
            AnnounceValue(option);
            return;
        }

        Navigate(direction);
    }

    private static void AnnounceValue(MenuOption option)
    {
        var value = option.Kind switch
        {
            ControlKind.Toggle => ReadMember(option.Control, "isOn") is bool isOn && isOn
                ? Loc.Get("state_on")
                : Loc.Get("state_off"),
            ControlKind.Slider => Convert.ToInt32(Math.Round(Convert.ToDouble(ReadMember(option.Control, "value") ?? 0d))) + Loc.Get("percent"),
            ControlKind.Dropdown => ReadMember(option.Control, "captionText") is object caption
                ? ReflectionReader.ReadText(caption) ?? ReadMember(option.Control, "value")?.ToString() ?? string.Empty
                : ReadMember(option.Control, "value")?.ToString() ?? string.Empty,
            _ => string.Empty
        };

        ScreenReader.Say(Loc.Get("menu_value", option.Label, value));
    }

    private static void SelectCurrentOption()
    {
        if (_options.Count == 0 || _currentIndex < 0 || _currentIndex >= _options.Count)
            return;

        var select = _options[_currentIndex].Control.GetType()
            .GetMethod("Select", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        select?.Invoke(_options[_currentIndex].Control, null);
    }

    private static void AnnounceCurrentOption()
    {
        if (_options.Count == 0 || _currentIndex < 0 || _currentIndex >= _options.Count)
            return;

        var option = _options[_currentIndex];
        var announcement = Loc.Get("menu_item", _currentIndex + 1, _options.Count, option.Label);
        var hint = ResolvePushToTalkHint(option.Control);
        if (!string.IsNullOrWhiteSpace(hint))
            announcement = $"{announcement} {hint}";
        ScreenReader.Say(announcement);
    }

    private static void AnnounceHelp()
    {
        ScreenReader.Say(Loc.Get("menu_help"));
    }

    private static void RefreshOptions(bool forceRefresh = false)
    {
        if (_activeScreen == null)
            return;
        if (!forceRefresh && _options.Count > 0 && DateTime.UtcNow < _nextOptionsRefresh)
            return;

        _nextOptionsRefresh = DateTime.UtcNow.AddMilliseconds(750);
        if (DateTime.UtcNow >= _nextLabelRefresh || _labelLanguageRevision != Loc.LanguageRevision)
        {
            _labelCache.Clear();
            _nextLabelRefresh = DateTime.UtcNow.AddSeconds(2);
            _labelLanguageRevision = Loc.LanguageRevision;
        }

        var previousControl = _options.Count > 0 && _currentIndex >= 0 && _currentIndex < _options.Count
            ? _options[_currentIndex].Control
            : null;
        var refreshed = new List<MenuOption>();

        try
        {
            _controlMembers = ReadMembers(_activeScreen).ToList();
            var root = _inlinePanel ?? ReadMember(_activeScreen, "gameObject");
            if (root != null && (_inlinePanel != null || IsOverlayScreen(_activeScreen)) && DateTime.UtcNow >= _nextHierarchyRefresh)
            {
                _nextHierarchyRefresh = DateTime.UtcNow.AddMilliseconds(1000);
                var discovered = new List<(object Control, string Fallback, ControlKind Kind)>();
                foreach (var typeName in new[] { "UnityEngine.UI.Button", "UnityEngine.UI.Toggle", "UnityEngine.UI.Slider", "TMPro.TMP_Dropdown", "TMPro.TMP_InputField", "UnityEngine.UI.Dropdown", "UnityEngine.UI.InputField" })
                {
                    var type = ReflectionReader.FindTypeByName(typeName);
                    if (type == null)
                        continue;
                    try
                    {
                        foreach (var control in ReflectionReader.GetComponentsInChildren(root, type))
                        {
                            if (control == null)
                                continue;
                            if (_inlinePanel != null && !IsInsidePanel(control, _inlinePanel))
                                continue;
                            discovered.Add((control, ReadMember(ReadMember(control, "gameObject"), "name") as string ?? type.Name, GetControlKind(type)!.Value));
                        }
                    }
                    catch (Exception exception)
                    {
                        var failure = exception.GetBaseException().Message;
                        if (_hierarchyFailure != failure)
                        {
                            _hierarchyFailure = failure;
                            _logger?.Warning($"UI hierarchy discovery failed for {typeName}: {failure}");
                        }
                    }
                }

                _hierarchyControls.Clear();
                _hierarchyControls.AddRange(discovered);
            }

            foreach (var (control, fallback, kind) in _hierarchyControls)
                AddControl(refreshed, control, fallback, kind);

            foreach (var member in _controlMembers)
            {
                var kind = GetControlKind(member.Type);
                if (kind != null)
                {
                    if (_inlinePanel == null || IsInsidePanel(member.Value, _inlinePanel))
                        AddControl(refreshed, member.Value, member.Name, kind.Value);
                    continue;
                }

                if (member.Value != null && (member.Type.FullName?.StartsWith("Il2CppMimick.", StringComparison.Ordinal) ?? false))
                {
                    AddNestedControls(refreshed, member.Value, member.Name);
                }

                var index = 0;
                foreach (var control in ReflectionReader.Enumerate(member.Value))
                {
                    var collectionKind = GetControlKind(control.GetType());
                    if (collectionKind != null && (_inlinePanel == null || IsInsidePanel(control, _inlinePanel)))
                        AddControl(refreshed, control, $"{member.Name} {++index}", collectionKind.Value);
                    else if (control != null && (control.GetType().FullName?.StartsWith("Il2CppMimick.", StringComparison.Ordinal) ?? false))
                        AddNestedControls(refreshed, control, member.Name);
                }
            }
        }
        catch (Exception exception)
        {
            _logger?.Warning($"Could not inspect active menu controls: {exception.Message}");
        }

        _options = refreshed;
        if (_options.Count != _lastLoggedOptionCount)
        {
            _lastLoggedOptionCount = _options.Count;
            var labels = _options.Count > 40 ? _options.Take(40).Select(option => option.Label) : _options.Select(option => option.Label);
            _logger?.Info($"Accessible options now: {_options.Count} [{string.Join(" | ", labels)}]");
        }

        if (_options.Count == 0)
        {
            _currentIndex = 0;
            return;
        }

        var previousIndex = previousControl == null ? -1 : _options.FindIndex(option => AreSameControl(option.Control, previousControl));
        _currentIndex = previousIndex >= 0 ? previousIndex : Math.Clamp(_currentIndex, 0, _options.Count - 1);
    }

    private static void AddNestedControls(List<MenuOption> refreshed, object holder, string prefix)
    {
        foreach (var nested in ReadMembers(holder))
        {
            var nestedKind = GetControlKind(nested.Type);
            if (nestedKind == null)
                continue;

            if (_inlinePanel == null || IsInsidePanel(nested.Value, _inlinePanel))
                AddControl(refreshed, nested.Value, $"{prefix} {nested.Name}", nestedKind.Value);
        }
    }

    private static void AddControl(List<MenuOption> options, object? control, string fallbackLabel, ControlKind kind)
    {
        if (control == null || !IsControlUsable(control))
            return;
        if (options.Any(option => AreSameControl(option.Control, control)))
            return;

        var pointer = ReadMember(control, "Pointer") as IntPtr?;
        string label;
        if (pointer.HasValue && _labelCache.TryGetValue(pointer.Value, out var cached))
            label = cached;
        else
        {
            label = ResolveControlLabel(control, fallbackLabel);
            if (pointer.HasValue)
                _labelCache[pointer.Value] = label;
        }
        options.Add(new MenuOption(control, kind, label));
    }

    private static bool IsInsidePanel(object? control, object panel)
    {
        var transform = ReadMember(control, "transform");
        var panelTransform = ReadMember(panel, "transform");
        for (var depth = 0; transform != null && depth < 64; depth++)
        {
            if (AreSameUnityObject(transform, panelTransform))
                return true;
            transform = ReadMember(transform, "parent");
        }
        return false;
    }

    private static ControlKind? GetControlKind(Type type)
    {
        return type.Name switch
        {
            "Button" or "SettingsButton" => ControlKind.Button,
            "Toggle" => ControlKind.Toggle,
            "Slider" => ControlKind.Slider,
            "Dropdown" or "TMP_Dropdown" => ControlKind.Dropdown,
            "InputField" or "TMP_InputField" => ControlKind.InputField,
            _ => null
        };
    }

    private static bool IsPotentialControlType(Type type)
    {
        if (GetControlKind(type) != null)
            return true;
        if (type.IsArray && type.GetElementType() is Type elementType && GetControlKind(elementType) != null)
            return true;

        return type.IsGenericType && type.GetGenericArguments().Any(argument => GetControlKind(argument) != null);
    }

    private static bool IsControlUsable(object control)
    {
        var gameObject = ReadMember(control, "gameObject");
        var active = ReadMember(gameObject, "activeInHierarchy");
        if (active is bool isActive && !isActive)
            return false;

        var interactable = ReadMember(control, "interactable");
        return interactable is not bool isInteractable || isInteractable;
    }

    private static readonly string[] RowLabelTypes =
    {
        "Il2CppMimick.UI.RoomRowUI",
        "Il2CppMimick.UI.FriendEntryUI",
        "Il2CppMimick.UI.FriendRowUI",
        "Il2CppMimick.UI.PlayerLobbyRowUI",
        "Il2CppMimick.UI.SkinCellUI",
        "Il2CppMimick.UI.AuraCellUI",
        "Il2CppMimick.UI.WorkshopRowUI",
        "Il2CppMimick.UI.ThemeRowUI",
        "Il2CppMimick.UI.MimiBucksPackRow",
        "Il2CppMimick.UI.ObjectiveItem",
        "Il2CppMimick.UI.SeasonTierUI"
    };

    private static readonly string[] RowLabelMembers =
    {
        "hostLabel", "nameLabel", "label", "rankLabel", "titleLabel",
        "descriptionLabel", "modeLabel", "statusLabel", "valueLabel", "teamLabel"
    };

    private static string? ResolveRowLabel(object control)
    {
        try
        {
            var node = ReadMember(control, "transform");
            for (var depth = 0; node != null && depth < 4; depth++)
            {
                var gameObject = ReadMember(node, "gameObject");
                if (gameObject != null)
                {
                    var text = ReadRowText(gameObject);
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }

                node = ReadMember(node, "parent");
            }
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Row label lookup failed: {exception.Message}");
        }

        return null;
    }

    private static string? ReadRowText(object gameObject)
    {
        foreach (var typeName in RowLabelTypes)
        {
            var type = ReflectionReader.FindTypeByName(typeName);
            if (type == null)
                continue;

            object? row = null;
            try
            {
                row = ReflectionReader.GetComponent(gameObject, type);
            }
            catch
            {
                continue;
            }

            if (row == null)
                continue;
            var parts = new List<string>();
            foreach (var memberName in RowLabelMembers)
            {
                var text = ReflectionReader.ReadText(ReadMember(row, memberName));
                if (!string.IsNullOrWhiteSpace(text) && !parts.Contains(text))
                    parts.Add(text);
            }

            if (parts.Count > 0)
                return string.Join(", ", parts);
        }

        return null;
    }

    private static string ResolveControlLabel(object control, string fallback)
    {
        var pushToTalk = ResolvePushToTalkLabel(control);
        if (!string.IsNullOrWhiteSpace(pushToTalk))
            return pushToTalk;

        if (_activeScreen != null)
        {
            foreach (var member in _controlMembers)
            {
                if (AreSameControl(member.Value, control))
                {
                    fallback = member.Name;
                    break;
                }
            }
        }
        var associated = FindAssociatedLabel(fallback);
        if (!string.IsNullOrWhiteSpace(associated))
            return associated;

        if (GetControlKind(control.GetType()) == ControlKind.Dropdown)
            return CleanName(fallback);

        var rowLabel = ResolveRowLabel(control);
        if (!string.IsNullOrWhiteSpace(rowLabel))
            return rowLabel;

        try
        {
            var textType = ReflectionReader.FindTypeByName("TMPro.TMP_Text");
            if (textType != null)
            {
                var getComponent = control.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(method =>
                    {
                        var parameters = method.GetParameters();
                        return method.Name == "GetComponentInChildren" && parameters.Length == 1 && parameters[0].ParameterType == typeof(Type);
                    });
                var textComponent = getComponent?.Invoke(control, new object[] { textType })
                    ?? ReflectionReader.GetComponentsInChildren(control, textType).FirstOrDefault();
                var label = ReadMember(textComponent, "text") as string;
                if (!string.IsNullOrWhiteSpace(label))
                    return CleanText(label);
            }
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Control text lookup used its field name fallback: {exception.Message}");
        }

        var gameObject = ReadMember(control, "gameObject");
        var objectName = ReadMember(gameObject, "name") as string;
        var candidate = string.IsNullOrWhiteSpace(objectName) ? fallback : objectName;
        var separator = candidate.LastIndexOfAny(new[] { '.', '/', '\\' });
        if (separator >= 0)
            candidate = candidate.Substring(separator + 1);
        var cleaned = CleanName(candidate);
        if (!cleaned.Any(char.IsLetterOrDigit))
            return Loc.Get("menu_unknown_button");

        return cleaned;
    }

    private const string PushToTalkToggleMember = "pushToTalkButton";
    private const string PushToTalkKeyMember = "pushToTalkKeyButton";

    private static bool IsPushToTalkScreen()
    {
        return _activeScreen?.GetType().Name == "PauseMenu";
    }

    private static string? ResolvePushToTalkLabel(object control)
    {
        if (!IsPushToTalkScreen())
            return null;

        try
        {
            if (AreSameControl(ReadMember(_activeScreen, PushToTalkToggleMember), control))
            {
                var label = ReflectionReader.ReadText(ReadMember(_activeScreen, "pushToTalkLabel")) ?? Loc.Get("ptt_toggle");
                var state = ReflectionReader.ReadText(ReadMember(_activeScreen, "pushToTalkState"));
                return string.IsNullOrWhiteSpace(state) ? label : $"{label}: {state}";
            }

            if (AreSameControl(ReadMember(_activeScreen, PushToTalkKeyMember), control))
            {
                var key = ReflectionReader.ReadText(ReadMember(_activeScreen, "pushToTalkKeyState"));
                var label = Loc.Get("ptt_key");
                return string.IsNullOrWhiteSpace(key) ? label : $"{label}: {key}";
            }
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Push-to-talk label lookup failed: {exception.Message}");
        }

        return null;
    }

    private static string? ResolvePushToTalkHint(object control)
    {
        if (!IsPushToTalkScreen())
            return null;

        try
        {
            if (AreSameControl(ReadMember(_activeScreen, PushToTalkToggleMember), control))
                return Loc.Get("hint_ptt_toggle");
            if (AreSameControl(ReadMember(_activeScreen, PushToTalkKeyMember), control))
                return Loc.Get("hint_ptt_key");
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Push-to-talk hint lookup failed: {exception.Message}");
        }

        return null;
    }

    private static void AnnouncePushToTalkState(MenuOption option)
    {
        if (!IsPushToTalkScreen())
            return;

        try
        {
            if (!AreSameControl(ReadMember(_activeScreen, PushToTalkToggleMember), option.Control))
                return;

            var label = ReflectionReader.ReadText(ReadMember(_activeScreen, "pushToTalkLabel")) ?? Loc.Get("ptt_toggle");
            var state = ReflectionReader.ReadText(ReadMember(_activeScreen, "pushToTalkState"));
            if (!string.IsNullOrWhiteSpace(state))
                ScreenReader.Say(Loc.Get("menu_value", label, state));
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Push-to-talk state read failed: {exception.Message}");
        }
    }

    private static void AnnouncePushToTalkKeySet()
    {
        if (!IsPushToTalkScreen())
            return;

        try
        {
            var key = ReflectionReader.ReadText(ReadMember(_activeScreen, "pushToTalkKeyState"));
            if (string.IsNullOrWhiteSpace(key))
                return;

            var messageKey = string.Equals(key, _pushToTalkKeyBeforeCapture, StringComparison.OrdinalIgnoreCase)
                ? "ptt_key_unchanged"
                : "ptt_key_set";
            ScreenReader.Say(Loc.Get(messageKey, key));
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Push-to-talk key read failed: {exception.Message}");
        }
    }

    private static string? FindAssociatedLabel(string fallback)
    {
        if (_activeScreen == null || string.IsNullOrWhiteSpace(fallback))
            return null;

        var baseName = Regex.Replace(fallback, "(keybutton|keystate|button|btn|field|input|slider|toggle|dropdown|state|label)$", "", RegexOptions.IgnoreCase);
        if (string.IsNullOrWhiteSpace(baseName))
            return null;

        try
        {
            foreach (var suffix in new[] { "Label", "Title" })
            {
                var text = ReflectionReader.ReadText(ReadMember(_activeScreen, baseName + suffix));
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Associated label lookup failed: {exception.Message}");
        }

        return null;
    }

    private static string CleanText(string value)
    {
        return ReflectionReader.CleanText(value);
    }

    private static string CleanName(string value)
    {
        var name = Regex.Replace(value, "(button|btn|field|input|slider|toggle|dropdown)$", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, "([a-z0-9])([A-Z])", "$1 $2");
        name = name.Replace('_', ' ').Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Loc.Get("menu_unknown_button");

        return Loc.TranslateFallback(name);
    }

    private static bool IsScreenActive(object screen)
    {
        try
        {
            var gameObject = ReadMember(screen, "gameObject");
            if (gameObject == null)
                return false;

            var active = ReadMember(gameObject, "activeInHierarchy");
            var enabled = ReadMember(screen, "enabled");
            var isOpen = ReadMember(screen, "IsOpen");
            return active is bool activeInHierarchy && activeInHierarchy &&
                   (enabled is not bool isEnabled || isEnabled) &&
                   (isOpen is not bool open || open);
        }
        catch
        {
            return false;
        }
    }

    private static object? GetFocusedInput()
    {
        if (_activeScreen == null)
            return null;

        try
        {
            foreach (var option in _options)
            {
                try
                {
                    if (option.Kind == ControlKind.InputField && ReadMember(option.Control, "isFocused") is true)
                        return option.Control;
                    if (option.Kind == ControlKind.Dropdown && ReadMember(option.Control, "IsExpanded") is true)
                        return option.Control;
                }
                catch
                {
                    continue;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static bool IsTextInputFocused()
    {
        if (_activeScreen == null)
            return false;

        try
        {
            foreach (var option in _options)
            {
                if (option.Kind == ControlKind.InputField && ReadMember(option.Control, "isFocused") is true)
                    return true;
                if (option.Kind == ControlKind.Dropdown && ReadMember(option.Control, "IsExpanded") is true)
                    return true;
            }

            foreach (var member in ReadMembers(_activeScreen))
            {
                if (!member.Type.Name.Contains("InputField", StringComparison.Ordinal))
                {
                    var controlKind = GetControlKind(member.Type);
                    if (controlKind == ControlKind.Dropdown && ReadMember(member.Value, "IsExpanded") is bool isExpanded && isExpanded)
                        return true;
                    continue;
                }

                var input = member.Value;
                if (ReadMember(input, "isFocused") is bool isFocused && isFocused)
                    return true;
            }

            return false;
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Could not determine whether a text field is focused: {exception.Message}");
            return false;
        }
    }

    private static bool AnnounceExpandedDropdownChange()
    {
        if (_activeScreen == null)
            return false;

        try
        {
            foreach (var member in ReadMembers(_activeScreen))
            {
                if (GetControlKind(member.Type) != ControlKind.Dropdown)
                    continue;

                var dropdown = member.Value;
                if (dropdown == null || ReadMember(dropdown, "IsExpanded") is not bool expanded || !expanded)
                    continue;

                var label = ResolveControlLabel(dropdown, member.Name);
                var value = ReadMember(dropdown, "captionText") is object caption
                    ? ReflectionReader.ReadText(caption) ?? ReadMember(dropdown, "value")?.ToString() ?? string.Empty
                    : ReadMember(dropdown, "value")?.ToString() ?? string.Empty;
                var announcement = $"{label}:{value}";
                if (string.Equals(announcement, _lastExpandedDropdownValue, StringComparison.Ordinal))
                    return true;

                _lastExpandedDropdownValue = announcement;
                ScreenReader.Say(Loc.Get("menu_value", label, value));
                return true;
            }
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Could not read the expanded dropdown value: {exception.Message}");
        }

        _lastExpandedDropdownValue = null;
        return false;
    }

    private static void ClearActiveScreen()
    {
        _activeScreen = null;
        _inlinePanel = null;
        _focusedInputControl = null;
        _expectedInputFocus = null;
        _options.Clear();
        _hierarchyControls.Clear();
        _lastLoggedOptionCount = -1;
        _controlMembers.Clear();
        _labelCache.Clear();
        _currentIndex = 0;
        _lastExpandedDropdownValue = null;
    }

    private static bool AreSameControl(object? first, object? second)
    {
        if (first == null || second == null)
            return false;
        if (ReferenceEquals(first, second) || Equals(first, second))
            return true;

        // Pointer is a managed identity property. Reading gameObject would enter
        // native Unity code and throw if a refreshed room row was already destroyed.
        try
        {
            var firstPointer = ReadMember(first, "Pointer");
            var secondPointer = ReadMember(second, "Pointer");
            if (firstPointer is IntPtr left && secondPointer is IntPtr right)
                return left != IntPtr.Zero && left == right;
            return AreSameUnityObject(ReadMember(first, "gameObject"), ReadMember(second, "gameObject"));
        }
        catch
        {
            return false;
        }
    }

    private static bool AreSameUnityObject(object? first, object? second)
    {
        if (first == null || second == null)
            return false;
        if (ReferenceEquals(first, second) || Equals(first, second))
            return true;

        var firstPointer = ReadMember(first, "Pointer");
        var secondPointer = ReadMember(second, "Pointer");
        if (firstPointer is IntPtr leftPointer && secondPointer is IntPtr rightPointer)
            return leftPointer == rightPointer;

        try
        {
            var getInstanceId = first.GetType().GetMethod("GetInstanceID", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            var firstId = getInstanceId?.Invoke(first, null);
            var secondId = getInstanceId?.Invoke(second, null);
            return firstId is int leftId && secondId is int rightId && leftId == rightId;
        }
        catch
        {
            return false;
        }
    }

    private static object? ReadMember(object? target, string name)
    {
        return ReflectionReader.ReadMember(target, name);
    }

    private static IEnumerable<(string Name, Type Type, object? Value)> ReadMembers(object target)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = target.GetType();

        foreach (var field in type.GetFields(flags))
        {
            if (field.IsStatic || !IsPotentialControlType(field.FieldType))
                continue;

            object? value;
            try
            {
                value = field.GetValue(target);
            }
            catch
            {
                continue;
            }

            yield return (field.Name, field.FieldType, value);
        }

        foreach (var property in type.GetProperties(flags))
        {
            var getter = property.GetGetMethod(nonPublic: true);
            if (getter == null || getter.IsStatic || property.GetIndexParameters().Length != 0 || !IsPotentialControlType(property.PropertyType))
                continue;

            object? value;
            try
            {
                value = property.GetValue(target);
            }
            catch
            {
                continue;
            }

            yield return (property.Name, property.PropertyType, value);
        }
    }

    private static void SetMember(object target, string name, object value)
    {
        var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.CanWrite == true)
        {
            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            property.SetValue(target, targetType.IsInstanceOfType(value) ? value : Convert.ChangeType(value, targetType));
            return;
        }

        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            var targetType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
            field.SetValue(target, targetType.IsInstanceOfType(value) ? value : Convert.ChangeType(value, targetType));
            return;
        }

        throw new MissingMemberException(target.GetType().FullName, name);
    }

    private static void InvokeNoArgument(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        if (method == null)
            throw new MissingMethodException(target.GetType().FullName, methodName);

        method.Invoke(target, null);
    }

    private static void BlurInput(object input)
    {
        try
        {
            InvokeNoArgument(input, "DeactivateInputField");
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Could not deactivate the input field: {exception.Message}");
        }

        try
        {
            var eventSystemType = ReflectionReader.FindTypeByName("UnityEngine.EventSystems.EventSystem");
            var current = eventSystemType?.GetProperty("current", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
            if (current == null)
                return;

            var clearSelection = eventSystemType!.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(method =>
                {
                    var parameters = method.GetParameters();
                    return method.Name == "SetSelectedGameObject" && parameters.Length == 1;
                });
            clearSelection?.Invoke(current, new object?[] { null });
        }
        catch (Exception exception)
        {
            _logger?.Debug($"Could not clear the selected UI object: {exception.Message}");
        }
    }

    private static MethodInfo? FindUniqueNoArgumentMethod(Type type, string name)
    {
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == name && method.GetParameters().Length == 0)
            .ToArray();
        return methods.Length == 1 ? methods[0] : null;
    }

    /// <summary>
    /// Only methods declared by the screen itself are safe to patch: patching a method
    /// inherited from a game base class makes the IL2CPP trampoline re-enter itself.
    /// </summary>
    private static MethodInfo? FindDeclaredNoArgumentMethod(Type type, string name)
    {
        var method = FindUniqueNoArgumentMethod(type, name);
        return method != null && method.DeclaringType == type ? method : null;
    }

    private static void InitializeInputReflection()
    {
        if (_inputReady || DateTime.UtcNow < _nextInputInitialization)
            return;
        _nextInputInitialization = DateTime.UtcNow.AddSeconds(3);

        _inputType = ReflectionReader.FindTypeByName("UnityEngine.Input");
        var keyCodeType = ReflectionReader.FindTypeByName("UnityEngine.KeyCode");
        if (_inputType == null || keyCodeType == null)
        {
            _logger?.Warning("Accessible menu key input unavailable: UnityEngine.Input or KeyCode was not found.");
            return;
        }

        _getKey = _inputType.GetMethods(BindingFlags.Static | BindingFlags.Public)
            .FirstOrDefault(method => HasSingleKeyCodeParameter(method, "GetKey", keyCodeType));
        _getKeyDown = _inputType.GetMethods(BindingFlags.Static | BindingFlags.Public)
            .FirstOrDefault(method => HasSingleKeyCodeParameter(method, "GetKeyDown", keyCodeType));

        foreach (var keyName in new[] { "LeftControl", "RightControl", "LeftAlt", "RightAlt", "UpArrow", "DownArrow", "LeftArrow", "RightArrow", "Return", "KeypadEnter", "Escape", "F1", "R" })
        {
            if (Enum.TryParse(keyCodeType, keyName, out var keyCode) && keyCode != null)
                KeyCodes[keyName] = keyCode;
        }

        _inputReady = _getKey != null && _getKeyDown != null && KeyCodes.Count >= 12;
        if (!_inputReady)
            _logger?.Warning("Accessible menu key input unavailable: required Unity input members were not resolved.");
        else
            _logger?.Info("Accessible menu keyboard input is ready.");
    }

    private static bool HasSingleKeyCodeParameter(MethodInfo method, string name, Type keyCodeType)
    {
        var parameters = method.GetParameters();
        return method.Name == name && parameters.Length == 1 && parameters[0].ParameterType == keyCodeType;
    }

    private static bool HasAccessibilityModifiers()
    {
        return IsKeyHeld("LeftControl") || IsKeyHeld("RightControl")
            ? IsKeyHeld("LeftAlt") || IsKeyHeld("RightAlt")
            : false;
    }

    private static bool IsKeyHeld(string keyName) => InvokeKeyMethod(_getKey, keyName);
    private static bool IsKeyDown(string keyName) => InvokeKeyMethod(_getKeyDown, keyName);

    private static bool InvokeKeyMethod(MethodInfo? method, string keyName)
    {
        if (!_inputReady || method == null || !KeyCodes.TryGetValue(keyName, out var keyCode))
            return false;

        return method.Invoke(null, new[] { keyCode }) is bool result && result;
    }

    private sealed class MenuOption
    {
        public MenuOption(object control, ControlKind kind, string label)
        {
            Control = control;
            Kind = kind;
            Label = label;
        }

        public object Control { get; }
        public ControlKind Kind { get; }
        public string Label { get; }
    }

    private enum ControlKind
    {
        Button,
        Toggle,
        Slider,
        Dropdown,
        InputField
    }
}
