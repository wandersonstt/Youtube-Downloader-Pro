using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace YoutubeDownloaderCS
{
    // Executa yt-dlp.exe para o "Modo Universal", reportando progresso real e suportando cancelamento.
    internal static class YtDlpDownloader
    {
        private static readonly Regex RegexProgresso = new Regex(@"\[download\]\s+(\d{1,3}(?:\.\d+)?)%", RegexOptions.Compiled);

        public static void Baixar(string url, string destino, string? formato, bool extrairMp3, IProgress<double>? progresso, CancellationToken token)
        {
            string ytDlpPath = Path.Combine(Environment.CurrentDirectory, "yt-dlp.exe");
            if (!File.Exists(ytDlpPath)) throw new Exception("yt-dlp.exe não encontrado.");
            string ffmpegPath = Path.Combine(Environment.CurrentDirectory, "ffmpeg.exe");

            string args = extrairMp3
                ? $"-f bestaudio -x --audio-format mp3 --newline --ffmpeg-location \"{ffmpegPath}\" -o \"{destino}\" \"{url}\""
                : $"-f \"{formato ?? "bestvideo+bestaudio/best"}\" --merge-output-format mp4 --newline --ffmpeg-location \"{ffmpegPath}\" -o \"{destino}\" \"{url}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = startInfo };
            string erroCompleto = "";

            process.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                var match = RegexProgresso.Match(e.Data);
                if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
                    progresso?.Report(pct / 100.0);
            };
            process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) erroCompleto += e.Data + "\n"; };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using (token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } }))
            {
                process.WaitForExit();
            }

            token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new Exception(erroCompleto);
        }
    }
}
