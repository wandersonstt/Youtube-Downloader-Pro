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
    // O YouTube exigiu autenticação e não há cookies utilizáveis. Quem captura decide como
    // pedir o login ao usuário (a interface abre a janela embutida do app).
    internal sealed class LoginYoutubeNecessarioException : Exception
    {
        public IReadOnlyList<string> Orientacoes { get; }

        public LoginYoutubeNecessarioException(string detalheTecnico, IReadOnlyList<string> orientacoes)
            : base(detalheTecnico)
        {
            Orientacoes = orientacoes;
        }
    }

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

        // O Firefox vem primeiro de propósito: é o único que ainda entrega cookies no Windows.
        // Os demais são Chromium e usam App-Bound Encryption, que o yt-dlp não consegue abrir.
        private static readonly string[] NavegadoresSuportados = { "firefox", "chrome", "edge", "brave", "opera", "vivaldi", "chromium" };

        private static readonly string[] NavegadoresChromium = { "chrome", "edge", "brave", "opera", "vivaldi", "chromium" };

        internal static bool EhChromium(string navegador) =>
            NavegadoresChromium.Contains(navegador, StringComparer.OrdinalIgnoreCase);

        // Monta os argumentos do yt-dlp. Separado para poder ser validado por teste.
        // --no-playlist é obrigatório: sem ele, uma URL de vídeo que carregue "&list=" faz o
        // yt-dlp baixar a playlist inteira (e um Mix/Rádio do YouTube é infinito), gravando
        // vídeo após vídeo por cima do mesmo arquivo de destino.
        internal static string MontarArgumentos(string url, string destino, string? formato, bool extrairMp3, string ffmpegPath, string? navegadorCookies = null, string? arquivoCookies = null)
        {
            string cookies = !string.IsNullOrWhiteSpace(arquivoCookies) ? $"--cookies \"{arquivoCookies}\" "
                           : !string.IsNullOrWhiteSpace(navegadorCookies) ? $"--cookies-from-browser {navegadorCookies} "
                           : "";
            string comum = $"{cookies}--no-playlist --newline --ffmpeg-location \"{ffmpegPath}\" -o \"{destino}\" \"{url}\"";
            return extrairMp3
                ? $"-f bestaudio -x --audio-format mp3 {comum}"
                : $"-f \"{formato ?? "bestvideo+bestaudio/best"}\" --merge-output-format mp4 {comum}";
        }

        internal static string MontarArgumentosTitulo(string url) =>
            $"--print \"%(title)s\" --skip-download --no-playlist --playlist-items 1 --no-warnings \"{url}\"";

        // O yt-dlp emite vários WARNING antes do ERROR de verdade (ex: o aviso sobre runtime
        // JavaScript). Mostrar a primeira linha fazia o app exibir o aviso errado como causa.
        internal static string ExtrairErroRelevante(string saidaDeErro)
        {
            var linhas = saidaDeErro.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            if (linhas.Count == 0) return "";

            var erro = linhas.FirstOrDefault(l => l.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase));
            if (erro != null) return erro;

            var naoAviso = linhas.FirstOrDefault(l => !l.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase));
            return naoAviso ?? linhas[0];
        }

        // Traduz as falhas conhecidas de leitura de cookies em instruções que o usuário
        // consegue seguir. São os três casos reais: navegador aberto travando o banco,
        // criptografia nova do Chrome, e navegador não instalado.
        internal static string? ExplicarFalhaDeCookies(string erro, string navegador)
        {
            if (erro.Contains("Could not copy", StringComparison.OrdinalIgnoreCase))
                return $"Feche o {Capitalizar(navegador)} completamente (todas as janelas) e clique em Baixar novamente.";

            if (erro.Contains("DPAPI", StringComparison.OrdinalIgnoreCase))
                return $"O {Capitalizar(navegador)} passou a proteger os cookies de um jeito que impede a leitura. " +
                       "Use o Firefox (faça login no YouTube nele) ou exporte um arquivo cookies.txt para a pasta do programa.";

            if (erro.Contains("could not find", StringComparison.OrdinalIgnoreCase))
                return null; // navegador não instalado: não é problema, apenas passa para o próximo

            return null;
        }

        private static string Capitalizar(string texto) =>
            string.IsNullOrEmpty(texto) ? texto : char.ToUpper(texto[0]) + texto.Substring(1);

        // Um cookies.txt largado na pasta do programa vem primeiro, como o README promete: é a
        // saída de quem precisa sobrepor uma sessão ruim do app. Depois, os cookies gravados
        // pela janela de login.
        internal static string? ArquivoCookiesManual()
        {
            foreach (var caminho in new[] { Util.CaminhoNaPastaApp("cookies.txt"), LoginYoutube.CaminhoCookies })
                if (File.Exists(caminho)) return caminho;
            return null;
        }

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

            // Se o usuário deixou um cookies.txt na pasta, ele já vale na primeira tentativa.
            string? cookiesManuais = ArquivoCookiesManual();
            if (cookiesManuais != null) Logger.Info($"Usando cookies manuais de {cookiesManuais}");

            var resultado = Executar(ytDlpPath, MontarArgumentos(url, destino, formato, extrairMp3, ffmpegPath, null, cookiesManuais), destino, progresso, statusEtapa, token);
            if (resultado.Sucesso) return;

            if (!PareceBloqueioDeLogin(resultado.Erro))
                throw new Exception(ExtrairErroRelevante(resultado.Erro));

            var orientacoes = new List<string>();

            // Se a sessão do próprio app já foi usada e mesmo assim o YouTube pediu login, ela
            // expirou: vasculhar navegadores só gastaria ~20s para falhar. Pede login de novo.
            if (cookiesManuais != null)
            {
                Logger.Info($"Cookies de {cookiesManuais} não bastaram; pedindo login novamente.");

                // Sem apagar, toda tentativa futura repetiria esta execução condenada antes de
                // chegar no login. O cookies.txt colocado à mão pelo usuário não é nosso: fica.
                if (string.Equals(cookiesManuais, LoginYoutube.CaminhoCookies, StringComparison.OrdinalIgnoreCase))
                    try { File.Delete(cookiesManuais); } catch (Exception ex) { Logger.Erro("Falha ao descartar cookies expirados", ex); }

                throw new LoginYoutubeNecessarioException(ExtrairErroRelevante(resultado.Erro), orientacoes);
            }

            // O YouTube exigiu autenticação: repete o download reaproveitando os cookies de um
            // navegador onde o usuário já esteja logado.
            Logger.Info("YouTube exigiu login; tentando novamente com cookies do navegador.");

            bool chromiumJaRecusou = false;

            foreach (var navegador in NavegadoresParaTentar(Preferencias.NavegadorCookies))
            {
                token.ThrowIfCancellationRequested();

                // App-Bound Encryption vale para todos os Chromium de uma vez, mas não afeta o
                // Firefox (que usa NSS). Pular só os Chromium mantém alcançável o único
                // navegador que ainda funciona no Windows.
                if (chromiumJaRecusou && EhChromium(navegador)) continue;

                statusEtapa?.Report($"YouTube pediu login: tentando cookies do {navegador}...");

                var comCookies = Executar(ytDlpPath, MontarArgumentos(url, destino, formato, extrairMp3, ffmpegPath, navegador), destino, progresso, statusEtapa, token);
                if (comCookies.Sucesso)
                {
                    Logger.Info($"Download concluído usando cookies do {navegador}.");
                    Preferencias.NavegadorCookies = navegador;
                    return;
                }

                string motivo = ExtrairErroRelevante(comCookies.Erro);
                Logger.Info($"Cookies do {navegador} não funcionaram: {motivo}");

                var orientacao = ExplicarFalhaDeCookies(motivo, navegador);
                if (orientacao != null && !orientacoes.Contains(orientacao)) orientacoes.Add(orientacao);

                if (motivo.Contains("DPAPI", StringComparison.OrdinalIgnoreCase)) chromiumJaRecusou = true;
            }

            // Nenhum navegador entregou os cookies (nos Chromium modernos isso é esperado:
            // o App-Bound Encryption impede a leitura). Quem trata é a interface, abrindo a
            // janela de login própria do app.
            throw new LoginYoutubeNecessarioException(ExtrairErroRelevante(resultado.Erro), orientacoes);
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
