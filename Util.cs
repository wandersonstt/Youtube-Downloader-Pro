using System;
using System.IO;
using System.Text.RegularExpressions;

namespace YoutubeDownloaderCS
{
    internal static class Util
    {
        // Pasta onde o app e seus componentes (ffmpeg.exe, yt-dlp.exe, log.txt) vivem.
        // Nunca usar Environment.CurrentDirectory para isso: ele muda quando o usuário
        // navega num diálogo de salvar e também vem errado quando o Windows eleva o
        // processo por UAC.
        public static string PastaApp => AppContext.BaseDirectory;

        public static string CaminhoNaPastaApp(string arquivo) => Path.Combine(PastaApp, arquivo);

        // Aceita texto colado com lixo em volta ("olha esse vídeo https://... valeu") e extrai só o link.
        public static string ExtrairLink(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            var match = Regex.Match(texto, @"https?://[^\s]+");
            return match.Success ? match.Value : texto.Trim();
        }

        public static string LimparNome(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return "download";
            string limpo = string.Join("_", nome.Split(Path.GetInvalidFileNameChars())).Trim();
            // Windows corta espaços/pontos finais silenciosamente e reclama de nomes muito longos.
            limpo = limpo.TrimEnd('.', ' ');
            if (limpo.Length > 120) limpo = limpo.Substring(0, 120).TrimEnd('.', ' ');
            return string.IsNullOrWhiteSpace(limpo) ? "download" : limpo;
        }
    }
}
