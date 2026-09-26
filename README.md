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
- **Histórico de downloads** — painel com o resultado (sucesso, falha, cancelado) de cada item baixado na sessão.

## Instalação (usuário final)

1. Acesse a aba [Releases](https://github.com/wandersonstt/Youtube-Downloader-Pro/releases) deste repositório.
2. Baixe o `YoutubeDownloaderCS.zip` da versão mais recente.
3. Extraia para uma pasta de sua preferência e execute `YoutubeDownloaderCS.exe`.
4. Na primeira execução, o app baixa automaticamente `ffmpeg.exe` e `yt-dlp.exe` — não é necessário instalar nada manualmente.

> O executável distribuído nas Releases é *self-contained*: não exige o runtime do .NET instalado na máquina do usuário.

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
| [`Preferencias.cs`](Preferencias.cs) | Persiste a última pasta de destino usada. |
| [`TelaCarregamento.cs`](TelaCarregamento.cs) / [`TelaDoacao.cs`](TelaDoacao.cs) | Janelas auxiliares (loading e doação). |
| [`Program.cs`](Program.cs) | Ponto de entrada da aplicação. |

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

A Action então builda um executável *self-contained*, publica a Release no GitHub com o zip anexado, e atualiza automaticamente o [`update.xml`](update.xml) — que é o arquivo lido pelo AutoUpdater.NET em todos os apps já instalados. Nenhum passo manual é necessário além do `tag`/`push`.

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
