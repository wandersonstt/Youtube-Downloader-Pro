namespace YoutubeDownloaderCS
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] argumentos)
        {
            if (argumentos.Length > 0 && argumentos[0] == "--selftest")
                return ExecutarTestes();

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
            RegistrarDiagnostico();

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
            return 0;
        }

        // Fotografia do ambiente a cada inicialização: é o que permite entender um problema
        // relatado pelo usuário apenas lendo o log.txt, sem precisar reproduzir.
        private static void RegistrarDiagnostico()
        {
            try
            {
                var versao = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                bool admin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

                string Situacao(string arquivo)
                {
                    string caminho = Util.CaminhoNaPastaApp(arquivo);
                    if (!System.IO.File.Exists(caminho)) return "AUSENTE";
                    var info = new System.IO.FileInfo(caminho);
                    return $"{info.Length / 1024 / 1024}MB, {info.LastWriteTime:yyyy-MM-dd}";
                }

                Logger.Info($"--- Início: v{versao} | admin={admin} | pasta={Util.PastaApp} " +
                            $"| ffmpeg={Situacao("ffmpeg.exe")} | yt-dlp={Situacao("yt-dlp.exe")} ---");
            }
            catch { }
        }

        // App WinForms não tem console próprio: anexa ao console de quem chamou
        // para que a saída dos testes apareça no terminal.
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        private static int ExecutarTestes()
        {
            AttachConsole(-1);
            return Selftest.Executar();
        }

        private static void MostrarErroFatal(Exception? ex)
        {
            Logger.Erro("Exceção não tratada", ex ?? new Exception("desconhecida"));
            MessageBox.Show($"Ocorreu um erro inesperado:\n\n{ex}\n\nDetalhes salvos em log.txt, na pasta do programa.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}