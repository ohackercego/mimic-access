# Mimic Party Access

**Mod de acessibilidade para leitores de tela em [Mimic Party](https://store.steampowered.com/app/5053820/) (Steam).**

> **English:** [Read this README in English](README.md).

Mimic Party Access permite que jogadores cegos ou com baixa visão naveguem pelos menus do jogo pelo teclado e ouvam o que está acontecendo pelo leitor de tela (NVDA, JAWS, Narrator e outros). Roda sobre o [MelonLoader](https://github.com/LavaGang/MelonLoader) e fala por meio do [Tolk](https://github.com/dkager/tolk). Nunca modifica `GameAssembly.dll` nem `global-metadata.dat`.

## Recursos

- **Navegação completa pelos menus no teclado**: setas para cima/baixo movem entre controles, esquerda/direita alteram sliders e listas, Enter ativa, Esc fecha overlays.
- **Anúncios das telas**: todos os menus (principal, configurações, buscar sala, lobby, partida, resultados, loja, passe de temporada, objetivos, amigos e mais) são anunciados ao abrir, e cada controle é anunciado com sua posição (`3 de 9: Criar sala`).
- **Anúncios da partida**: fases da rodada (anúncio, revelação, gravação, reprodução, pontuação, resultado, roleta), contador da rodada, pontuação de cada jogador, resumo do resultado da rodada, participantes do lobby e classificação final.
- **Avisos de gravação**: "Sua vez de falar!" quando sua vez de gravar começa, além da contagem anunciada para você saber quando falar e quando a gravação será cortada.
- **Apertar para falar nas configurações**: as duas opções de apertar para falar ficam totalmente acessíveis — estado atual e tecla atual são lidos, Enter alterna ou inicia a captura da tecla, e a nova tecla é anunciada ao ser definida.
- **Idioma segue o jogo**: anúncios em português quando o jogo está em português, em inglês caso contrário.
- **Linhas lidas corretamente**: linhas de sala, amigos, jogadores do lobby, itens da loja e objetivos são anunciados com os rótulos reais em vez de nomes internos de campos.
- **Não destrutivo**: apenas pós-fixos Harmony são usados, e cada hook é validado contra o contrato IL2CPP do jogo (tipo, método, tipos de parâmetros, valores de enumeração) antes de ser instalado. Hooks que não conferem são ignorados e registrados no log. Os binários do jogo nunca são alterados; seus hashes SHA-256 são registrados no log para identificação.

## Requisitos

- Mimic Party (Steam, Windows x64).
- [MelonLoader](https://github.com/LavaGang/MelonLoader/releases) instalado para o Mimic Party (testado com a 0.7.3).
- Um leitor de tela compatível com o Tolk (NVDA, JAWS, Narrator via SAPI e outros).
- Runtime .NET 6 (já exigido pelo MelonLoader).

## Instalação

1. **Instale o MelonLoader** (uma vez): baixe o instalador do MelonLoader na [página de releases](https://github.com/LavaGang/MelonLoader/releases), execute-o e selecione `Mimic Party.exe` na pasta da sua biblioteca Steam (geralmente `C:\Program Files (x86)\Steam\steamapps\common\Mimic Party`).
2. **Baixe o mod**: pegue `MimicPartyAccess-v*.zip` na página de Releases deste repositório.
3. **Extraia o zip** na pasta do Mimic Party. Ele cria:
   - `Mods\MimicPartyAccess.dll` — o mod
   - `Tolk.dll` e `nvdaControllerClient64.dll` na pasta do jogo — a ponte de fala (o DLL do NVDA só é necessário para o NVDA; outros leitores funcionam sem ele)
4. **Inicie o jogo** pela Steam. No log, a linha `Tolk screen-reader output is available.` indica que um leitor de tela foi detectado.

Para desinstalar, apague `Mods\MimicPartyAccess.dll` (e, opcionalmente, `Tolk.dll` / `nvdaControllerClient64.dll`).

## Controles

| Tecla | Ação |
| --- | --- |
| Cima / Baixo | Mover entre controles |
| Esquerda / Direita | Alterar valor de slider/lista, ou mover entre botões |
| Enter | Ativar botão, alternância, lista ou campo de texto |
| Esc | Fechar o overlay aberto (Configurações, popups) ou o campo de texto |
| Ctrl+Alt+F1 | Ler a ajuda de navegação |
| Ctrl+Alt+R | Repetir o último anúncio |

O mod só intercepta essas teclas enquanto uma tela acessível está ativa; digitar em campos de texto funciona normalmente.

## Compilando a partir do código

Requisitos: [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) e uma instalação do MelonLoader (os `MelonLoader.dll` e `0Harmony.dll` dela são referências de compilação).

```powershell
dotnet build MimicPartyAccess/MimicPartyAccess.MelonLoader.csproj -c Release `
  -p:MelonLoaderPath="C:\Program Files (x86)\Steam\steamapps\common\Mimic Party\MelonLoader\net6"
```

Saída: `MimicPartyAccess/bin/MelonLoader/Release/net6.0/MimicPartyAccess.dll`.

Execute as verificações de regressão (também exigem `-p:MelonLoaderPath=...`):

```powershell
dotnet build MimicPartyAccess/Tools/UiRegression/UiRegression.csproj -c Release `
  -p:MelonLoaderPath="C:\Program Files (x86)\Steam\steamapps\common\Mimic Party\MelonLoader\net6"
dotnet MimicPartyAccess/Tools/UiRegression/bin/Release/net7.0/UiRegression.dll
```

Para testar uma build local, feche o jogo e copie a DLL para `Mods\MimicPartyAccess.dll`.

## Solução de problemas

- Arquivo de log: `Mimic Party\MelonLoader\Latest.log`. Procure por `Mimic Party Access` para ver as mensagens do mod.
- Na inicialização o mod registra o SHA-256 de `GameAssembly.dll` e `global-metadata.dat`. Se uma atualização do jogo alterar esses arquivos, os hooks que não conferem mais são ignorados (o jogo continua funcionando, mas alguns anúncios param) — abra uma issue com o log.
- Sem fala: confira se `Tolk screen-reader output is available.` aparece no log; para o NVDA, garanta que `nvdaControllerClient64.dll` está ao lado de `Mimic Party.exe`.

## Como funciona

O mod usa reflexão sobre as assemblies proxy IL2CPP geradas pelo MelonLoader para encontrar os tipos de UI do jogo (`Mimick.UI.*`). Os hooks são pós-fixos Harmony em callbacks de UI como `GameScreen.OnPhaseChanged(RoundPhase)` ou `GameScreen.OnScored(...)`, instalados apenas quando o contrato em tempo de execução bate com o esperado. A descoberta de menus percorre a hierarquia de UI da tela ativa e associa cada controle ao seu rótulo visível do TextMeshPro. Veja [docs/game-api.md](MimicPartyAccess/docs/game-api.md) para a API do jogo pesquisada.

## Créditos e licenças

- Código-fonte deste mod: veja a licença do repositório.
- [Tolk](https://github.com/dkager/tolk) de Davy Kager — LGPL-3.0 (`Tolk.dll` incluído).
- Cliente controlador do [NVDA](https://www.nvaccess.org/) (`nvdaControllerClient64.dll` incluído).
- [MelonLoader](https://github.com/LavaGang/MelonLoader) e Harmony.

Este é um projeto de fã não oficial de acessibilidade e não é afiliado nem endossado pela FoliesAPP.
