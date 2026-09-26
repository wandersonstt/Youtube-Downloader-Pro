using System;
using System.IO;

namespace YoutubeDownloaderCS
{
    // Lembra a última pasta de destino usada, persistida entre execuções.
    internal static class Preferencias
    {
        private static readonly string CaminhoArquivo = Path.Combine(Environment.CurrentDirectory, "preferencias.txt");

        public static string UltimaPasta
        {
            get
            {
                try
                {
                    if (File.Exists(CaminhoArquivo))
                    {
                        string pasta = File.ReadAllText(CaminhoArquivo).Trim();
                        if (Directory.Exists(pasta)) return pasta;
                    }
                }
                catch { }
                return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            }
            set
            {
                try { File.WriteAllText(CaminhoArquivo, value); } catch { }
            }
        }
    }
}
