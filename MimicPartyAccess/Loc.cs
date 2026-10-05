using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace MimicPartyAccess;

/// <summary>
/// Small localization catalog for announcements produced by the mod.
/// </summary>
internal static class Loc
{
    private static readonly IReadOnlyDictionary<string, (string Portuguese, string English)> Strings =
        new Dictionary<string, (string Portuguese, string English)>
        {
            ["mod_loaded"] = ("Mimic Party Access carregado.", "Mimic Party Access loaded."),
            ["phase_announce"] = ("Nova rodada. Prepare-se para ouvir o som.", "New round. Get ready to hear the sound."),
            ["phase_reveal"] = ("Som de referência. Ouça com atenção.", "Reference sound. Listen carefully."),
            ["phase_recording"] = ("Sua vez de imitar. Gravação em andamento.", "Your turn to imitate. Recording is in progress."),
            ["phase_playback"] = ("Ouça as imitações dos jogadores.", "Listen to the players' imitations."),
            ["phase_peer_scoring"] = ("Avalie as imitações.", "Rate the imitations."),
            ["phase_round_results"] = ("Resultado da rodada.", "Round results."),
            ["phase_wheel"] = ("Roleta da rodada: bônus ou sabotagem.", "Round wheel: bonus or sabotage."),
            ["phase_idle"] = ("Aguardando a próxima rodada.", "Waiting for the next round."),
            ["phase_intro"] = ("Introdução da partida.", "Game introduction."),
            ["phase_hook_unavailable"] = (
                "A leitura das fases do jogo não pôde ser ativada nesta versão.",
                "Game phase announcements could not be enabled for this version."),
            ["screen_main_menu"] = ("menu principal", "main menu"),
            ["screen_matchmaking"] = ("busca de partida", "matchmaking"),
            ["screen_lobby"] = ("sala", "lobby"),
            ["screen_solo"] = ("modo solo", "solo mode"),
            ["screen_game"] = ("partida", "game"),
            ["screen_results"] = ("resultados", "results"),
            ["screen_theme_creator"] = ("criador de pacotes de sons", "sound pack creator"),
            ["screen_playtest_popup"] = ("aviso sobre o Mimic Party no celular", "Mimic Party mobile announcement"),
            ["screen_settings"] = ("configurações", "settings"),
            ["screen_confirm"] = ("confirmação", "confirmation"),
            ["screen_account_link"] = ("vincular conta", "link account"),
            ["screen_game_mode"] = ("modo de jogo", "game mode"),
            ["screen_room_browser"] = ("buscar sala", "find room"),
            ["screen_skin_panel"] = ("personagens", "characters"),
            ["screen_aura_panel"] = ("auras", "auras"),
            ["screen_reveal_animation_panel"] = ("animações de entrada", "entrance animations"),
            ["screen_invite_friend_panel"] = ("convidar amigo", "invite friend"),
            ["screen_friends_panel"] = ("amigos", "friends"),
            ["screen_shop_panel"] = ("loja", "shop"),
            ["screen_season_pass_panel"] = ("passe de temporada", "season pass"),
            ["screen_objectives_panel"] = ("objetivos", "objectives"),
            ["screen_name_panel"] = ("nome", "name"),
            ["screen_theme_preview_panel"] = ("pré-visualização do tema", "theme preview"),
            ["screen_discord_panel"] = ("discord", "discord"),
            ["screen_player_report_panel"] = ("denunciar jogador", "report player"),
            ["screen_country_preference_panel"] = ("país", "country"),
            ["screen_pack_feedback_panel"] = ("avaliar pacote", "rate pack"),
            ["screen_theme_feedback_panel"] = ("avaliar tema", "rate theme"),
            ["screen_theme_discord_popup"] = ("discord do tema", "theme discord"),
            ["screen_generic"] = ("tela", "screen"),
            ["menu_opened"] = ("{0} aberto.", "{0} opened."),
            ["menu_closed"] = ("{0} fechado.", "{0} closed."),
            ["menu_press_key"] = (
                "Pressione a tecla do atalho, ou Esc para cancelar.",
                "Press the shortcut key, or Escape to cancel."),
            ["language_changed"] = (
                "Idioma do mod alterado para o idioma do jogo.",
                "Mod language changed to the game language."),
            ["gate_ban"] = (
                "Atenção: há um bloqueio de conta ativo. O jogo pode impedir partidas.",
                "Warning: an account ban gate is active. The game may block matches."),
            ["gate_maintenance"] = (
                "O jogo está em manutenção. Partidas podem estar indisponíveis.",
                "The game is under maintenance. Matches may be unavailable."),
            ["gate_ownership"] = (
                "Verificação de posse do jogo pendente. Partidas podem estar bloqueadas.",
                "Game ownership verification is pending. Matches may be blocked."),
            ["gate_update"] = (
                "Há uma atualização pendente. Atualize o jogo na Steam.",
                "An update is pending. Update the game on Steam."),
            ["menu_no_buttons"] = ("Nenhum controle acessível foi detectado nesta tela.", "No accessible controls were detected on this screen."),
            ["menu_no_selection"] = ("Nenhuma opção selecionada.", "No option is selected."),
            ["menu_item"] = ("{0} de {1}: {2}.", "{0} of {1}: {2}."),
            ["menu_activating"] = ("Ativando {0}.", "Activating {0}."),
            ["menu_cannot_activate"] = ("Não foi possível ativar {0}.", "Could not activate {0}."),
            ["menu_unknown_button"] = ("botão sem rótulo", "unlabelled button"),
            ["menu_help"] = (
                "Navegação: setas para cima e para baixo movem entre controles. Setas para esquerda e direita alteram controles de valor. Enter ativa ou edita. Control mais Alt mais F1 repete a ajuda. Control mais Alt mais R repete a última fala.",
                "Navigation: Up and Down move between controls. Left and Right change value controls. Enter activates or edits. Control plus Alt plus F1 repeats help. Control plus Alt plus R repeats the last announcement."),
            ["menu_toggle_changed"] = ("{0}: {1}.", "{0}: {1}."),
            ["menu_input_focus"] = ("Campo {0} ativo. Digite o texto usando o teclado.", "{0} field active. Type text using the keyboard."),
            ["menu_input_blur"] = ("Campo fechado. As setas voltam a navegar pelos controles.", "Field closed. The arrow keys navigate the controls again."),
            ["menu_dropdown_opened"] = ("Lista de {0} aberta. Use as setas para escolher.", "{0} list opened. Use the arrow keys to choose."),
            ["menu_dropdown_opened_options"] = (
                "Lista de {0} aberta com {1} opções: {2}. Use as setas para escolher.",
                "{0} list opened with {1} options: {2}. Use the arrow keys to choose."),
            ["menu_value"] = ("{0}: {1}.", "{0}: {1}."),
            ["menu_empty_dropdown"] = ("{0} não tem opções disponíveis.", "{0} has no available options."),
            ["state_on"] = ("ativado", "on"),
            ["state_off"] = ("desativado", "off"),
            ["percent"] = (" por cento", " percent"),
            ["round_started"] = ("Rodada {0} de 5.", "Round {0} of 5."),
            ["score_result"] = ("Rodada {0}. {1}: {2} pontos.", "Round {0}. {1}: {2} points."),
            ["score_unavailable"] = ("Pontuação não disponível.", "Score is unavailable."),
            ["lobby_players"] = ("Jogadores na sala. {0}", "Players in the lobby. {0}"),
            ["lobby_ready"] = ("pronto", "ready"),
            ["lobby_waiting"] = ("aguardando", "waiting"),
            ["results_summary"] = ("Classificação final. {0}", "Final ranking. {0}"),
            ["results_player"] = ("{0}º lugar, {1}, {2}.", "Place {0}, {1}, {2}."),
            ["round_scores"] = ("Pontos da rodada: {0}.", "Round points: {0}."),
            ["round_leader"] = ("Maior pontuação: {0} com {1} pontos.", "Top score: {0} with {1} points."),
            ["round_your_score"] = ("Seu placar: {0} pontos.", "Your score: {0} points."),
            ["record_your_turn"] = ("Sua vez de falar!", "Your turn to speak!"),
            ["record_countdown"] = ("Contagem: {0}.", "Countdown: {0}."),
            ["ptt_toggle"] = ("apertar para falar", "push to talk"),
            ["ptt_key"] = ("tecla para falar", "talk key"),
            ["hint_ptt_toggle"] = (
                "Pressione Enter para ligar ou desligar.",
                "Press Enter to turn it on or off."),
            ["hint_ptt_key"] = (
                "Pressione Enter e depois a nova tecla desejada.",
                "Press Enter and then the new key you want."),
            ["ptt_key_set"] = ("Nova tecla para falar: {0}.", "New talk key: {0}."),
            ["ptt_key_unchanged"] = ("Tecla para falar mantida: {0}.", "Talk key kept: {0}.")
        };

