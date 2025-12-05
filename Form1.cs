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
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Converter; // Essencial
using YoutubeExplode.Common;
using AutoUpdaterDotNET;

namespace YoutubeDownloaderCS
{
    public partial class Form1 : Form
    {
        private readonly YoutubeClient youtube = new YoutubeClient();
        private const string UrlXmlUpdate = "https://raw.githubusercontent.com/wandersonstt/Youtube-Downloader-Pro/refs/heads/main/update.xml";
        private const string UrlFFmpegZip = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        private const string LinkLivePix = "https://livepix.gg/alho97";

        private StreamManifest? streamManifestAtual;
        private bool modoPlaylist = false;
        private IReadOnlyList<YoutubeExplode.Playlists.PlaylistVideo>? listaVideosPlaylist;
        private CancellationTokenSource? _cts;
        private string tituloVideoAtual = "";

        public Form1()
        {
            InitializeComponent();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var loading = new TelaCarregamento("Iniciando sistemas...");
            loading.Show();
            Application.DoEvents();

            try
            {
                AutoUpdater.RunUpdateAsAdmin = false;
                AutoUpdater.DownloadPath = Environment.CurrentDirectory;
                AutoUpdater.AppTitle = "Youtube Downloader Pro";
                AutoUpdater.UpdateFormSize = new Size(800, 600);
                AutoUpdater.Start(UrlXmlUpdate);

                loading.AtualizarMensagem("Verificando componentes de vídeo...");
                await VerificarAtualizacaoFFmpeg();
            }
            finally { loading.Close(); }
        }

