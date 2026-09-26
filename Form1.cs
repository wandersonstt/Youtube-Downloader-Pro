using System;
using System.Drawing;
using System.Drawing.Imaging; // Importante para salvar em JPG
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Converter;
using YoutubeExplode.Common;
using AutoUpdaterDotNET;

namespace YoutubeDownloaderCS
{
    public partial class Form1 : Form
    {
        private readonly YoutubeClient youtube = new YoutubeClient();
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        // URLs
        private const string UrlXmlUpdate = "https://raw.githubusercontent.com/wandersonstt/Youtube-Downloader-Pro/refs/heads/main/update.xml";
        private const string LinkLivePix = "https://livepix.gg/alho97";

        // Variáveis de controle
        private StreamManifest? streamManifestAtual;
        private bool modoPlaylist = false;
        private IReadOnlyList<YoutubeExplode.Playlists.PlaylistVideo>? listaVideosPlaylist;
        private CancellationTokenSource? _cts;
        private string tituloVideoAtual = "";

        private YoutubeExplode.Videos.Video? videoAtual;
        private RichTextBox? txtHistorico;
        private RichTextBox? txtLog;
        private TabControl? tabsInferior;
        private string urlAnalisada = "";

        public Form1()
        {
            InitializeComponent();
            ConfigurarPainelInferior();
        }

        private void ConfigurarPainelInferior()
        {
            // Posiciona abaixo do último controle existente em vez de usar coordenadas fixas:
            // com escala de DPI acima de 100% os controles do designer se deslocam e o painel
            // antigo (com Y fixo) acabava sobreposto à barra de progresso.
            int baseY = 0;
            foreach (Control c in this.Controls) baseY = Math.Max(baseY, c.Bottom);
            baseY += 10;

            const int alturaPainel = 160;
            this.ClientSize = new Size(this.ClientSize.Width, baseY + alturaPainel + 12);

            tabsInferior = new TabControl
            {
                Location = new Point(12, baseY),
                Size = new Size(this.ClientSize.Width - 24, alturaPainel),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            var abaHistorico = new TabPage("Histórico") { BackColor = Color.FromArgb(40, 40, 40) };
            txtHistorico = CriarCaixaTexto(Color.LimeGreen);
            abaHistorico.Controls.Add(txtHistorico);

            var abaLog = new TabPage("Log") { BackColor = Color.FromArgb(40, 40, 40) };
            txtLog = CriarCaixaTexto(Color.Gainsboro);
            var barraLog = CriarBarraBotoesLog();
            abaLog.Controls.Add(txtLog);
            abaLog.Controls.Add(barraLog);

            tabsInferior.TabPages.Add(abaHistorico);
            tabsInferior.TabPages.Add(abaLog);
            tabsInferior.SelectedIndexChanged += (s, e) => { if (tabsInferior.SelectedTab == abaLog) AtualizarLog(); };
            this.Controls.Add(tabsInferior);
        }

        private static RichTextBox CriarCaixaTexto(Color corTexto) => new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = corTexto,
            Font = new Font("Consolas", 9),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        private Panel CriarBarraBotoesLog()
        {
            var barra = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Color.FromArgb(40, 40, 40) };

            Button CriarBotao(string texto, int x, EventHandler aoClicar)
            {
                var b = new Button
                {
                    Text = texto,
                    Location = new Point(x, 3),
                    Size = new Size(96, 24),
                    BackColor = Color.FromArgb(60, 60, 60),
                    ForeColor = Color.WhiteSmoke,
                    FlatStyle = FlatStyle.Flat
                };
                b.FlatAppearance.BorderSize = 0;
                b.Click += aoClicar;
                return b;
            }

            barra.Controls.Add(CriarBotao("Atualizar", 4, (s, e) => AtualizarLog()));
            barra.Controls.Add(CriarBotao("Abrir pasta", 104, (s, e) => AbrirPastaDoLog()));
            barra.Controls.Add(CriarBotao("Limpar", 204, (s, e) => { Logger.Limpar(); AtualizarLog(); }));
            return barra;
        }

