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
            MessageBox.Show($"Ocorreu um erro inesperado:\n\n{ex}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}