        private async Task VerificarAtualizacaoFFmpeg()
        {
            string caminhoFFmpeg = Path.Combine(Environment.CurrentDirectory, "ffmpeg.exe");
            string arquivoVersao = Path.Combine(Environment.CurrentDirectory, "ffmpeg_version.txt");
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var request = new HttpRequestMessage(HttpMethod.Head, UrlFFmpegZip);
                    var response = await client.SendAsync(request);
                    if (response.IsSuccessStatusCode && response.Content.Headers.LastModified.HasValue)
                    {
                        DateTime dataServidor = response.Content.Headers.LastModified.Value.UtcDateTime;
                        bool precisaBaixar = !File.Exists(caminhoFFmpeg);
                        if (File.Exists(caminhoFFmpeg) && File.Exists(arquivoVersao))
                        {
                            if (DateTime.TryParse(File.ReadAllText(arquivoVersao), out DateTime dataLocal)) { if (dataServidor > dataLocal) precisaBaixar = true; }
                        }
                        if (precisaBaixar)
                        {
                            string pastaTemp = Path.Combine(Path.GetTempPath(), "ffmpeg_update");
                            string zipTemp = Path.Combine(pastaTemp, "update.zip");
                            if (Directory.Exists(pastaTemp)) Directory.Delete(pastaTemp, true);
                            Directory.CreateDirectory(pastaTemp);
                            var dados = await client.GetByteArrayAsync(UrlFFmpegZip);
                            File.WriteAllBytes(zipTemp, dados);
                            ZipFile.ExtractToDirectory(zipTemp, pastaTemp);
                            string[] exes = Directory.GetFiles(pastaTemp, "ffmpeg.exe", SearchOption.AllDirectories);
                            if (exes.Length > 0)
                            {
                                if (File.Exists(caminhoFFmpeg)) File.Delete(caminhoFFmpeg);
                                File.Move(exes[0], caminhoFFmpeg);
                                File.WriteAllText(arquivoVersao, dataServidor.ToString());
                            }
                            if (Directory.Exists(pastaTemp)) Directory.Delete(pastaTemp, true);
                            lblStatus.Text = "Componentes atualizados.";
                        }
                    }
                }
            }
            catch { }
        }

        private void btnDoar_Click(object sender, EventArgs e)
        {
            var telaPix = new TelaDoacao(LinkLivePix);
            telaPix.ShowDialog();
        }

        private async void btnBuscar_Click(object sender, EventArgs e)
        {
            var url = txtUrl.Text;
            if (string.IsNullOrWhiteSpace(url)) return;
            var loading = new TelaCarregamento("Analisando link...");
            loading.Show(); Application.DoEvents();
            try
            {
                TravarInterface(true); cmbQualidade.Items.Clear(); btnBaixar.Enabled = false; modoPlaylist = false;
                if (url.Contains("list="))
                {
                    loading.Close();
                    if (MessageBox.Show("Link de Playlist detectado.\nDeseja baixar todos os vídeos?", "Playlist", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        loading = new TelaCarregamento("Carregando lista..."); loading.Show(); Application.DoEvents();
                        modoPlaylist = true;
                        var playlist = await youtube.Playlists.GetAsync(url);
                        listaVideosPlaylist = await youtube.Playlists.GetVideosAsync(playlist.Id);
                        lblStatus.Text = $"Playlist: {playlist.Title} ({listaVideosPlaylist.Count} vídeos)";
                        tituloVideoAtual = playlist.Title;
                        if (listaVideosPlaylist.Count > 0) picThumbnail.LoadAsync($"https://img.youtube.com/vi/{listaVideosPlaylist[0].Id}/hqdefault.jpg");
                        cmbQualidade.Items.Add(new OpcaoDownload { Nome = "Playlist: Apenas Áudio (MP3)", EhAudio = true });
                        cmbQualidade.Items.Add(new OpcaoDownload { Nome = "Playlist: Máxima (4K/8K)", MaxAltura = 4320 });
                        cmbQualidade.Items.Add(new OpcaoDownload { Nome = "Playlist: 1080p (Full HD)", MaxAltura = 1080 });
                        cmbQualidade.Items.Add(new OpcaoDownload { Nome = "Playlist: 720p (HD)", MaxAltura = 720 });
                        cmbQualidade.SelectedIndex = 1; btnBaixar.Enabled = true; return;
                    }
                    else { loading = new TelaCarregamento("Analisando vídeo único..."); loading.Show(); }
                }
                var video = await youtube.Videos.GetAsync(url);
                picThumbnail.LoadAsync($"https://img.youtube.com/vi/{video.Id}/hqdefault.jpg");
                tituloVideoAtual = video.Title; lblStatus.Text = $"Vídeo: {video.Title}";
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
            catch (Exception ex) { MessageBox.Show("Erro: " + ex.Message); }
            finally { TravarInterface(false); loading.Close(); }
        }

        private void btnCancelar_Click(object sender, EventArgs e) { if (_cts != null) { _cts.Cancel(); btnCancelar.Enabled = false; lblStatus.Text = "Cancelando..."; } }

        // --- CORREÇÃO PRINCIPAL AQUI (btnBaixar) ---
        private async void btnBaixar_Click(object sender, EventArgs e)
        {
            if (cmbQualidade.SelectedItem == null) return;
            OpcaoDownload opcao = (OpcaoDownload)cmbQualidade.SelectedItem!;
            if (!opcao.EhAudio && !File.Exists("ffmpeg.exe")) { MessageBox.Show("FFmpeg ausente. Reinicie."); return; }

            _cts = new CancellationTokenSource(); var token = _cts.Token;
            TravarInterface(true); btnCancelar.Enabled = true;

            try
            {
                if (modoPlaylist)
                {
                    if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                    {
                        string pasta = folderBrowserDialog1.SelectedPath;
                        int total = listaVideosPlaylist!.Count, atual = 0;
                        foreach (var vid in listaVideosPlaylist)
                        {
                            if (token.IsCancellationRequested) break;
                            atual++; lblStatus.Text = $"Baixando {atual}/{total}: {vid.Title}";
                            try
                            {
                                var man = await youtube.Videos.Streams.GetManifestAsync(vid.Id, token);
                                string path = Path.Combine(pasta, LimparNome(vid.Title));

                                if (opcao.EhAudio)
                                {
                                    var sInfo = man.GetAudioOnlyStreams().GetWithHighestBitrate();
                                    if (sInfo != null) await youtube.Videos.Streams.DownloadAsync(sInfo, path + ".mp3", null, token);
                                }
                                else
                                {
                                    var sVid = man.GetVideoOnlyStreams().Where(s => s.VideoQuality.MaxHeight <= opcao.MaxAltura).GetWithHighestVideoQuality();
                                    var sAud = man.GetAudioOnlyStreams().GetWithHighestBitrate();

                                    // CORREÇÃO: DownloadAsync na versão nova precisa de um ARRAY de streams
                                    if (sVid != null && sAud != null)
                                    {
                                        var streamInfos = new IStreamInfo[] { sVid, sAud };
                                        await youtube.Videos.DownloadAsync(streamInfos, new ConversionRequestBuilder(path + ".mp4").Build(), null, token);
                                    }
                                }
                                progressBar1.Value = (int)((double)atual / total * 100); lblPorcentagem.Text = $"{progressBar1.Value}%";
                            }
                            catch (OperationCanceledException) { throw; }
                            catch { continue; }
                        }
                    }
                }
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
                            await youtube.Videos.Streams.DownloadAsync(opcao.Stream!, saveFileDialog1.FileName, prog, token);
                        }
                        else
                        {
                            var aud = streamManifestAtual!.GetAudioOnlyStreams().GetWithHighestBitrate();
                            var vid = (IVideoStreamInfo)opcao.Stream!;

                            // CORREÇÃO: DownloadAsync na versão nova
                            var streamInfos = new IStreamInfo[] { vid, aud };
                            await youtube.Videos.DownloadAsync(streamInfos, new ConversionRequestBuilder(saveFileDialog1.FileName).Build(), prog, token);
                        }
                    }
                }
                if (!token.IsCancellationRequested) { lblStatus.Text = "Concluído!"; MessageBox.Show("Sucesso!"); }
            }
            catch (OperationCanceledException) { lblStatus.Text = "Cancelado."; progressBar1.Value = 0; lblPorcentagem.Text = "0%"; }
            catch (Exception ex) { MessageBox.Show("Erro: " + ex.Message); }
            finally { TravarInterface(false); btnBaixar.Enabled = true; btnCancelar.Enabled = false; _cts?.Dispose(); }
        }

        private string LimparNome(string nome) => string.Join("_", nome.Split(Path.GetInvalidFileNameChars()));
        private class OpcaoDownload { public string? Nome { get; set; } public IStreamInfo? Stream { get; set; } public bool EhAudio { get; set; } public int MaxAltura { get; set; } = 4320; public override string ToString() => Nome ?? "?"; }
        private void btnColar_Click(object sender, EventArgs e) { if (Clipboard.ContainsText()) { txtUrl.Text = Clipboard.GetText(); btnBuscar.PerformClick(); } }
        private void TravarInterface(bool travado) { btnColar.Enabled = !travado; txtUrl.Enabled = !travado; btnAtualizar.Enabled = !travado; btnBuscar.Enabled = !travado; cmbQualidade.Enabled = !travado; }
        private void btnAtualizar_Click(object sender, EventArgs e) { AutoUpdater.ReportErrors = true; AutoUpdater.ShowRemindLaterButton = true; AutoUpdater.Start(UrlXmlUpdate); }

        private void btnSobre_Click(object sender, EventArgs e)
        {
            Form j = new Form(); j.Text = "Sobre"; j.Size = new Size(350, 250); j.StartPosition = FormStartPosition.CenterParent;
            j.FormBorderStyle = FormBorderStyle.FixedDialog; j.MaximizeBox = false; j.MinimizeBox = false;
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Label l1 = new Label { Text = "Youtube Downloader Pro", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, Location = new Point(60, 20) };
            Label l2 = new Label { Text = $"Versão: {v}\n\nC# .NET", TextAlign = ContentAlignment.MiddleCenter, AutoSize = true, Location = new Point(90, 60) };
            LinkLabel ll = new LinkLabel { Text = "github.com/wandersonstt", AutoSize = true, Location = new Point(100, 120) };
            ll.LinkClicked += (s, args) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://github.com/wandersonstt", UseShellExecute = true });
            j.Controls.Add(l1); j.Controls.Add(l2); j.Controls.Add(ll); j.ShowDialog();
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
            this.Size = new Size(300, 100); this.FormBorderStyle = FormBorderStyle.None; this.StartPosition = FormStartPosition.CenterScreen; this.BackColor = Color.White;
            this.Paint += (s, e) => e.Graphics.DrawRectangle(Pens.Black, 0, 0, Width - 1, Height - 1);
            lblMensagem = new Label { Text = texto, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top, Height = 40, Font = new Font("Segoe UI", 10) };
            var pb = new ProgressBar { Style = ProgressBarStyle.Marquee, Dock = DockStyle.Bottom, Height = 20 };
            this.Controls.Add(lblMensagem); this.Controls.Add(pb);
            lblMensagem.Top = (this.ClientSize.Height - pb.Height - lblMensagem.Height) / 2;
        }
        public void AtualizarMensagem(string t) { if (lblMensagem != null) { lblMensagem.Text = t; Application.DoEvents(); } }
    }
}