        private void AtualizarLog()
        {
            if (txtLog == null) return;
            txtLog.Text = Logger.Ler();
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        private void AbrirPastaDoLog()
        {
            try
            {
                string caminho = Logger.CaminhoArquivo;
                string argumento = File.Exists(caminho) ? $"/select,\"{caminho}\"" : $"\"{Util.PastaApp}\"";
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = argumento, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Erro("Falha ao abrir a pasta do log", ex);
                MessageBox.Show("Não foi possível abrir a pasta: " + ex.Message);
            }
        }

        private void AdicionarHistorico(string status, string nome)
        {
            if (txtHistorico == null) return;

            if (txtHistorico.InvokeRequired)
            {
                txtHistorico.Invoke(new Action(() => AdicionarHistorico(status, nome)));
                return;
            }

            string hora = DateTime.Now.ToString("HH:mm:ss");
            string linha = $"[{hora}] {status}: {nome}\n";
            txtHistorico.AppendText(linha);
            txtHistorico.ScrollToCaret();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var loading = new TelaCarregamento("Iniciando sistemas...");
            loading.Show(this);
            Application.DoEvents();

            try
            {
                AutoUpdater.RunUpdateAsAdmin = false;
                AutoUpdater.DownloadPath = Util.PastaApp;
                AutoUpdater.AppTitle = "Youtube Downloader Pro";
                AutoUpdater.Start(UrlXmlUpdate);

                await VerificarDependencias(loading);
            }
            catch (Exception ex)
            {
                Logger.Erro("Erro na inicialização", ex);
                MessageBox.Show("Erro na inicialização: " + ex.Message);
            }
            finally
            {
                loading.Close();
            }
        }

        private async void btnAtualizar_Click(object sender, EventArgs e)
        {
            var loading = new TelaCarregamento("Verificando atualizações...");
            loading.Show(this);
            TravarInterface(true);

            try
            {
                await VerificarDependencias(loading);
                AutoUpdater.ReportErrors = true;
                AutoUpdater.Start(UrlXmlUpdate);
                loading.AtualizarMensagem("Verificação concluída!");
                await Task.Delay(1000);
            }
            catch (Exception ex) { Logger.Erro("Erro ao verificar atualizações", ex); MessageBox.Show("Erro: " + ex.Message); }
            finally { loading.Close(); TravarInterface(false); }
        }

        private async Task VerificarDependencias(TelaCarregamento loading)
        {
            var erros = await DependencyUpdater.VerificarTudoAsync(msg => loading.AtualizarMensagem(msg));
            if (erros.Count > 0) MessageBox.Show(string.Join("\n", erros));
        }

        private void PopularOpcoesUniversais()
        {
            cmbQualidade.Items.Add(new OpcaoDownload { Nome = "Melhor Qualidade (Automático)", IsGeneric = true, YtDlpFormato = FormatoCompativel(null) });
            foreach (var altura in new[] { 2160, 1440, 1080, 720, 480, 360 })
                cmbQualidade.Items.Add(new OpcaoDownload { Nome = $"Vídeo {altura}p (MP4)", IsGeneric = true, YtDlpFormato = FormatoCompativel(altura) });
            cmbQualidade.Items.Add(new OpcaoDownload { Nome = "Áudio MP3", IsGeneric = true, ExtrairAudioMp3 = true });
            cmbQualidade.SelectedIndex = 0;
        }

        // Prefere H.264 + AAC: o YouTube entrega AV1/Opus por padrão nas melhores qualidades,
        // e o Windows não reproduz esses codecs sem instalar extensões. Se não houver H.264
        // disponível, cai para o melhor formato existente.
        internal static string FormatoCompativel(int? altura)
        {
            string limite = altura.HasValue ? $"[height<={altura}]" : "";
            return $"bestvideo{limite}[vcodec^=avc1]+bestaudio[acodec^=mp4a]/"
                 + $"bestvideo{limite}+bestaudio/"
                 + $"best{limite}";
        }

        private async void btnBuscar_Click(object sender, EventArgs e)
        {
            var urlSuja = txtUrl.Text;
            var url = Util.ExtrairLink(urlSuja);
            if (url != urlSuja) txtUrl.Text = url;

            if (string.IsNullOrWhiteSpace(url)) return;

            Logger.Info($"Analisar clicado: {url}");
            var loading = new TelaCarregamento("Analisando link...");
            loading.Show(this); Application.DoEvents();

            try
            {
                TravarInterface(true); cmbQualidade.Items.Clear(); btnBaixar.Enabled = false; modoPlaylist = false;

                // Limpa o estado da análise anterior: sem isso, se o vídeo anterior tivesse
                // sido analisado com sucesso e o atual caísse no modo Universal, o app usava
                // o título e a capa do vídeo ANTERIOR no arquivo baixado.
                urlAnalisada = url;
                videoAtual = null;
                streamManifestAtual = null;
                listaVideosPlaylist = null;
                tituloVideoAtual = "";

                bool isYoutube = url.Contains("youtube.com") || url.Contains("youtu.be");

                if (!isYoutube)
                {
                    lblStatus.Text = "Link externo detectado (Universal)";
                    picThumbnail.Image = null;
                    tituloVideoAtual = await Task.Run(() => YtDlpDownloader.ObterTitulo(url, TimeSpan.FromSeconds(15))) ?? "Download Externo";
                    PopularOpcoesUniversais();
                    btnBaixar.Enabled = true;
                    return;
                }

                if (url.Contains("list="))
                {
                    loading.Close();
                    if (MessageBox.Show("Playlist detectada. Baixar todos?", "Playlist", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        loading = new TelaCarregamento("Carregando playlist..."); loading.Show(this); Application.DoEvents();
                        modoPlaylist = true;
                        var playlist = await youtube.Playlists.GetAsync(url);
                        listaVideosPlaylist = await youtube.Playlists.GetVideosAsync(playlist.Id);
                        lblStatus.Text = $"Playlist: {playlist.Title} ({listaVideosPlaylist.Count} vídeos)";
                        tituloVideoAtual = playlist.Title;
                        if (listaVideosPlaylist.Count > 0) picThumbnail.LoadAsync($"https://img.youtube.com/vi/{listaVideosPlaylist[0].Id}/hqdefault.jpg");
                        cmbQualidade.Items.Add(new OpcaoDownload { Nome = "Playlist: Apenas Áudio (MP3)", EhAudio = true });
                        cmbQualidade.Items.Add(new OpcaoDownload { Nome = "Playlist: Vídeo (Melhor Qualidade)", MaxAltura = 4320 });
                        cmbQualidade.SelectedIndex = 1; btnBaixar.Enabled = true;
                        return;
                    }
                    else { loading = new TelaCarregamento("Analisando vídeo..."); loading.Show(this); }
                }

                var video = await youtube.Videos.GetAsync(url);
                videoAtual = video; // Guardamos o vídeo para usar a ID na capa

                picThumbnail.LoadAsync($"https://img.youtube.com/vi/{video.Id}/hqdefault.jpg");
                tituloVideoAtual = video.Title;
                lblStatus.Text = $"Vídeo: {video.Title}";
                streamManifestAtual = await youtube.Videos.Streams.GetManifestAsync(url);
                var audioStream = streamManifestAtual.GetAudioOnlyStreams().GetWithHighestBitrate();
                if (audioStream != null)
                {
                    cmbQualidade.Items.Add(new OpcaoDownload { Nome = "Áudio MP3 (Alta Qualidade)", Stream = audioStream, EhAudio = true });
                    string extOriginal = audioStream.Container.Name == "mp4" ? "m4a" : audioStream.Container.Name;
                    cmbQualidade.Items.Add(new OpcaoDownload { Nome = $"Áudio Original {extOriginal.ToUpper()} (sem conversão)", Stream = audioStream, EhAudio = true, AudioNativo = true, Extensao = extOriginal });
                }
                var videoStreams = streamManifestAtual.GetVideoOnlyStreams().OrderByDescending(s => s.VideoQuality.MaxHeight);
                foreach (var stream in videoStreams)
                {
                    string tam = stream.Size.MegaBytes.ToString("F1");
                    cmbQualidade.Items.Add(new OpcaoDownload { Nome = $"Vídeo {stream.VideoQuality.Label} - {stream.Container} ({tam} MB)", Stream = stream, EhAudio = false });
                }
                if (cmbQualidade.Items.Count > 0) { cmbQualidade.SelectedIndex = 0; btnBaixar.Enabled = true; }
            }
            catch (Exception ex)
            {
                // ==========================================
                // SOLUÇÃO 3: FALLBACK (PLANO B) ADICIONADO AQUI
                // ==========================================
                Logger.Erro("YoutubeExplode falhou ao analisar link, caindo para modo Universal", ex);

                lblStatus.Text = "Modo de compatibilidade ativado (yt-dlp)";
                cmbQualidade.Items.Clear();
                modoPlaylist = false; // a análise da playlist pode ter falhado no meio
                tituloVideoAtual = videoAtual?.Title
                    ?? await Task.Run(() => YtDlpDownloader.ObterTitulo(url, TimeSpan.FromSeconds(15)))
                    ?? "Vídeo (Modo Universal)";
                PopularOpcoesUniversais();
                btnBaixar.Enabled = true;

                MessageBox.Show("Não foi possível analisar os formatos detalhados devido a uma restrição do YouTube.\nO download será realizado no modo Universal.",
                                "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally { TravarInterface(false); loading.Close(); }
        }

        // ==========================================
        // MÉTODO DE CAPA FINAL (CORRIGIDO E SEGURO)
        // ==========================================
        // Tenta a capa em alta resolução e cai para a padrão se o YouTube não tiver.
        // Descarta a primeira resposta antes de refazer a chamada (antes ela vazava).
        private async Task<HttpResponseMessage?> BaixarThumbnail(string videoId, CancellationToken token)
        {
            try
            {
                var resposta = await httpClient.GetAsync($"https://img.youtube.com/vi/{videoId}/maxresdefault.jpg", token);
                if (resposta.IsSuccessStatusCode) return resposta;

                resposta.Dispose();
                return await httpClient.GetAsync($"https://img.youtube.com/vi/{videoId}/hqdefault.jpg", token);
            }
            catch (Exception ex)
            {
                Logger.Erro("Falha ao baixar a capa", ex);
                return null;
            }
        }

        private async Task AdicionarCapa(string caminhoAudio, string videoId, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            string caminhoImagem = Path.ChangeExtension(caminhoAudio, ".jpg");
            string caminhoTemp = Path.ChangeExtension(caminhoAudio, ".temp.mp3");

            try
            {
                using (var response = await BaixarThumbnail(videoId, token))
                {
                    if (response == null || !response.IsSuccessStatusCode) return;

                    using var stream = await response.Content.ReadAsStreamAsync(token);
                    try
                    {
                        using var img = Image.FromStream(stream);
                        img.Save(caminhoImagem, ImageFormat.Jpeg);
                    }
                    catch
                    {
                        return; // Se a imagem for inválida, desiste da capa mas mantém o áudio
                    }
                }

                if (!File.Exists(caminhoImagem)) return;

                // 2. Chama o FFmpeg para juntar a capa com as FLAGS CERTAS
                string ffmpegPath = Util.CaminhoNaPastaApp("ffmpeg.exe");

                // -map 0:a -> Pega só o áudio do arquivo original
                // -map 1   -> Pega a imagem
                // -disposition:v:1 attached_pic -> FORÇA O WINDOWS A VER COMO CAPA
                string args = $"-i \"{caminhoAudio}\" -i \"{caminhoImagem}\" -map 0:a -map 1 -c copy -id3v2_version 3 -metadata:s:v title=\"Album cover\" -metadata:s:v comment=\"Cover (front)\" -disposition:v:1 attached_pic -y \"{caminhoTemp}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                bool ffmpegConcluiu = false;
                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        process.StandardInput.Close(); // evita travar esperando input que nunca virá
                        // O ffmpeg escreve bastante no stderr; sem drenar os fluxos ele trava
                        // quando o buffer do pipe enche, e a capa "expirava" no timeout.
                        process.OutputDataReceived += (s, ev) => { };
                        process.ErrorDataReceived += (s, ev) => { };
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        // Timeout de segurança de 30s OU cancelamento pedido pelo usuário
                        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                        {
                            cts.CancelAfter(TimeSpan.FromSeconds(30));
                            try
                            {
                                await process.WaitForExitAsync(cts.Token);
                                ffmpegConcluiu = process.ExitCode == 0;
                            }
                            catch (OperationCanceledException) { try { process.Kill(true); } catch { } }
                        }
                    }
                }

                // 3. Só substitui o áudio original se o ffmpeg realmente terminou bem.
                // Antes bastava o arquivo temporário existir, o que trocava um MP3 íntegro
                // por um truncado quando o ffmpeg era interrompido.
                if (ffmpegConcluiu && File.Exists(caminhoTemp) && new FileInfo(caminhoTemp).Length > 0)
                {
                    File.Delete(caminhoAudio);
                    File.Move(caminhoTemp, caminhoAudio);
                }
                else if (!ffmpegConcluiu)
                {
                    Logger.Info("Capa não aplicada (ffmpeg não concluiu); áudio original preservado.");
                }
            }
            catch (Exception ex)
            {
                Logger.Erro("Erro ao adicionar capa", ex);
            }
            finally
            {
                if (File.Exists(caminhoImagem)) File.Delete(caminhoImagem);
                if (File.Exists(caminhoTemp)) File.Delete(caminhoTemp);
            }
        }

        private async void btnBaixar_Click(object sender, EventArgs e)
        {
            if (cmbQualidade.SelectedItem == null)
            {
                Logger.Info("Baixar clicado, mas nenhuma opção de qualidade selecionada.");
                return;
            }
            OpcaoDownload opcao = (OpcaoDownload)cmbQualidade.SelectedItem!;
            Logger.Info($"Baixar clicado: opção='{opcao.Nome}', pastaApp='{Util.PastaApp}'");

            if (!File.Exists(Util.CaminhoNaPastaApp("ffmpeg.exe")))
            {
                Logger.Erro($"ffmpeg.exe não encontrado em '{Util.CaminhoNaPastaApp("ffmpeg.exe")}'");
                MessageBox.Show("FFmpeg ausente. Atualize o programa.");
                return;
            }

            if (_cts != null)
            {
                Logger.Info("Baixar clicado com um download já em andamento; ignorado.");
                return;
            }

            _cts = new CancellationTokenSource(); var token = _cts.Token;
            TravarInterface(true); btnCancelar.Enabled = true;
            bool baixouAlgo = false;

            try
            {
                // 1. MODO UNIVERSAL
                if (opcao.IsGeneric)
                {
                    string extPadrao = opcao.ExtrairAudioMp3 ? "mp3" : "mp4";
                    string nomePadrao = !string.IsNullOrWhiteSpace(tituloVideoAtual) && tituloVideoAtual != "Download Externo" && tituloVideoAtual != "Vídeo (Modo Universal)"
                        ? Util.LimparNome(tituloVideoAtual)
                        : (opcao.ExtrairAudioMp3 ? "audio_download" : "video_download");
                    PrepararDialogoSalvar(nomePadrao, extPadrao);
                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        Preferencias.UltimaPasta = Path.GetDirectoryName(saveFileDialog1.FileName) ?? Preferencias.UltimaPasta;
                        lblStatus.Text = "Baixando (Modo Universal)...";
                        var progUniversal = new Progress<double>(p => { progressBar1.Value = Math.Clamp((int)(p * 100), 0, 100); lblPorcentagem.Text = $"{progressBar1.Value}%"; });
                        var statusUniversal = new Progress<string>(texto => lblStatus.Text = texto);
                        // Usa a URL analisada (e não o texto atual da caixa, que o usuário
                        // pode ter editado depois de clicar em Analisar).
                        string urlDownload = string.IsNullOrWhiteSpace(urlAnalisada) ? Util.ExtrairLink(txtUrl.Text) : urlAnalisada;

                        try
                        {
                            await BaixarUniversalAsync(urlDownload, opcao, progUniversal, statusUniversal, token);
                            lblStatus.Text = "Concluído!";
                            AdicionarHistorico("SUCESSO (Uni)", Path.GetFileName(saveFileDialog1.FileName));
                            MessageBox.Show("Download Concluído!");
                        }
                        catch (OperationCanceledException)
                        {
                            lblStatus.Text = "Cancelado.";
                            progressBar1.Value = 0;
                            AdicionarHistorico("CANCELADO", "Download Universal");
                        }
                        catch (Exception ex)
                        {
                            Logger.Erro("Download Universal falhou", ex);
                            if (ex.Message.Contains("DRM")) MessageBox.Show("Site protegido (DRM).", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            else MessageBox.Show("Erro: " + ex.Message);
                            AdicionarHistorico("ERRO", "Download Universal falhou");
                        }
                    }
                    return;
                }

                // 2. MODO PLAYLIST
                if (modoPlaylist)
                {
                    if (listaVideosPlaylist == null || listaVideosPlaylist.Count == 0)
                    {
                        MessageBox.Show("Erro: Playlist vazia ou não carregada.");
                        return;
                    }

                    folderBrowserDialog1.SelectedPath = Preferencias.UltimaPasta;
                    if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                    {
                        string pasta = folderBrowserDialog1.SelectedPath;
                        Preferencias.UltimaPasta = pasta;
                        int total = listaVideosPlaylist.Count, atual = 0, sucessos = 0, falhas = 0;
                        AdicionarHistorico("INÍCIO", $"Playlist: {tituloVideoAtual}");

                        foreach (var vid in listaVideosPlaylist)
                        {
                            if (token.IsCancellationRequested) break;
                            atual++;
                            lblStatus.Text = $"Baixando {atual}/{total}: {vid.Title}";

                            try
                            {
                                var man = await youtube.Videos.Streams.GetManifestAsync(vid.Id, token);
                                string path = Path.Combine(pasta, Util.LimparNome(vid.Title));
                                bool itemBaixado = false;

                                if (opcao.EhAudio)
                                {
                                    var sInfo = man.GetAudioOnlyStreams().GetWithHighestBitrate();
                                    if (sInfo != null)
                                    {
                                        string caminhoFinal = path + ".mp3";
                                        await youtube.Videos.DownloadAsync(new[] { sInfo }, new ConversionRequestBuilder(caminhoFinal).Build(), null, token);
                                        // Chama o método seguro de capa
                                        await AdicionarCapa(caminhoFinal, vid.Id, token);
                                        itemBaixado = true;
                                    }
                                }
                                else
                                {
                                    var sVid = man.GetVideoOnlyStreams().Where(s => s.VideoQuality.MaxHeight <= opcao.MaxAltura).GetWithHighestVideoQuality();
                                    var sAud = man.GetAudioOnlyStreams().GetWithHighestBitrate();
                                    if (sVid != null && sAud != null)
                                    {
                                        var streamInfos = new IStreamInfo[] { sVid, sAud };
                                        await youtube.Videos.DownloadAsync(streamInfos, new ConversionRequestBuilder(path + ".mp4").Build(), null, token);
                                        itemBaixado = true;
                                    }
                                }
                                progressBar1.Value = (int)((double)atual / total * 100);
                                lblPorcentagem.Text = $"{progressBar1.Value}%";

                                // Sem nenhum stream utilizável nada foi gravado: registrar como OK
                                // fazia a playlist terminar "com sucesso" sem nenhum arquivo.
                                if (itemBaixado) { sucessos++; AdicionarHistorico("OK", vid.Title); }
                                else { falhas++; AdicionarHistorico("FALHA", $"{vid.Title} (sem formato disponível)"); }
                            }
                            catch (OperationCanceledException)
                            {
                                AdicionarHistorico("CANCELADO", vid.Title);
                                break;
                            }
                            catch (Exception ex)
                            {
                                falhas++;
                                Logger.Erro($"Falha ao baixar item da playlist: {vid.Title}", ex);
                                AdicionarHistorico("FALHA", vid.Title);
                                continue;
                            }
                        }

                        baixouAlgo = sucessos > 0;
                        AdicionarHistorico("FIM", $"Playlist: {sucessos} concluído(s), {falhas} com falha");
                        if (sucessos == 0 && falhas > 0)
                            MessageBox.Show($"Nenhum item da playlist pôde ser baixado ({falhas} falha(s)).\nVeja a aba Log para o motivo.",
                                            "Playlist", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                // 3. MODO VÍDEO ÚNICO
                else
                {
                    string extAlvo = opcao.EhAudio ? (opcao.AudioNativo ? (opcao.Extensao ?? "m4a") : "mp3") : "mp4";
                    PrepararDialogoSalvar(Util.LimparNome(tituloVideoAtual), extAlvo);

                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        baixouAlgo = true;
                        Preferencias.UltimaPasta = Path.GetDirectoryName(saveFileDialog1.FileName) ?? Preferencias.UltimaPasta;
                        lblStatus.Text = "Baixando...";
                        var prog = new Progress<double>(p => { progressBar1.Value = Math.Clamp((int)(p * 100), 0, 100); lblPorcentagem.Text = $"{progressBar1.Value}%"; });

                        if (opcao.EhAudio)
                        {
                            if (opcao.Stream != null)
                            {
                                if (opcao.AudioNativo)
                                {
                                    await youtube.Videos.Streams.DownloadAsync(opcao.Stream, saveFileDialog1.FileName, prog, token);
                                }
                                else
                                {
                                    await youtube.Videos.DownloadAsync(new[] { opcao.Stream }, new ConversionRequestBuilder(saveFileDialog1.FileName).Build(), prog, token);

                                    lblStatus.Text = "A adicionar capa...";
                                    if (videoAtual != null)
                                    {
                                        // Chama o método seguro de capa
                                        await AdicionarCapa(saveFileDialog1.FileName, videoAtual.Id, token);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (streamManifestAtual != null && opcao.Stream != null)
                            {
                                var aud = streamManifestAtual.GetAudioOnlyStreams().GetWithHighestBitrate();
                                var vid = (IVideoStreamInfo)opcao.Stream;

                                if (aud != null)
                                {
                                    var streamInfos = new IStreamInfo[] { vid, aud };
                                    await youtube.Videos.DownloadAsync(streamInfos, new ConversionRequestBuilder(saveFileDialog1.FileName).Build(), prog, token);
                                }
                            }
                        }
                        AdicionarHistorico("SUCESSO", tituloVideoAtual);
                    }
                }

                // Só comemora se algo foi realmente baixado: antes, cancelar o diálogo de
                // salvar/escolher pasta ainda exibia "Sucesso!".
                if (baixouAlgo && !token.IsCancellationRequested && !opcao.IsGeneric)
                {
                    lblStatus.Text = "Concluído!";
                    MessageBox.Show("Sucesso!");
                }
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "Cancelado.";
                progressBar1.Value = 0;
                AdicionarHistorico("CANCELADO", "Pelo utilizador");
            }
            catch (Exception ex)
            {
                Logger.Erro("Download falhou", ex);
                MessageBox.Show("Erro: " + ex.Message);
                AdicionarHistorico("ERRO", ex.Message);
            }
            finally
            {
                TravarInterface(false);
                btnCancelar.Enabled = false;
                _cts?.Dispose();
                _cts = null; // sem zerar, o botão Cancelar chamaria Cancel() num objeto já descartado
            }
        }

        // Baixa no modo Universal e, se o YouTube exigir login, oferece a janela de login
        // embutida e repete o download uma vez com os cookies recém-obtidos.
        private async Task BaixarUniversalAsync(string url, OpcaoDownload opcao, IProgress<double> progresso, IProgress<string> status, CancellationToken token)
        {
            try
            {
                await Task.Run(() => YtDlpDownloader.Baixar(url, saveFileDialog1.FileName, opcao.YtDlpFormato, opcao.ExtrairAudioMp3, progresso, status, token));
                return;
            }
            catch (LoginYoutubeNecessarioException ex)
            {
                Logger.Info("Download exigiu login; oferecendo a janela de login do app.");

                var texto = new StringBuilder("O YouTube está exigindo login para baixar este vídeo.\n\n");
                texto.AppendLine("Os navegadores Chrome, Brave e Edge não permitem mais que programas leiam seus cookies, ")
                     .AppendLine("então o programa tem a própria tela de login.")
                     .AppendLine()
                     .Append("Deseja entrar na sua conta do YouTube agora?");

                if (MessageBox.Show(texto.ToString(), "Login necessário", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    string extra = ex.Orientacoes.Count > 0 ? "\n\n" + string.Join("\n", ex.Orientacoes) : "";
                    throw new Exception("Download cancelado: o vídeo exige login." + extra);
                }

                status.Report("Aguardando login...");
                using var janela = new LoginYoutube();
                if (janela.ShowDialog(this) != DialogResult.OK)
                    throw new Exception("Login não concluído. Tente novamente e entre na sua conta do YouTube.");
            }

            // Segunda e última tentativa, agora com o cookies.txt gerado pela janela de login.
            status.Report("Baixando com a sua conta...");
            await Task.Run(() => YtDlpDownloader.Baixar(url, saveFileDialog1.FileName, opcao.YtDlpFormato, opcao.ExtrairAudioMp3, progresso, status, token));
        }

        // Garante que o arquivo saia com a extensão certa mesmo se o usuário apagá-la no diálogo.
        private void PrepararDialogoSalvar(string nomeSemExtensao, string extensao)
        {
            saveFileDialog1.FileName = nomeSemExtensao;
            saveFileDialog1.Filter = $"{extensao.ToUpper()}|*.{extensao}";
            saveFileDialog1.DefaultExt = extensao;
            saveFileDialog1.AddExtension = true;
            saveFileDialog1.InitialDirectory = Preferencias.UltimaPasta;
            // Sem isso o diálogo troca o diretório de trabalho do processo pela pasta escolhida.
            saveFileDialog1.RestoreDirectory = true;
        }

        private void btnColar_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsText()) { txtUrl.Text = Clipboard.GetText(); btnBuscar.PerformClick(); }
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            try { _cts?.Cancel(); } catch (ObjectDisposedException) { return; }
            btnCancelar.Enabled = false;
            lblStatus.Text = "Cancelando...";
        }

        private void btnDoar_Click(object sender, EventArgs e) { new TelaDoacao(LinkLivePix).ShowDialog(); }

        private void btnSobre_Click(object sender, EventArgs e)
        {
            Form j = new Form(); j.Text = "Sobre"; j.Size = new Size(350, 250); j.StartPosition = FormStartPosition.CenterParent;
            j.FormBorderStyle = FormBorderStyle.FixedDialog; j.MaximizeBox = false; j.MinimizeBox = false;
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Label l1 = new Label { Text = "Youtube Downloader Pro", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, Location = new Point(60, 20) };
            Label l2 = new Label { Text = $"Versão: {v}\n\nC# .NET + yt-dlp", TextAlign = ContentAlignment.MiddleCenter, AutoSize = true, Location = new Point(90, 60) };
            LinkLabel ll = new LinkLabel { Text = "github.com/wandersonstt", AutoSize = true, Location = new Point(100, 120) };
            ll.LinkClicked += (s, args) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://github.com/wandersonstt", UseShellExecute = true });
            j.Controls.Add(l1); j.Controls.Add(l2); j.Controls.Add(ll); j.ShowDialog();
        }

        private void TravarInterface(bool travado)
        {
            btnColar.Enabled = !travado; txtUrl.Enabled = !travado; btnAtualizar.Enabled = !travado;
            btnBuscar.Enabled = !travado; cmbQualidade.Enabled = !travado;
            // btnBaixar também precisa travar: sem isso um segundo clique iniciava outro
            // download em paralelo, sobrescrevendo o CancellationTokenSource e quebrando o Cancelar.
            btnBaixar.Enabled = !travado;
        }

        // Encerra qualquer download em andamento ao fechar a janela: sem isso o yt-dlp/ffmpeg
        // continuavam rodando em segundo plano, gravando no arquivo do usuário.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { _cts?.Cancel(); } catch { }
            base.OnFormClosing(e);
        }

        private class OpcaoDownload
        {
            public string? Nome { get; set; }
            public IStreamInfo? Stream { get; set; }
            public bool EhAudio { get; set; }
            public int MaxAltura { get; set; } = 4320;
            public bool IsGeneric { get; set; } = false;
            public string? YtDlpFormato { get; set; }
            public string? Extensao { get; set; }
            public bool ExtrairAudioMp3 { get; set; }
            public bool AudioNativo { get; set; }
            public override string ToString() => Nome ?? "?";
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}