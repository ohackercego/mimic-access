# Mimic Party — API e compatibilidade

## Identificação

- Engine: Unity 6000.4.2f1
- Runtime: IL2CPP
- Arquitetura: Windows x64
- Build da pasta recebida: 25319031
- Mod loader: MelonLoader 0.7.3

## Evidências locais

- `GameAssembly.dll` presente
- `Mimic Party_Data/il2cpp_data/Metadata/global-metadata.dat` presente
- Assembly-CSharp C# decompilado: indisponível
- Tabelas de localização em Addressables: inglês, espanhol, francês, japonês e chinês simplificado
- Tolk DLLs: `Tolk.dll` e `nvdaControllerClient64.dll` presentes na pasta do executável
- Mod DLL: `Mods\MimicPartyAccess.dll`, carregado pelo MelonLoader

## Bindings e entrada

O dump não revelou handlers de teclado nos controladores dessas telas. O mod trata setas e Enter quando uma tela identificada está ativa. O prefixo do `EventSystem.Update()` pula o processamento do EventSystem somente no frame de navegação consumido; em campos de texto focados, o teclado permanece normal.

## UI e eventos

Tipos e métodos encontrados no dump de IL2CPP desta pasta, em `Assembly-CSharp.dll`:

- `Mimick.UI.MainMenuScreen`: botões Play, Solo, Settings e Quit; entrada para código de sala.
- `Mimick.UI.LobbyScreen`: rótulos de jogadores/tema/código e controles para iniciar/sair.
- `Mimick.UI.GameScreen`: `OnPhaseChanged(Mimick.Data.RoundPhase)`, `OnRoundStarted(int)`, `OnRecordTurnStarted(MimickPlayer)`, `OnPlaybackStarted(MimickPlayer, PlayerRecording)`, `OnScored(MimickPlayer, PlayerRecording, ScoreResult)`; campos visuais `roundLabel`, `phaseLabel`, `soundThemeLabel`, `soundLabel`, `statusLabel`, `scoreLabel` e `bonusLabel`.
- `Mimick.UI.ResultsScreen`: classificação, estatísticas, jogar novamente e voltar ao menu.
- `Mimick.UI.ThemeCreatorScreen`: campos de nome/descrição, importação e gravação de sons, salvar/publicar pacote e voltar.
- `Mimick.UI.ResultCardUI`: campos de colocação, nome e pontuação usados para leitura do placar final.
- `Mimick.UI.PlayerLobbyRowUI`: campos de nome e estado de prontidão de cada linha do lobby.
- `Mimick.Gameplay.RoundController`: eventos públicos `RoundStarted`, `PhaseChanged`, `RecordTurnStarted`, `PlaybackStarted`, `Scored`, `GameFinished`, `WheelStopped` e `WheelTargetingStarted`.
- `Mimick.Data.RoundPhase`: `Idle`, `Announce`, `Reveal`, `Recording`, `Playback`, `PeerScoring`, `RoundResults`, `Wheel`.
- `Mimick.Data.MimickPlayer.DisplayName`: nome público do participante.

Primeiro hook implementado: postfix em `Mimick.UI.GameScreen.OnPhaseChanged(Mimick.Data.RoundPhase)`. A instalação verifica a presença do tipo, o conjunto exato de valores e uma única assinatura compatível antes de aplicar Harmony.

Hooks adicionais usados para anúncios:

- `GameScreen.OnRoundStarted(int)` e `GameScreen.OnScored(MimickPlayer, PlayerRecording, ScoreResult)`.
- `LobbyScreen.RefreshRoster()` para ler as linhas de participantes e prontidão.
- `ResultsScreen.OpenReplayWindow()` para ler os cartões de colocação, nomes e pontuações após a animação.
- `Start()` nas telas principais para anunciar o contexto e reunir botões ativos.

A navegação atua sobre campos e propriedades de controles públicos/privados da tela ativa: Button, SettingsButton, InputField, Toggle, Slider e Dropdown/TMP_Dropdown. Os overlays `PauseMenu` e `PlaytestPopup` são priorizados quando abertos; a seleção atual do EventSystem é anunciada quando muda.
A navegação também reconhece `Il2CppMimick.UI.PauseMenu`, `PlaytestPopup` e o controle especial `SettingsButton`. Os bindings gerados para o build Steam prefixam os namespaces de jogo com `Il2Cpp`.

## Fingerprints observados

- GameAssembly: `B0E3B58874C032D9C98FAB77721B0DEB9CCD0B6CFE5C8EB41C5E2511F8955859`
- global-metadata: `D2164064AD42457F27ABC369817BD0E002E1B0026BE80AFAAC811FF9E1F84654`

O mod registra esses hashes no log a cada inicialização. Se uma atualização do jogo alterar os binários, os hooks cujo contrato não confere mais são ignorados e registrados.

## Segurança de compatibilidade

- Nenhum offset nativo usado.
- Harmony usado somente para hooks gerenciados validados e o update do EventSystem.
- Nenhum offset nativo usado; nenhum binário nativo do jogo alterado.
- O plugin registra o fingerprint exato para permitir validação antes de novas features.
