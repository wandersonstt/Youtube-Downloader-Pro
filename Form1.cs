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
using System.Text.RegularExpressions;
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

        public Form1()
        {
            InitializeComponent();
            ConfigurarInterfaceHistorico();
        }

        private void ConfigurarInterfaceHistorico()
        {
            this.Height = 500;

            Label lblHist = new Label();
            lblHist.Text = "Histórico de Downloads:";
            lblHist.ForeColor = Color.DarkGray;
            lblHist.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            lblHist.AutoSize = true;
            lblHist.Location = new Point(12, 310);
            this.Controls.Add(lblHist);

            txtHistorico = new RichTextBox();
            txtHistorico.Location = new Point(12, 330);
            txtHistorico.Size = new Size(this.ClientSize.Width - 24, 120);
            txtHistorico.BackColor = Color.FromArgb(40, 40, 40);
            txtHistorico.ForeColor = Color.LimeGreen;
            txtHistorico.Font = new Font("Consolas", 9);
            txtHistorico.ReadOnly = true;
            txtHistorico.BorderStyle = BorderStyle.None;
            txtHistorico.ScrollBars = RichTextBoxScrollBars.Vertical;
            this.Controls.Add(txtHistorico);
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
                AutoUpdater.DownloadPath = Environment.CurrentDirectory;
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
            cmbQualidade.Items.Add(new OpcaoDownload { Nome = "Melhor Qualidade (Automático)", IsGeneric = true, YtDlpFormato = "bestvideo+bestaudio/best" });
            foreach (var altura in new[] { 2160, 1440, 1080, 720, 480, 360 })
                cmbQualidade.Items.Add(new OpcaoDownload { Nome = $"Vídeo {altura}p (MP4)", IsGeneric = true, YtDlpFormato = $"bestvideo[height<={altura}]+bestaudio/best[height<={altura}]" });
            cmbQualidade.Items.Add(new OpcaoDownload { Nome = "Áudio MP3", IsGeneric = true, ExtrairAudioMp3 = true });
            cmbQualidade.SelectedIndex = 0;
        }

        private string ExtrairLink(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            var match = Regex.Match(texto, @"https?://[^\s]+");
            return match.Success ? match.Value : texto.Trim();
        }

        private async void btnBuscar_Click(object sender, EventArgs e)
        {
            var urlSuja = txtUrl.Text;
            var url = ExtrairLink(urlSuja);
            if (url != urlSuja) txtUrl.Text = url;

            if (string.IsNullOrWhiteSpace(url)) return;

            Logger.Info($"Analisar clicado: {url}");
            var loading = new TelaCarregamento("Analisando link...");
            loading.Show(this); Application.DoEvents();

            try
            {
                TravarInterface(true); cmbQualidade.Items.Clear(); btnBaixar.Enabled = false; modoPlaylist = false;

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
        private async Task AdicionarCapa(string caminhoAudio, string videoId, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            string caminhoImagem = Path.ChangeExtension(caminhoAudio, ".jpg");
            string caminhoTemp = Path.ChangeExtension(caminhoAudio, ".temp.mp3");

            try
            {
                // 1. Tenta baixar a capa em Alta Resolução (maxresdefault)
                var thumbUrl = $"https://img.youtube.com/vi/{videoId}/maxresdefault.jpg";

                {
                    var response = await httpClient.GetAsync(thumbUrl);

                    // Se falhar (404), tenta a qualidade padrão (hqdefault)
                    if (!response.IsSuccessStatusCode)
                    {
                        thumbUrl = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";
                        response = await httpClient.GetAsync(thumbUrl);
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            try
                            {
                                using (var img = Image.FromStream(stream))
                                {
                                    img.Save(caminhoImagem, ImageFormat.Jpeg);
                                }
                            }
                            catch
                            {
                                return; // Se a imagem for inválida, desiste da capa mas mantém o áudio
                            }
                        }
                    }
                }

                if (!File.Exists(caminhoImagem)) return;

                // 2. Chama o FFmpeg para juntar a capa com as FLAGS CERTAS
                string ffmpegPath = Path.Combine(Environment.CurrentDirectory, "ffmpeg.exe");

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

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        process.StandardInput.Close(); // evita travar esperando input que nunca virá
                        // Timeout de segurança de 30s OU cancelamento pedido pelo usuário
                        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                        {
                            cts.CancelAfter(TimeSpan.FromSeconds(30));
                            try { await process.WaitForExitAsync(cts.Token); }
                            catch (OperationCanceledException) { try { process.Kill(true); } catch { } }
                        }
                    }
                }

                // 3. Substitui o arquivo apenas se o temporário foi criado com sucesso
                if (File.Exists(caminhoTemp) && new FileInfo(caminhoTemp).Length > 0)
                {
                    File.Delete(caminhoAudio);
                    File.Move(caminhoTemp, caminhoAudio);
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
            if (cmbQualidade.SelectedItem == null) return;
            OpcaoDownload opcao = (OpcaoDownload)cmbQualidade.SelectedItem!;

            if (!File.Exists("ffmpeg.exe")) { MessageBox.Show("FFmpeg ausente. Atualize o programa."); return; }

            _cts = new CancellationTokenSource(); var token = _cts.Token;
            TravarInterface(true); btnCancelar.Enabled = true;

            try
            {
                // 1. MODO UNIVERSAL
                if (opcao.IsGeneric)
                {
                    string extPadrao = opcao.ExtrairAudioMp3 ? "mp3" : "mp4";
                    string nomePadrao = !string.IsNullOrWhiteSpace(tituloVideoAtual) && tituloVideoAtual != "Download Externo" && tituloVideoAtual != "Vídeo (Modo Universal)"
                        ? LimparNome(tituloVideoAtual)
                        : (opcao.ExtrairAudioMp3 ? "audio_download" : "video_download");
                    saveFileDialog1.FileName = nomePadrao;
                    saveFileDialog1.Filter = $"{extPadrao.ToUpper()}|*.{extPadrao}";
                    saveFileDialog1.InitialDirectory = Preferencias.UltimaPasta;
                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        Preferencias.UltimaPasta = Path.GetDirectoryName(saveFileDialog1.FileName) ?? Preferencias.UltimaPasta;
                        lblStatus.Text = "Baixando (Modo Universal)...";
                        var progUniversal = new Progress<double>(p => { progressBar1.Value = Math.Clamp((int)(p * 100), 0, 100); lblPorcentagem.Text = $"{progressBar1.Value}%"; });
                        var statusUniversal = new Progress<string>(texto => lblStatus.Text = texto);
                        try
                        {
                            await Task.Run(() => YtDlpDownloader.Baixar(txtUrl.Text, saveFileDialog1.FileName, opcao.YtDlpFormato, opcao.ExtrairAudioMp3, progUniversal, statusUniversal, token));
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
                        int total = listaVideosPlaylist.Count, atual = 0;
                        AdicionarHistorico("INÍCIO", $"Playlist: {tituloVideoAtual}");

                        foreach (var vid in listaVideosPlaylist)
                        {
                            if (token.IsCancellationRequested) break;
                            atual++;
                            lblStatus.Text = $"Baixando {atual}/{total}: {vid.Title}";

                            try
                            {
                                var man = await youtube.Videos.Streams.GetManifestAsync(vid.Id, token);
                                string path = Path.Combine(pasta, LimparNome(vid.Title));

                                if (opcao.EhAudio)
                                {
                                    var sInfo = man.GetAudioOnlyStreams().GetWithHighestBitrate();
                                    if (sInfo != null)
                                    {
                                        string caminhoFinal = path + ".mp3";
                                        await youtube.Videos.DownloadAsync(new[] { sInfo }, new ConversionRequestBuilder(caminhoFinal).Build(), null, token);
                                        // Chama o método seguro de capa
                                        await AdicionarCapa(caminhoFinal, vid.Id, token);
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
                                    }
                                }
                                progressBar1.Value = (int)((double)atual / total * 100);
                                lblPorcentagem.Text = $"{progressBar1.Value}%";
                                AdicionarHistorico("OK", vid.Title);
                            }
                            catch (OperationCanceledException)
                            {
                                AdicionarHistorico("CANCELADO", vid.Title);
                                break;
                            }
                            catch { AdicionarHistorico("FALHA", vid.Title); continue; }
                        }
                        AdicionarHistorico("FIM", "Playlist concluída");
                    }
                }
                // 3. MODO VÍDEO ÚNICO
                else
                {
                    string nomeArquivo = LimparNome(tituloVideoAtual);
                    saveFileDialog1.FileName = nomeArquivo;
                    string extAudio = opcao.AudioNativo ? (opcao.Extensao ?? "m4a") : "mp3";
                    saveFileDialog1.Filter = opcao.EhAudio ? $"{extAudio.ToUpper()}|*.{extAudio}" : "MP4|*.mp4";
                    saveFileDialog1.InitialDirectory = Preferencias.UltimaPasta;

                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
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

                if (!token.IsCancellationRequested && !opcao.IsGeneric)
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
                btnBaixar.Enabled = true;
                btnCancelar.Enabled = false;
                _cts?.Dispose();
            }
        }

        private string LimparNome(string nome) => string.Join("_", nome.Split(Path.GetInvalidFileNameChars()));

        private void btnColar_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsText()) { txtUrl.Text = Clipboard.GetText(); btnBuscar.PerformClick(); }
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            if (_cts != null) { _cts.Cancel(); btnCancelar.Enabled = false; lblStatus.Text = "Cancelando..."; }
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