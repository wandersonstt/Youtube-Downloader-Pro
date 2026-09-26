using System;
using System.IO;

namespace YoutubeDownloaderCS
{
    // Log simples em arquivo texto, na mesma pasta do app, para diagnosticar erros
    // sem precisar reproduzir o problema junto com o usuário.
    internal static class Logger
    {
        public static string CaminhoArquivo => Util.CaminhoNaPastaApp("log.txt");

        private static readonly object Trava = new();
        private const long TamanhoMaximoBytes = 5 * 1024 * 1024; // 5MB

        public static void Info(string mensagem) => Escrever("INFO", mensagem);

        public static void Erro(string contexto, Exception ex) => Escrever("ERRO", $"{contexto}: {ex}");

        public static void Erro(string mensagem) => Escrever("ERRO", mensagem);

        // Lê o log para exibição na interface. Usa FileShare amplo porque o próprio
        // app pode estar escrevendo no arquivo neste momento.
        public static string Ler()
        {
            try
            {
                lock (Trava)
                {
                    if (!File.Exists(CaminhoArquivo)) return "(nenhum evento registrado ainda)";
                    using var fs = new FileStream(CaminhoArquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var sr = new StreamReader(fs);
                    string conteudo = sr.ReadToEnd();
                    return string.IsNullOrWhiteSpace(conteudo) ? "(nenhum evento registrado ainda)" : conteudo;
                }
            }
            catch (Exception ex)
            {
                return "Não foi possível ler o log: " + ex.Message;
            }
        }

        public static void Limpar()
        {
            try
            {
                lock (Trava)
                {
                    if (File.Exists(CaminhoArquivo)) File.Delete(CaminhoArquivo);
                }
            }
            catch { }
        }

        private static void Escrever(string nivel, string mensagem)
        {
            try
            {
                lock (Trava)
                {
                    if (File.Exists(CaminhoArquivo) && new FileInfo(CaminhoArquivo).Length > TamanhoMaximoBytes)
                        File.Delete(CaminhoArquivo); // evita crescer para sempre

                    string linha = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{nivel}] {mensagem}{Environment.NewLine}";
                    File.AppendAllText(CaminhoArquivo, linha);
                }
            }
            catch
            {
                // Se nem o log conseguir gravar (ex: sem espaço em disco), não deve derrubar o app.
            }
        }
    }
}
