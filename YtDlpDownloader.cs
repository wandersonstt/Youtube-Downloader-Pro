using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace YoutubeDownloaderCS
{
    // Executa yt-dlp.exe para o "Modo Universal", reportando progresso real e suportando cancelamento.
    internal static class YtDlpDownloader
    {
        private static readonly Regex RegexProgresso = new Regex(@"\[download\]\s+(\d{1,3}(?:\.\d+)?)%", RegexOptions.Compiled);
        private static readonly Regex RegexEtapa = new Regex(@"^\[(\w+)\]", RegexOptions.Compiled);

        private static readonly Dictionary<string, string> NomesEtapas = new()
        {
            ["ExtractAudio"] = "Convertendo para MP3...",
            ["Merger"] = "Juntando vídeo e áudio...",
            ["VideoRemuxer"] = "Finalizando vídeo...",
            ["Metadata"] = "Adicionando metadados...",
            ["Fixup"] = "Finalizando arquivo...",
        };

        private static readonly string[] NavegadoresSuportados = { "chrome", "edge", "brave", "firefox", "opera", "vivaldi", "chromium" };

        // Monta os argumentos do yt-dlp. Separado para poder ser validado por teste.
        // --no-playlist é obrigatório: sem ele, uma URL de vídeo que carregue "&list=" faz o
        // yt-dlp baixar a playlist inteira (e um Mix/Rádio do YouTube é infinito), gravando
        // vídeo após vídeo por cima do mesmo arquivo de destino.
        internal static string MontarArgumentos(string url, string destino, string? formato, bool extrairMp3, string ffmpegPath, string? navegadorCookies = null)
        {
            string cookies = string.IsNullOrWhiteSpace(navegadorCookies) ? "" : $"--cookies-from-browser {navegadorCookies} ";
            string comum = $"{cookies}--no-playlist --newline --ffmpeg-location \"{ffmpegPath}\" -o \"{destino}\" \"{url}\"";
            return extrairMp3
                ? $"-f bestaudio -x --audio-format mp3 {comum}"
                : $"-f \"{formato ?? "bestvideo+bestaudio/best"}\" --merge-output-format mp4 {comum}";
        }

        internal static string MontarArgumentosTitulo(string url) =>
            $"--print \"%(title)s\" --skip-download --no-playlist --playlist-items 1 --no-warnings \"{url}\"";

        // O YouTube passou a exigir login para parte dos vídeos ("Sign in to confirm you're not
        // a bot"). Nesses casos o download só funciona reaproveitando os cookies do navegador.
        internal static bool PareceBloqueioDeLogin(string erro) =>
            erro.Contains("Sign in to confirm", StringComparison.OrdinalIgnoreCase)
            || erro.Contains("not a bot", StringComparison.OrdinalIgnoreCase)
            || erro.Contains("--cookies", StringComparison.OrdinalIgnoreCase)
            || erro.Contains("Please sign in", StringComparison.OrdinalIgnoreCase)
            || erro.Contains("age-restricted", StringComparison.OrdinalIgnoreCase);

        // Tenta primeiro o navegador que já funcionou antes (se houver), depois os demais.
        internal static IEnumerable<string> NavegadoresParaTentar(string? preferido)
        {
            if (!string.IsNullOrWhiteSpace(preferido)) yield return preferido;
            foreach (var n in NavegadoresSuportados)
                if (!string.Equals(n, preferido, StringComparison.OrdinalIgnoreCase)) yield return n;
        }

        // Consulta o título real do vídeo sem baixar nada, para nomear o arquivo corretamente
        // mesmo quando o YoutubeExplode falha (modo Universal / sites externos).
        public static string? ObterTitulo(string url, TimeSpan timeout)
        {
            string ytDlpPath = Util.CaminhoNaPastaApp("yt-dlp.exe");
            if (!File.Exists(ytDlpPath)) return null;

            var startInfo = CriarStartInfo(ytDlpPath, MontarArgumentosTitulo(url));

            try
            {
                using var process = new Process { StartInfo = startInfo };
                var saida = new StringBuilder();
                // Lê stdout/stderr de forma assíncrona: ReadToEnd() bloqueava sem respeitar o
                // timeout, e o stderr nunca era drenado (podia travar o processo se enchesse o buffer).
                process.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) lock (saida) saida.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { };

                process.Start();
                process.StandardInput.Close(); // evita que o processo trave esperando input que nunca virá
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    Logger.Erro($"ObterTitulo: yt-dlp não respondeu em {timeout.TotalSeconds}s para '{url}'");
                    try { process.Kill(true); } catch { }
                    return null;
                }
                // WaitForExit(int) não espera os handlers assíncronos terminarem; o sem-argumento espera.
                process.WaitForExit();

                string titulo;
                lock (saida) titulo = saida.ToString().Trim();
                return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(titulo) ? titulo : null;
            }
            catch (Exception ex)
            {
                Logger.Erro("ObterTitulo falhou", ex);
                return null;
            }
        }

        public static void Baixar(string url, string destino, string? formato, bool extrairMp3, IProgress<double>? progresso, IProgress<string>? statusEtapa, CancellationToken token)
        {
            string ytDlpPath = Util.CaminhoNaPastaApp("yt-dlp.exe");
            if (!File.Exists(ytDlpPath))
            {
                Logger.Erro("yt-dlp.exe não encontrado em " + ytDlpPath);
                throw new Exception("yt-dlp.exe não encontrado.");
            }
            string ffmpegPath = Util.CaminhoNaPastaApp("ffmpeg.exe");
            if (!File.Exists(ffmpegPath))
                Logger.Erro("ffmpeg.exe não encontrado em " + ffmpegPath);

            var resultado = Executar(ytDlpPath, MontarArgumentos(url, destino, formato, extrairMp3, ffmpegPath), destino, progresso, statusEtapa, token);
            if (resultado.Sucesso) return;

            if (!PareceBloqueioDeLogin(resultado.Erro))
                throw new Exception(resultado.Erro);

            // O YouTube exigiu autenticação: repete o download reaproveitando os cookies de um
            // navegador onde o usuário já esteja logado.
            Logger.Info("YouTube exigiu login; tentando novamente com cookies do navegador.");
            foreach (var navegador in NavegadoresParaTentar(Preferencias.NavegadorCookies))
            {
                token.ThrowIfCancellationRequested();
                statusEtapa?.Report($"YouTube pediu login: tentando cookies do {navegador}...");

                var comCookies = Executar(ytDlpPath, MontarArgumentos(url, destino, formato, extrairMp3, ffmpegPath, navegador), destino, progresso, statusEtapa, token);
                if (comCookies.Sucesso)
                {
                    Logger.Info($"Download concluído usando cookies do {navegador}.");
                    Preferencias.NavegadorCookies = navegador;
                    return;
                }
                Logger.Info($"Cookies do {navegador} não funcionaram: {PrimeiraLinha(comCookies.Erro)}");
            }

            throw new Exception(
                "O YouTube exigiu login para este vídeo e não foi possível usar os cookies do navegador.\n\n" +
                "Tente: abrir o vídeo logado no navegador, fechar o navegador completamente e baixar de novo.\n\n" +
                "Detalhe técnico: " + PrimeiraLinha(resultado.Erro));
        }

        // O arquivo só conta como resultado deste download se foi gravado depois que
        // o processo começou — caso contrário seria o resto de uma tentativa anterior.
        private static bool ArquivoGravadoAgora(string destino, DateTime inicio)
        {
            try
            {
                if (!File.Exists(destino)) return false;
                var info = new FileInfo(destino);
                return info.Length > 0 && info.LastWriteTime >= inicio.AddSeconds(-2);
            }
            catch { return false; }
        }

        private static string PrimeiraLinha(string texto)
        {
            var linha = texto.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
            return (linha ?? texto).Trim();
        }

        private static ProcessStartInfo CriarStartInfo(string exe, string args) => new()
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        private readonly record struct Resultado(bool Sucesso, string Erro);

        private static Resultado Executar(string ytDlpPath, string args, string destino, IProgress<double>? progresso, IProgress<string>? statusEtapa, CancellationToken token)
        {
            Logger.Info($"Executando: {ytDlpPath} {args}");

            using var process = new Process { StartInfo = CriarStartInfo(ytDlpPath, args) };
            var erro = new StringBuilder();
            long ultimaAtividadeTicks = DateTime.UtcNow.Ticks;

            process.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                Interlocked.Exchange(ref ultimaAtividadeTicks, DateTime.UtcNow.Ticks);

                var matchProgresso = RegexProgresso.Match(e.Data);
                if (matchProgresso.Success && double.TryParse(matchProgresso.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
                {
                    progresso?.Report(pct / 100.0);
                    return;
                }

                var matchEtapa = RegexEtapa.Match(e.Data);
                if (matchEtapa.Success && NomesEtapas.TryGetValue(matchEtapa.Groups[1].Value, out var nomeEtapa))
                    statusEtapa?.Report(nomeEtapa);
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                Interlocked.Exchange(ref ultimaAtividadeTicks, DateTime.UtcNow.Ticks);
                lock (erro) erro.AppendLine(e.Data); // o handler roda em thread de I/O
            };

            process.Start();
            process.StandardInput.Close(); // evita que o processo trave esperando input que nunca virá
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Guardado antes de começar: um arquivo de destino que já existia (tentativa
            // anterior, download antigo com o mesmo nome) não pode contar como "pronto".
            DateTime inicio = DateTime.Now;

            bool finalizadoPorInatividade = false;
            using (token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } }))
            {
                // yt-dlp/ffmpeg às vezes trava numa etapa de limpeza sem importância mesmo
                // com o arquivo final já pronto. Se não sair nenhum log novo por um tempo E
                // o arquivo de destino já existir, considera concluído e encerra o processo.
                while (!process.WaitForExit(2000))
                {
                    if (token.IsCancellationRequested) break;

                    var inativoHa = DateTime.UtcNow - new DateTime(Interlocked.Read(ref ultimaAtividadeTicks), DateTimeKind.Utc);
                    if (inativoHa > TimeSpan.FromSeconds(45) && ArquivoGravadoAgora(destino, inicio))
                    {
                        Logger.Info($"Processo parado há {inativoHa.TotalSeconds:F0}s com '{destino}' já pronto. Encerrando e considerando sucesso.");
                        finalizadoPorInatividade = true;
                        try { process.Kill(true); } catch { }
                        break;
                    }
                }
            }

            token.ThrowIfCancellationRequested();
            if (finalizadoPorInatividade) return new Resultado(true, "");

            process.WaitForExit(); // garante o flush dos handlers assíncronos antes de ler a saída
            string mensagem;
            lock (erro) mensagem = erro.ToString().Trim();

            if (process.ExitCode == 0)
            {
                Logger.Info($"Download concluído: {destino}");
                return new Resultado(true, "");
            }

            if (string.IsNullOrWhiteSpace(mensagem)) mensagem = $"yt-dlp falhou (código {process.ExitCode}).";
            Logger.Erro($"yt-dlp saiu com código {process.ExitCode}. Erro: {mensagem}");
            return new Resultado(false, mensagem);
        }
    }
}
