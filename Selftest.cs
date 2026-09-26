using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace YoutubeDownloaderCS
{
    // Testes executáveis das regras que não dependem de interface nem de rede.
    // Rodar com: YoutubeDownloaderCS.exe --selftest   (ou dotnet run -- --selftest)
    internal static class Selftest
    {
        private static int falhas;

        public static int Executar()
        {
            falhas = 0;

            TestarArgumentosDownload();
            TestarArgumentosTitulo();
            TestarDeteccaoBloqueioLogin();
            TestarOrdemNavegadores();
            TestarMensagensDeErro();
            TestarParsingProgresso();
            TestarParsingEtapa();
            TestarLimparNome();
            TestarExtrairLink();
            TestarFormatoCompativel();
            TestarPastaDoApp();
            TestarLayoutDaJanela();

            Console.WriteLine(falhas == 0 ? "\nTODOS OS TESTES PASSARAM" : $"\n{falhas} TESTE(S) FALHARAM");
            return falhas == 0 ? 0 : 1;
        }

        private static void Verificar(string descricao, bool condicao)
        {
            Console.WriteLine($"{(condicao ? "  ok  " : " FALHA")} {descricao}");
            if (!condicao) falhas++;
        }

        // O bug mais grave encontrado: sem --no-playlist, uma URL com "&list=" fazia o yt-dlp
        // baixar a playlist inteira (Mix/Rádio é infinito) por cima do mesmo arquivo.
        private static void TestarArgumentosDownload()
        {
            Console.WriteLine("\n[Argumentos do download]");
            string video = YtDlpDownloader.MontarArgumentos(
                "https://www.youtube.com/watch?v=abc&list=RDabc&start_radio=1",
                @"C:\Videos\musica.mp4", "bestvideo[height<=360]+bestaudio/best[height<=360]", false, @"C:\App\ffmpeg.exe");

            Verificar("vídeo: contém --no-playlist", video.Contains("--no-playlist"));
            Verificar("vídeo: contém --newline (progresso linha a linha)", video.Contains("--newline"));
            Verificar("vídeo: formato selecionado é respeitado", video.Contains("bestvideo[height<=360]+bestaudio/best[height<=360]"));
            Verificar("vídeo: saída forçada para mp4", video.Contains("--merge-output-format mp4"));
            Verificar("vídeo: caminhos com espaço vão entre aspas", video.Contains("\"C:\\Videos\\musica.mp4\""));
            Verificar("vídeo: ffmpeg apontado explicitamente", video.Contains("--ffmpeg-location \"C:\\App\\ffmpeg.exe\""));

            string audio = YtDlpDownloader.MontarArgumentos(
                "https://youtu.be/abc", @"C:\Musicas\som.mp3", null, true, @"C:\App\ffmpeg.exe");

            Verificar("áudio: contém --no-playlist", audio.Contains("--no-playlist"));
            Verificar("áudio: extrai para mp3", audio.Contains("-x --audio-format mp3"));
            Verificar("áudio: não força container de vídeo", !audio.Contains("--merge-output-format"));
        }

        private static void TestarArgumentosTitulo()
        {
            Console.WriteLine("\n[Argumentos da consulta de título]");
            string args = YtDlpDownloader.MontarArgumentosTitulo("https://www.youtube.com/watch?v=abc&list=RDabc");

            Verificar("contém --no-playlist", args.Contains("--no-playlist"));
            Verificar("limita a 1 item (Mix é infinito)", args.Contains("--playlist-items 1"));
            Verificar("não baixa nada", args.Contains("--skip-download"));
            Verificar("imprime só o título", args.Contains("--print \"%(title)s\""));
        }

        // Texto real devolvido pelo yt-dlp quando o YouTube exige autenticação.
        private static void TestarDeteccaoBloqueioLogin()
        {
            Console.WriteLine("\n[Detecção do bloqueio de login do YouTube]");
            const string erroReal = "ERROR: [youtube] QQDcOFBLzi4: Sign in to confirm you're not a bot. " +
                                    "Use --cookies-from-browser or --cookies for the authentication.";

            Verificar("reconhece o erro real do YouTube", YtDlpDownloader.PareceBloqueioDeLogin(erroReal));
            Verificar("reconhece vídeo com restrição de idade", YtDlpDownloader.PareceBloqueioDeLogin("ERROR: This video is age-restricted"));
            Verificar("não confunde com erro de rede", !YtDlpDownloader.PareceBloqueioDeLogin("ERROR: unable to download video data: HTTP Error 403"));
            Verificar("não confunde com vídeo removido", !YtDlpDownloader.PareceBloqueioDeLogin("ERROR: Video unavailable. This video has been removed"));

            string comCookies = YtDlpDownloader.MontarArgumentos("https://x/v", "a.mp4", null, false, "ff.exe", "brave");
            Verificar("argumento de cookies é adicionado quando pedido", comCookies.Contains("--cookies-from-browser brave"));
            Verificar("sem navegador, não passa cookies",
                !YtDlpDownloader.MontarArgumentos("https://x/v", "a.mp4", null, false, "ff.exe").Contains("--cookies-from-browser"));
        }

        private static void TestarOrdemNavegadores()
        {
            Console.WriteLine("\n[Ordem de tentativa dos navegadores]");
            var comPreferido = new List<string>(YtDlpDownloader.NavegadoresParaTentar("brave"));
            var semPreferido = new List<string>(YtDlpDownloader.NavegadoresParaTentar(null));

            Verificar("navegador que já funcionou é tentado primeiro", comPreferido[0] == "brave");
            Verificar("não repete o preferido na lista", comPreferido.FindAll(n => n == "brave").Count == 1);
            Verificar("cobre os navegadores principais",
                semPreferido.Contains("chrome") && semPreferido.Contains("edge") && semPreferido.Contains("firefox") && semPreferido.Contains("brave"));
        }

        // Saídas reais coletadas rodando o yt-dlp na máquina do usuário.
        private static void TestarMensagensDeErro()
        {
            Console.WriteLine("\n[Mensagem de erro mostrada ao usuário]");

            const string saidaReal =
                "WARNING: [youtube] No supported JavaScript runtime could be found. Only deno is enabled by default\n" +
                "WARNING: [youtube] No title found in player responses; falling back to title from initial data\n" +
                "ERROR: [youtube] QQDcOFBLzi4: Sign in to confirm you're not a bot.";

            string relevante = YtDlpDownloader.ExtrairErroRelevante(saidaReal);
            Verificar("escolhe a linha ERROR, não o WARNING", relevante.StartsWith("ERROR:"));
            Verificar("não mostra o aviso de runtime JavaScript", !relevante.Contains("JavaScript runtime"));
            Verificar("só com avisos, ainda devolve algo",
                !string.IsNullOrWhiteSpace(YtDlpDownloader.ExtrairErroRelevante("WARNING: algo\nWARNING: outro")));
            Verificar("saída vazia não quebra", YtDlpDownloader.ExtrairErroRelevante("") == "");

            // Os três erros de cookie que realmente aparecem no Windows.
            string? aberto = YtDlpDownloader.ExplicarFalhaDeCookies("ERROR: Could not copy Chrome cookie database", "brave");
            Verificar("navegador aberto vira instrução de fechar", aberto != null && aberto.Contains("Feche o Brave"));

            string? dpapi = YtDlpDownloader.ExplicarFalhaDeCookies("ERROR: Failed to decrypt with DPAPI", "chrome");
            Verificar("proteção do Chrome sugere alternativa", dpapi != null && (dpapi.Contains("Firefox") || dpapi.Contains("cookies.txt")));

            Verificar("navegador não instalado não vira orientação",
                YtDlpDownloader.ExplicarFalhaDeCookies("ERROR: could not find firefox cookies database", "firefox") == null);

            string comArquivo = YtDlpDownloader.MontarArgumentos("https://x/v", "a.mp4", null, false, "ff.exe", null, @"C:\App\cookies.txt");
            Verificar("cookies.txt tem prioridade sobre navegador", comArquivo.Contains("--cookies \"C:\\App\\cookies.txt\"") && !comArquivo.Contains("--cookies-from-browser"));
        }

        // Mesmo regex usado para mover a barra de progresso no modo Universal.
        private static void TestarParsingProgresso()
        {
            Console.WriteLine("\n[Leitura do progresso do yt-dlp]");
            var regex = new Regex(@"\[download\]\s+(\d{1,3}(?:\.\d+)?)%");

            double? Ler(string linha)
            {
                var m = regex.Match(linha);
                if (!m.Success) return null;
                return double.Parse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            Verificar("linha inicial 0.0%", Ler("[download]   0.0% of ~  10.00MiB at  1.00MiB/s ETA 00:10") == 0.0);
            Verificar("linha intermediária 42.5%", Ler("[download]  42.5% of   10.00MiB at  2.00MiB/s ETA 00:05") == 42.5);
            Verificar("linha final 100%", Ler("[download] 100% of 10.00MiB in 00:05") == 100.0);
            Verificar("decimal lido como ponto mesmo em pt-BR", Ler("[download]  99.9% of 10.00MiB") == 99.9);
            Verificar("linha sem porcentagem é ignorada", Ler("[download] Destination: video.mp4") == null);
            Verificar("linha de outra etapa é ignorada", Ler("[Merger] Merging formats into \"video.mp4\"") == null);
        }

        private static void TestarParsingEtapa()
        {
            Console.WriteLine("\n[Leitura da etapa atual]");
            var regex = new Regex(@"^\[(\w+)\]");

            string? Etapa(string linha)
            {
                var m = regex.Match(linha);
                return m.Success ? m.Groups[1].Value : null;
            }

            Verificar("detecta Merger", Etapa("[Merger] Merging formats into \"a.mp4\"") == "Merger");
            Verificar("detecta ExtractAudio", Etapa("[ExtractAudio] Destination: a.mp3") == "ExtractAudio");
            Verificar("ignora linha sem colchete", Etapa("Deleting original file a.webm") == null);
        }

        private static void TestarLimparNome()
        {
            Console.WriteLine("\n[Sanitização de nome de arquivo]");
            Verificar("remove barras e dois-pontos", !Util.LimparNome("AC/DC: Back in Black").Contains('/') && !Util.LimparNome("AC/DC: Back in Black").Contains(':'));
            Verificar("preserva acentos", Util.LimparNome("Música Sertaneja").Contains("Música"));
            Verificar("não termina com ponto (Windows corta)", !Util.LimparNome("Vídeo Final...").EndsWith("."));
            Verificar("trunca título gigante", Util.LimparNome(new string('a', 400)).Length <= 120);
            Verificar("nome vazio vira fallback", Util.LimparNome("   ") == "download");
            Verificar("nome só com caracteres inválidos vira fallback", Util.LimparNome("///") == "download" || Util.LimparNome("///") == "___");
        }

        // O YouTube entrega AV1/Opus por padrão, que o Windows não reproduz sem codecs extras.
        private static void TestarFormatoCompativel()
        {
            Console.WriteLine("\n[Preferência por codecs compatíveis]");
            string f720 = Form1.FormatoCompativel(720);
            string fAuto = Form1.FormatoCompativel(null);

            Verificar("prioriza H.264 (avc1)", f720.StartsWith("bestvideo[height<=720][vcodec^=avc1]"));
            Verificar("prioriza áudio AAC (mp4a)", f720.Contains("bestaudio[acodec^=mp4a]"));
            Verificar("respeita o limite de altura em todas as alternativas",
                f720.Split('/').Length == 3 && f720.Split('/')[1].Contains("[height<=720]") && f720.Split('/')[2].Contains("[height<=720]"));
            Verificar("tem alternativa quando não há H.264", f720.Contains("/bestvideo[height<=720]+bestaudio/"));
            Verificar("modo automático não limita altura", !fAuto.Contains("height<="));
        }

        // Componentes precisam ser localizados pela pasta do executável: diálogos de salvar
        // trocam o diretório de trabalho e a elevação por UAC também o altera.
        private static void TestarPastaDoApp()
        {
            Console.WriteLine("\n[Resolução de caminhos]");
            string esperado = System.IO.Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");
            Verificar("caminho resolvido a partir da pasta do executável", Util.CaminhoNaPastaApp("yt-dlp.exe") == esperado);

            string antes = Util.PastaApp;
            Environment.CurrentDirectory = System.IO.Path.GetTempPath(); // simula o diálogo mudando o diretório
            Verificar("não depende do diretório de trabalho atual", Util.PastaApp == antes);
            Environment.CurrentDirectory = AppContext.BaseDirectory;

            Verificar("log fica na pasta do app", Logger.CaminhoArquivo.StartsWith(AppContext.BaseDirectory));
        }

        // O painel inferior usava coordenadas fixas e ficava sobreposto à barra de progresso
        // quando o Windows estava com escala de DPI acima de 100%.
        private static void TestarLayoutDaJanela()
        {
            Console.WriteLine("\n[Layout da janela]");
            try
            {
                System.Windows.Forms.Application.EnableVisualStyles();
                using var form = new Form1();

                System.Windows.Forms.TabControl? abas = null;
                foreach (System.Windows.Forms.Control c in form.Controls)
                    if (c is System.Windows.Forms.TabControl tc) abas = tc;

                if (abas == null) { Verificar("painel de abas existe", false); return; }

                Verificar("painel de abas existe", true);
                Verificar("tem aba Histórico e aba Log",
                    abas.TabPages.Count == 2 && abas.TabPages[0].Text == "Histórico" && abas.TabPages[1].Text == "Log");

                bool sobrepoe = false;
                string culpado = "";
                foreach (System.Windows.Forms.Control c in form.Controls)
                {
                    if (ReferenceEquals(c, abas)) continue;
                    if (c.Bounds.IntersectsWith(abas.Bounds)) { sobrepoe = true; culpado = c.Name; }
                }
                Verificar($"abas não sobrepõem nenhum controle{(sobrepoe ? $" (sobrepondo: {culpado})" : "")}", !sobrepoe);
                Verificar("abas cabem dentro da janela", abas.Bottom <= form.ClientSize.Height);
                Verificar("janela alta o bastante para o painel", form.ClientSize.Height > abas.Top + 100);
            }
            catch (Exception ex)
            {
                Verificar("criar a janela sem exceção: " + ex.Message, false);
            }
        }

        private static void TestarExtrairLink()
        {
            Console.WriteLine("\n[Extração de link colado]");
            Verificar("extrai link no meio do texto",
                Util.ExtrairLink("olha isso https://youtu.be/abc123 muito bom") == "https://youtu.be/abc123");
            Verificar("mantém link já limpo",
                Util.ExtrairLink("https://www.youtube.com/watch?v=abc") == "https://www.youtube.com/watch?v=abc");
            Verificar("preserva parâmetros da query",
                Util.ExtrairLink("https://www.youtube.com/watch?v=abc&list=RDabc").Contains("list=RDabc"));
            Verificar("texto vazio devolve vazio", Util.ExtrairLink("   ") == "");
        }
    }
}