    public static string Get(string key, params object[] arguments)
    {
        if (!Strings.TryGetValue(key, out var pair))
            return key;

        var template = ActiveLanguage == "pt" ? pair.Portuguese : pair.English;
        return string.Format(CultureInfo.CurrentCulture, template, arguments);
    }

    private static string? _osLanguage;
    private static string? _baseLanguage;

    private static string ActiveLanguage => _baseLanguage ?? OsLanguage;

    public static string CurrentLanguage => ActiveLanguage;

    public static string OsLanguageCode => OsLanguage;

    public static string? BaseLanguage => _baseLanguage;

    /// <summary>
    /// Unity threads usually run under an English culture, so the OS display
    /// language is read directly instead of trusting the thread culture.
    /// </summary>
    private static string OsLanguage
    {
        get
        {
            if (_osLanguage != null)
                return _osLanguage;

            try
            {
                var uiLanguage = GetUserDefaultUILanguage() & 0x3FF;
                if (uiLanguage == 0x16)
                {
                    _osLanguage = "pt";
                    return _osLanguage;
                }

                var buffer = new StringBuilder(85);
                var length = GetUserDefaultLocaleName(buffer, buffer.Capacity);
                if (length > 0 && buffer.ToString().StartsWith("pt", StringComparison.OrdinalIgnoreCase))
                    _osLanguage = "pt";
                else
                    _osLanguage = "en";
            }
            catch
            {
                _osLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? "pt" : "en";
            }

            return _osLanguage;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetUserDefaultLocaleName(StringBuilder lpLocaleName, int cchLocaleName);

    public static int LanguageRevision { get; private set; }

    /// <summary>
    /// Applies the language the game itself is using. Returns true when the language changed.
    /// </summary>
    public static bool SetBaseLanguage(string? twoLetterLanguage)
    {
        if (_baseLanguage == twoLetterLanguage)
            return false;

        _baseLanguage = twoLetterLanguage;
        LanguageRevision++;
        return true;
    }

    private static readonly IReadOnlyDictionary<string, string> FallbackTranslations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["play"] = "Jogar",
            ["settings"] = "Configurações",
            ["quit"] = "Sair",
            ["theme creator"] = "Criador de temas",
            ["create room"] = "Criar sala",
            ["create"] = "Criar",
            ["objectives"] = "Objetivos",
            ["info"] = "Informações",
            ["invite"] = "Convidar",
            ["options tab"] = "Aba de opções",
            ["push to talk"] = "Apertar para falar",
            ["push to talk key"] = "Tecla de apertar para falar",
            ["link device"] = "Vincular dispositivo",
            ["resume"] = "Continuar",
            ["sensitivity"] = "Sensibilidade",
            ["voice"] = "Voz",
            ["music"] = "Música",
            ["sfx"] = "Efeitos sonoros",
            ["mic"] = "Microfone",
            ["microphone"] = "Microfone",
            ["language"] = "Idioma",
            ["region"] = "Região",
            ["display"] = "Tela",
            ["delete account"] = "Excluir conta",
            ["terms"] = "Termos",
            ["privacy"] = "Privacidade",
            ["restore"] = "Restaurar",
            ["close"] = "Fechar",
            ["cancel"] = "Cancelar",
            ["confirm"] = "Confirmar",
            ["back"] = "Voltar",
            ["country"] = "País",
            ["classic"] = "Clássico",
            ["versus"] = "Versus",
            ["solo"] = "Sozinho",
            ["join"] = "Entrar",
            ["code"] = "Código",
            ["quick match"] = "Partida rápida",
            ["search"] = "Buscar",
            ["save"] = "Salvar",
            ["publish"] = "Publicar",
            ["import"] = "Importar",
            ["record"] = "Gravar",
            ["delete"] = "Excluir",
            ["copy"] = "Copiar",
            ["leave"] = "Sair",
            ["start"] = "Iniciar",
            ["menu"] = "Menu",
            ["rematch"] = "Revanche",
            ["play again"] = "Jogar novamente",
            ["retry"] = "Tentar de novo",
            ["sounds"] = "Sons",
            ["packs"] = "Pacotes",
            ["account"] = "Conta",
            ["send"] = "Enviar",
            ["room"] = "Sala",
            ["rooms"] = "Salas",
            ["players"] = "Jogadores",
            ["rounds"] = "Rodadas",
            ["wheel"] = "Roleta",
            ["prize"] = "Prêmio",
            ["daily"] = "Diário",
            ["streak"] = "Sequência",
            ["store"] = "Loja",
            ["buy"] = "Comprar",
            ["skins"] = "Aparências",
            ["themes"] = "Temas",
            ["my packs"] = "Meus pacotes",
            ["create pack"] = "Criar pacote",
            ["join code"] = "Código da sala",
            ["submit code"] = "Enviar código",
            ["join random"] = "Entrar em sala aleatória",
            ["filter"] = "Filtro",
            ["random"] = "Aleatória",
            ["free"] = "Grátis",
            ["gratis"] = "Grátis",
            ["all"] = "Todas",
            ["refresh"] = "Atualizar",
            ["apply"] = "Aplicar",
            ["loading"] = "Carregando",
            ["empty"] = "Vazia",
            ["none"] = "Nenhuma",
            ["ok"] = "Ok",
        };

    /// <summary>
    /// Translates identifier-derived English fallback labels to Portuguese when
    /// the mod is running in Portuguese. Game-provided texts pass through untouched.
    /// </summary>
    public static string TranslateFallback(string label)
    {
        if (ActiveLanguage == "pt" && FallbackTranslations.TryGetValue(label.Trim(), out var translated))
            return translated;

        return label;
    }
}
