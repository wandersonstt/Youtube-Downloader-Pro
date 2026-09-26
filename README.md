# Youtube Downloader Pro

Aplicativo desktop para Windows que baixa vídeos e playlists do YouTube (e, em modo universal, de outros sites suportados pelo `yt-dlp`), com conversão para MP3 e gerenciamento automático de dependências.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)
![Release](https://img.shields.io/github/v/release/wandersonstt/Youtube-Downloader-Pro)

## Funcionalidades

- **Múltiplas qualidades de vídeo** — de 360p a 4K/8K, escolhidas a partir dos streams reais disponíveis para o vídeo.
- **Áudio em MP3 ou formato original** — extração em MP3 (com capa embutida automaticamente) ou download do áudio nativo (M4A/WebM) sem reconversão.
- **Download de playlists inteiras** — detecta o link, pergunta se deve baixar todos os vídeos e processa em lote com progresso e histórico por item.
- **Modo Universal (fallback via `yt-dlp`)** — quando o YouTube bloqueia a extração padrão, ou o link é de outro site, o app usa `yt-dlp` diretamente, oferecendo as mesmas opções de resolução e progresso em tempo real.
- **Cancelamento real** — interrompe o download (e a etapa de capa/conversão) a qualquer momento, sem deixar arquivos parciais corrompidos.
- **Dependências autogerenciadas** — `ffmpeg.exe` e `yt-dlp.exe` são baixados e mantidos atualizados automaticamente (yt-dlp sempre na última versão publicada, via API do GitHub), sem intervenção do usuário.
- **Auto-update do próprio aplicativo** — verifica novas versões no GitHub a cada abertura e oferece a atualização.
- **Histórico e log integrados** — abas na própria janela com o resultado de cada download e o log técnico do aplicativo, para diagnóstico sem sair do programa.
- **Contorno do bloqueio de login do YouTube** — quando o YouTube exige autenticação ("Sign in to confirm you're not a bot"), o app tenta automaticamente reaproveitar os cookies do navegador onde você já está logado.

## Instalação (usuário final)

Acesse a aba [Releases](https://github.com/wandersonstt/Youtube-Downloader-Pro/releases) e escolha uma das opções:

### Opção recomendada: instalador (`YoutubeDownloaderPro-Setup.exe`)

1. Baixe o `YoutubeDownloaderPro-Setup.exe` da versão mais recente.
2. Execute e siga o assistente (Avançar → Instalar).
3. O programa é instalado com atalho na Área de Trabalho e no Menu Iniciar, e aparece em "Adicionar ou remover programas" do Windows para desinstalar depois.

### Opção portátil (`YoutubeDownloaderCS.zip`)

1. Baixe o `.zip` da versão mais recente.
2. Extraia para uma pasta de sua preferência e execute `YoutubeDownloaderCS.exe` — não instala nada no sistema, ideal para pen drive ou uso sem privilégios de administrador.

Em ambos os casos, na primeira execução o app baixa automaticamente `ffmpeg.exe` e `yt-dlp.exe` — não é necessário instalar nada manualmente.

> O executável é *self-contained*: não exige o runtime do .NET instalado na máquina do usuário.

## Como usar

1. Cole a URL do vídeo ou playlist e clique em **Analisar**.
2. Escolha a qualidade/formato desejado na lista.
3. Clique em **Baixar** e selecione onde salvar (o app lembra a última pasta usada).
4. Acompanhe o progresso na barra e cancele a qualquer momento pelo botão **Cancelar**, se precisar.

## Arquitetura

O projeto é um WinForms (.NET 10) organizado por responsabilidade:

| Arquivo | Responsabilidade |
|---|---|
| [`Form1.cs`](Form1.cs) | Interface e orquestração dos fluxos de busca/download. |
| [`DependencyUpdater.cs`](DependencyUpdater.cs) | Verifica e baixa `ffmpeg.exe`/`yt-dlp.exe`, com download atômico e resolução da versão mais recente do yt-dlp via API do GitHub. |
| [`YtDlpDownloader.cs`](YtDlpDownloader.cs) | Executa o `yt-dlp` no modo Universal, reportando progresso real e suportando cancelamento. |
| [`Preferencias.cs`](Preferencias.cs) | Persiste a última pasta de destino e o navegador usado para cookies. |
| [`Logger.cs`](Logger.cs) | Registra eventos e erros em `log.txt` (exibido na aba **Log** do app). |
| [`Util.cs`](Util.cs) | Extração de link colado e sanitização de nome de arquivo. |
| [`Selftest.cs`](Selftest.cs) | Testes automatizados das regras que não dependem de rede nem de interação. |
| [`TelaCarregamento.cs`](TelaCarregamento.cs) / [`TelaDoacao.cs`](TelaDoacao.cs) | Janelas auxiliares (loading e doação). |
| [`Program.cs`](Program.cs) | Ponto de entrada, tratamento global de erros e diagnóstico inicial. |

### "O YouTube está exigindo login"

O YouTube passou a bloquear parte dos vídeos com a mensagem *"Sign in to confirm you're not a bot"*. Nesse caso o app tenta automaticamente reaproveitar os cookies do navegador, e o que costuma resolver é:

1. **Feche o navegador completamente** (todas as janelas) e clique em Baixar de novo — com o navegador aberto, o Windows trava o arquivo de cookies e o download falha com *"Could not copy cookie database"*.
2. Chrome e Edge recentes protegem os cookies de um jeito que impede a leitura (*"Failed to decrypt with DPAPI"*), mesmo fechados. Nesses casos, use o **Firefox** logado no YouTube.
3. Alternativa para qualquer navegador: exporte um arquivo **`cookies.txt`** (formato Netscape, via extensão de navegador) e coloque-o na pasta de instalação do programa — ele passa a ser usado automaticamente.

### Diagnóstico

O app grava um `log.txt` na própria pasta de instalação, visível na aba **Log** da janela principal (com botões para atualizar, abrir a pasta e limpar). Cada inicialização registra versão, se está elevado como administrador, a pasta de trabalho e o estado de `ffmpeg.exe`/`yt-dlp.exe` — o suficiente para diagnosticar a maioria dos problemas sem reproduzir o caso.

### Testes

```bash
dotnet build
dotnet bin/Debug/net10.0-windows/YoutubeDownloaderCS.dll --selftest
```

Cobre a montagem dos argumentos do `yt-dlp` (inclusive a presença obrigatória de `--no-playlist`), a leitura do progresso e das etapas, a detecção do bloqueio de login do YouTube, a sanitização de nomes de arquivo, a extração de links e o layout da janela. Os testes rodam pela DLL porque o `.exe` exige elevação.

### Fluxo de download

```
URL informada
     │
     ▼
Contém "youtube.com"/"youtu.be"? ──não──► Modo Universal (yt-dlp)
     │ sim
     ▼
Tem "list="? ──sim──► Playlist (YoutubeExplode, download em lote)
     │ não
     ▼
YoutubeExplode consegue os streams? ──não──► Modo Universal (yt-dlp)
     │ sim
     ▼
Lista qualidades reais (vídeo/áudio) e baixa via YoutubeExplode + FFmpeg
```

## Tecnologias

- **Linguagem/Runtime:** C# / .NET 10 (Windows Forms)
- **Extração/Download:** [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) + [YoutubeExplode.Converter](https://github.com/Tyrrrz/YoutubeExplode)
- **Fallback universal:** [yt-dlp](https://github.com/yt-dlp/yt-dlp)
- **Conversão/mux de mídia:** [FFmpeg](https://ffmpeg.org/) ([builds BtbN](https://github.com/BtbN/FFmpeg-Builds))
- **Auto-update do app:** [AutoUpdater.NET](https://github.com/ravibpatel/AutoUpdater.NET)

## Compilando localmente

Pré-requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download) e Windows.

```bash
git clone https://github.com/wandersonstt/Youtube-Downloader-Pro.git
cd Youtube-Downloader-Pro
dotnet restore
dotnet build
dotnet run
```

## Publicando uma nova versão (mantenedores)

O repositório tem um workflow de release automatizado ([`.github/workflows/release.yml`](.github/workflows/release.yml)). Para publicar uma nova versão:

```bash
git tag X.Y.Z.W
git push origin X.Y.Z.W
```

A Action então builda um executável *self-contained*, gera o `YoutubeDownloaderCS.zip` (portátil) e o `YoutubeDownloaderPro-Setup.exe` (instalador, via [Inno Setup](installer/setup.iss)), publica a Release no GitHub com os dois arquivos anexados, e atualiza automaticamente o [`update.xml`](update.xml) — que é o arquivo lido pelo AutoUpdater.NET em todos os apps já instalados (o auto-update continua usando o `.zip` portátil). Nenhum passo manual é necessário além do `tag`/`push`.

## Contribuindo

1. Faça um fork do projeto.
2. Crie uma branch (`git checkout -b feature/minha-feature`).
3. Commit suas mudanças (`git commit -m 'Adiciona minha feature'`).
4. Push para a branch (`git push origin feature/minha-feature`).
5. Abra um Pull Request.

Bugs e sugestões podem ser reportados na aba [Issues](https://github.com/wandersonstt/Youtube-Downloader-Pro/issues).

## Aviso legal

Este software é fornecido para uso pessoal e educacional. O download de conteúdo protegido por direitos autorais sem autorização pode violar os Termos de Serviço do YouTube e leis de direitos autorais aplicáveis. O uso é de inteira responsabilidade do usuário.

## Licença

Distribuído sob a licença MIT. Veja [LICENSE.txt](LICENSE.txt) para mais detalhes.

---

<p align="center">Desenvolvido por <a href="https://github.com/wandersonstt">Wanderson</a></p>
