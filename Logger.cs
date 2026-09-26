using System;
using System.IO;

namespace YoutubeDownloaderCS
{
    // Log simples em arquivo texto, na mesma pasta do app, para diagnosticar erros
    // sem precisar reproduzir o problema junto com o usuário.
    internal static class Logger
    {
        private static readonly string CaminhoArquivo = Path.Combine(Environment.CurrentDirectory, "log.txt");
        private static readonly object Trava = new();
        private const long TamanhoMaximoBytes = 5 * 1024 * 1024; // 5MB

        public static void Info(string mensagem) => Escrever("INFO", mensagem);

        public static void Erro(string contexto, Exception ex) => Escrever("ERRO", $"{contexto}: {ex}");

        public static void Erro(string mensagem) => Escrever("ERRO", mensagem);

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
