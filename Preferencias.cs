using System;
using System.Collections.Generic;
using System.IO;

namespace YoutubeDownloaderCS
{
    // Guarda preferências simples entre execuções (pasta de destino, navegador dos cookies).
    internal static class Preferencias
    {
        private static readonly string CaminhoArquivo = Util.CaminhoNaPastaApp("preferencias.txt");
        private static readonly object Trava = new();

        public static string UltimaPasta
        {
            get
            {
                string pasta = Ler("ultimaPasta");
                return Directory.Exists(pasta) ? pasta : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            }
            set => Gravar("ultimaPasta", value);
        }

        // Navegador cujos cookies funcionaram para contornar o bloqueio de login do YouTube.
        public static string NavegadorCookies
        {
            get => Ler("navegadorCookies");
            set => Gravar("navegadorCookies", value);
        }

        private static Dictionary<string, string> Carregar()
        {
            var dados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(CaminhoArquivo)) return dados;
                foreach (var linha in File.ReadAllLines(CaminhoArquivo))
                {
                    int i = linha.IndexOf('=');
                    if (i > 0) dados[linha.Substring(0, i).Trim()] = linha.Substring(i + 1).Trim();
                }
            }
            catch { }
            return dados;
        }

        private static string Ler(string chave)
        {
            lock (Trava)
            {
                return Carregar().TryGetValue(chave, out var valor) ? valor : "";
            }
        }

        private static void Gravar(string chave, string valor)
        {
            lock (Trava)
            {
                try
                {
                    var dados = Carregar();
                    dados[chave] = valor;
                    File.WriteAllLines(CaminhoArquivo, new List<string>(ParesTexto(dados)));
                }
                catch { }
            }
        }

        private static IEnumerable<string> ParesTexto(Dictionary<string, string> dados)
        {
            foreach (var par in dados) yield return $"{par.Key}={par.Value}";
        }
    }
}
