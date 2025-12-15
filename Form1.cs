using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
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

        // URLs
        private const string UrlXmlUpdate = "https://raw.githubusercontent.com/wandersonstt/Youtube-Downloader-Pro/refs/heads/main/update.xml";
        private const string UrlFFmpegZip = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        private const string UrlYtDlpExe = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        private const string LinkLivePix = "https://livepix.gg/alho97";

        // Variáveis de controle
        private StreamManifest? streamManifestAtual;
        private bool modoPlaylist = false;
        private IReadOnlyList<YoutubeExplode.Playlists.PlaylistVideo>? listaVideosPlaylist;
        private CancellationTokenSource? _cts;
        private string tituloVideoAtual = "";

        // CORREÇÃO CS8618: Tornamos anulável (?) para evitar aviso do construtor
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
            // Segurança: Se a caixa de texto não foi criada, sai do método
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
            catch (Exception ex) { MessageBox.Show("Erro: " + ex.Message); }
            finally { loading.Close(); TravarInterface(false); }
        }

        private async Task VerificarDependencias(TelaCarregamento loading)
        {
            loading.AtualizarMensagem("Verificando FFmpeg...");
            await BaixarOuAtualizarArquivo(UrlFFmpegZip, Path.Combine(Environment.CurrentDirectory, "ffmpeg.exe"), true, "ffmpeg_version.txt");

            loading.AtualizarMensagem("Verificando yt-dlp...");
            await BaixarOuAtualizarArquivo(UrlYtDlpExe, Path.Combine(Environment.CurrentDirectory, "yt-dlp.exe"), false, "ytdlp_version.txt");
        }

        private async Task BaixarOuAtualizarArquivo(string url, string caminhoDestino, bool ehZip, string arquivoVersao)
        {
            string caminhoVersao = Path.Combine(Environment.CurrentDirectory, arquivoVersao);
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var request = new HttpRequestMessage(HttpMethod.Head, url);
                    var response = await client.SendAsync(request);

                    if (response.IsSuccessStatusCode && response.Content.Headers.LastModified.HasValue)
                    {
                        DateTime dataServidor = response.Content.Headers.LastModified.Value.UtcDateTime;
                        bool precisaBaixar = !File.Exists(caminhoDestino);

                        if (File.Exists(caminhoDestino) && File.Exists(caminhoVersao))
                        {
                            if (DateTime.TryParse(File.ReadAllText(caminhoVersao), out DateTime dataLocal))
                            {
                                if (dataServidor > dataLocal) precisaBaixar = true;
                            }
                        }

                        if (precisaBaixar)
                        {
                            var dados = await client.GetByteArrayAsync(url);
                            await Task.Run(() =>
                            {
                                if (ehZip)
                                {
                                    string pastaTemp = Path.Combine(Path.GetTempPath(), "yt_update_" + Guid.NewGuid());
                                    string zipTemp = Path.Combine(pastaTemp, "update.zip");
                                    if (Directory.Exists(pastaTemp)) Directory.Delete(pastaTemp, true);
                                    Directory.CreateDirectory(pastaTemp);
                                    File.WriteAllBytes(zipTemp, dados);
                                    ZipFile.ExtractToDirectory(zipTemp, pastaTemp);
                                    string nomeExe = Path.GetFileName(caminhoDestino);
                                    string[] exes = Directory.GetFiles(pastaTemp, nomeExe, SearchOption.AllDirectories);
                                    if (exes.Length > 0)
                                    {
                                        if (File.Exists(caminhoDestino)) File.Delete(caminhoDestino);
                                        File.Move(exes[0], caminhoDestino);
                                    }
                                    if (Directory.Exists(pastaTemp)) Directory.Delete(pastaTemp, true);
                                }
                                else
                                {
                                    if (File.Exists(caminhoDestino)) File.Delete(caminhoDestino);
                                    File.WriteAllBytes(caminhoDestino, dados);
                                }
                                File.WriteAllText(caminhoVersao, dataServidor.ToString());
                            });
                        }
                    }
                }
            }
            catch (Exception)
            {
                if (!File.Exists(caminhoDestino))
                    this.Invoke((MethodInvoker)delegate { MessageBox.Show($"Componente {Path.GetFileName(caminhoDestino)} ausente e sem internet."); });
            }
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

            var loading = new TelaCarregamento("Analisando link...");
            loading.Show(this); Application.DoEvents();

            try
            {
                TravarInterface(true); cmbQualidade.Items.Clear(); btnBaixar.Enabled = false; modoPlaylist = false;

                bool isYoutube = url.Contains("youtube.com") || url.Contains("youtu.be");

                if (!isYoutube)
                {
                    loading.Close();
                    tituloVideoAtual = "Download Externo";
                    lblStatus.Text = "Link externo detectado (Universal)";
                    picThumbnail.Image = null;
                    cmbQualidade.Items.Add(new OpcaoDownload { Nome = "Melhor Qualidade (Universal)", IsGeneric = true });
                    cmbQualidade.SelectedIndex = 0;
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
                if (audioStream != null) cmbQualidade.Items.Add(new OpcaoDownload { Nome = "Áudio MP3 (Alta Qualidade)", Stream = audioStream, EhAudio = true });
                var videoStreams = streamManifestAtual.GetVideoOnlyStreams().OrderByDescending(s => s.VideoQuality.MaxHeight);
                foreach (var stream in videoStreams)
                {
                    string tam = stream.Size.MegaBytes.ToString("F1");
                    cmbQualidade.Items.Add(new OpcaoDownload { Nome = $"Vídeo {stream.VideoQuality.Label} - {stream.Container} ({tam} MB)", Stream = stream, EhAudio = false });
                }
                if (cmbQualidade.Items.Count > 0) { cmbQualidade.SelectedIndex = 0; btnBaixar.Enabled = true; }
            }
            catch (Exception ex) { MessageBox.Show("Erro ao analisar: " + ex.Message); }
            finally { TravarInterface(false); loading.Close(); }
        }

        private async Task AdicionarCapa(string caminhoAudio, string videoId)
        {
            string caminhoImagem = Path.ChangeExtension(caminhoAudio, ".jpg");
            string caminhoTemp = Path.ChangeExtension(caminhoAudio, ".temp.mp3");

            try
            {
                var thumbUrl = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";
                using (var client = new HttpClient())
                {
                    var bytes = await client.GetByteArrayAsync(thumbUrl);
                    await File.WriteAllBytesAsync(caminhoImagem, bytes);
                }

                string ffmpegPath = Path.Combine(Environment.CurrentDirectory, "ffmpeg.exe");

                string args = $"-i \"{caminhoAudio}\" -i \"{caminhoImagem}\" -map 0 -map 1 -c copy -id3v2_version 3 -metadata:s:v title=\"Album cover\" -metadata:s:v comment=\"Cover (front)\" -y \"{caminhoTemp}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(startInfo))
                {
                    await process.WaitForExitAsync();
                }

                if (File.Exists(caminhoTemp))
                {
                    File.Delete(caminhoAudio);
                    File.Move(caminhoTemp, caminhoAudio);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Erro ao adicionar capa: " + ex.Message);
            }
            finally
            {
                if (File.Exists(caminhoImagem)) File.Delete(caminhoImagem);
            }
        }

        private async void btnBaixar_Click(object sender, EventArgs e)
        {
            // CORREÇÃO CS8602: Verificamos se SelectedItem é nulo antes de prosseguir
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
                    saveFileDialog1.FileName = "video_download";
                    saveFileDialog1.Filter = "Vídeo MP4|*.mp4";
                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        lblStatus.Text = "Baixando (Modo Universal)...";
                        try
                        {
                            await Task.Run(() => BaixarViaYtDlp(txtUrl.Text, saveFileDialog1.FileName));
                            AdicionarHistorico("SUCESSO (Uni)", Path.GetFileName(saveFileDialog1.FileName));
                            MessageBox.Show("Download Concluído!");
                        }
                        catch (Exception ex)
                        {
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
                    // CORREÇÃO CS8602: Validamos se listaVideosPlaylist existe
                    if (listaVideosPlaylist == null || listaVideosPlaylist.Count == 0)
                    {
                        MessageBox.Show("Erro: Playlist vazia ou não carregada.");
                        return;
                    }

                    if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                    {
                        string pasta = folderBrowserDialog1.SelectedPath;
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
                                        await AdicionarCapa(caminhoFinal, vid.Id);
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
                    saveFileDialog1.Filter = opcao.EhAudio ? "MP3|*.mp3" : "MP4|*.mp4";

                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        lblStatus.Text = "Baixando...";
                        var prog = new Progress<double>(p => { progressBar1.Value = Math.Min((int)(p * 100), 100); lblPorcentagem.Text = $"{progressBar1.Value}%"; });

                        if (opcao.EhAudio)
                        {
                            if (opcao.Stream != null)
                            {
                                await youtube.Videos.DownloadAsync(new[] { opcao.Stream }, new ConversionRequestBuilder(saveFileDialog1.FileName).Build(), prog, token);

                                lblStatus.Text = "A adicionar capa...";
                                // CORREÇÃO CS8602: Garantimos que videoAtual não é nulo
                                if (videoAtual != null)
                                {
                                    await AdicionarCapa(saveFileDialog1.FileName, videoAtual.Id);
                                }
                            }
                        }
                        else
                        {
                            // CORREÇÃO CS8602: Verificação do manifesto e stream
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

        private void BaixarViaYtDlp(string url, string destino)
        {
            string ytDlpPath = Path.Combine(Environment.CurrentDirectory, "yt-dlp.exe");
            if (!File.Exists(ytDlpPath)) throw new Exception("yt-dlp.exe não encontrado.");

            string args = $"-o \"{destino}\" --remux-video mp4 \"{url}\"";
            var startInfo = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new Exception(process.StandardError.ReadToEnd());
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
            public override string ToString() => Nome ?? "?";
        }
    }

    public class TelaDoacao : Form
    {
        public TelaDoacao(string linkLivePix)
        {
            this.Text = "Apoie o Projeto ❤"; this.Size = new Size(350, 450); this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog; this.MaximizeBox = false; this.MinimizeBox = false; this.BackColor = Color.FromArgb(32, 32, 32);
            Label lblTitulo = new Label { Text = "Gostou do programa?", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(70, 20) };
            Label lblDesc = new Label { Text = "Escaneie o QR Code abaixo para doar\nqualquer valor via LivePix e apoiar o dev!", Font = new Font("Segoe UI", 9), ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleCenter, AutoSize = true, Location = new Point(50, 60) };
            PictureBox picQR = new PictureBox { Size = new Size(200, 200), Location = new Point(65, 110), SizeMode = PictureBoxSizeMode.StretchImage, BackColor = Color.White, Padding = new Padding(5) };
            try { picQR.Load($"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={linkLivePix}"); } catch { picQR.BackColor = Color.Red; }
            Button btnAbrir = new Button { Text = "Abrir Link no Navegador", Size = new Size(200, 35), Location = new Point(65, 330), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnAbrir.FlatAppearance.BorderSize = 0;
            btnAbrir.Click += (s, e) => { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = linkLivePix, UseShellExecute = true }); };
            this.Controls.Add(lblTitulo); this.Controls.Add(lblDesc); this.Controls.Add(picQR); this.Controls.Add(btnAbrir);
        }
    }

    public class TelaCarregamento : Form
    {
        private Label lblMensagem;
        public TelaCarregamento(string texto)
        {
            this.TopMost = true; this.Size = new Size(300, 100); this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen; this.BackColor = Color.White;
            this.Paint += (s, e) => e.Graphics.DrawRectangle(Pens.Black, 0, 0, Width - 1, Height - 1);
            lblMensagem = new Label { Text = texto, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top, Height = 40, Font = new Font("Segoe UI", 10) };
            var pb = new ProgressBar { Style = ProgressBarStyle.Marquee, Dock = DockStyle.Bottom, Height = 20 };
            this.Controls.Add(lblMensagem); this.Controls.Add(pb);
            lblMensagem.Top = (this.ClientSize.Height - pb.Height - lblMensagem.Height) / 2;
        }
        public void AtualizarMensagem(string t) { if (lblMensagem != null) { lblMensagem.Text = t; Application.DoEvents(); } }
    }
}