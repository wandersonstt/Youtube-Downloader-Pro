using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace YoutubeDownloaderCS
{
    // Verifica e baixa ffmpeg.exe / yt-dlp.exe, mantendo-os atualizados.
    internal static class DependencyUpdater
    {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        private const string UrlFFmpegZip = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        private const string UrlYtDlpApiLatest = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
        private const string UrlYtDlpFallback = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

        public static async Task<List<string>> VerificarTudoAsync(Action<string> reportarStatus)
        {
            var erros = new List<string>();

            reportarStatus("Verificando FFmpeg...");
            var erroFFmpeg = await BaixarOuAtualizarArquivo(UrlFFmpegZip, Path.Combine(Environment.CurrentDirectory, "ffmpeg.exe"), true, "ffmpeg_version.txt");
            if (erroFFmpeg != null) erros.Add(erroFFmpeg);

            reportarStatus("Verificando yt-dlp...");
            string urlYtDlp = await ObterUrlYtDlpMaisRecenteAsync();
            var erroYtDlp = await BaixarOuAtualizarArquivo(urlYtDlp, Path.Combine(Environment.CurrentDirectory, "yt-dlp.exe"), false, "ytdlp_version.txt");
            if (erroYtDlp != null) erros.Add(erroYtDlp);

            return erros;
        }

        private static async Task<string> ObterUrlYtDlpMaisRecenteAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, UrlYtDlpApiLatest);
                request.Headers.UserAgent.ParseAdd("YoutubeDownloaderCS");
                var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return UrlYtDlpFallback;

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
                {
                    if (string.Equals(asset.GetProperty("name").GetString(), "yt-dlp.exe", StringComparison.OrdinalIgnoreCase))
                        return asset.GetProperty("browser_download_url").GetString() ?? UrlYtDlpFallback;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Falha ao consultar release mais recente do yt-dlp: " + ex.Message);
            }
            return UrlYtDlpFallback;
        }

        private static async Task<string?> BaixarOuAtualizarArquivo(string url, string caminhoDestino, bool ehZip, string arquivoVersao)
        {
            string caminhoVersao = Path.Combine(Environment.CurrentDirectory, arquivoVersao);
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await httpClient.SendAsync(request);

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
                        var dados = await httpClient.GetByteArrayAsync(url);
                        await Task.Run(() => SalvarArquivoAtomicamente(dados, caminhoDestino, ehZip));
                        File.WriteAllText(caminhoVersao, dataServidor.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Falha ao baixar {arquivoVersao}: {ex}");
                if (!File.Exists(caminhoDestino))
                    return $"Componente {Path.GetFileName(caminhoDestino)} ausente. Falha ao baixar: {ex.Message}";
            }
            return null;
        }

        // Baixa para um arquivo .tmp e só substitui o destino no final,
        // para não deixar um executável corrompido se o app fechar no meio do processo.
        private static void SalvarArquivoAtomicamente(byte[] dados, string caminhoDestino, bool ehZip)
        {
            string tmp = caminhoDestino + ".tmp";

            if (ehZip)
            {
                string pastaTemp = Path.Combine(Path.GetTempPath(), "yt_update_" + Guid.NewGuid());
                Directory.CreateDirectory(pastaTemp);
                try
                {
                    string zipTemp = Path.Combine(pastaTemp, "update.zip");
                    File.WriteAllBytes(zipTemp, dados);
                    ZipFile.ExtractToDirectory(zipTemp, pastaTemp);
                    string nomeExe = Path.GetFileName(caminhoDestino);
                    string[] exes = Directory.GetFiles(pastaTemp, nomeExe, SearchOption.AllDirectories);
                    if (exes.Length == 0) throw new Exception($"{nomeExe} não encontrado no pacote baixado.");
                    File.Copy(exes[0], tmp, true);
                }
                finally
                {
                    if (Directory.Exists(pastaTemp)) Directory.Delete(pastaTemp, true);
                }
            }
            else
            {
                File.WriteAllBytes(tmp, dados);
            }

            File.Move(tmp, caminhoDestino, true);
        }
    }
}
