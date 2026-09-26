namespace YoutubeDownloaderCS
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Processos elevados por UAC (requireAdministrator) podem iniciar com a
            // pasta de trabalho errada (ex: C:\Windows\System32). Como o app usa
            // Environment.CurrentDirectory para achar ffmpeg.exe/yt-dlp.exe, isso os
            // fazia "sumir" depois de exigirmos administrador. Fixa aqui na pasta real do app.
            Environment.CurrentDirectory = AppContext.BaseDirectory;

            // Sem isso, qualquer exceção não tratada (ex: em callbacks de progresso)
            // derruba o app inteiro sem nenhum aviso ao usuário.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => MostrarErroFatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => MostrarErroFatal(e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, e) => { MostrarErroFatal(e.Exception); e.SetObserved(); };

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }

        private static void MostrarErroFatal(Exception? ex)
        {
            Logger.Erro("Exceção não tratada", ex ?? new Exception("desconhecida"));
            MessageBox.Show($"Ocorreu um erro inesperado:\n\n{ex}\n\nDetalhes salvos em log.txt, na pasta do programa.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}