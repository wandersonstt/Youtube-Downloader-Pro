; Script do Inno Setup para gerar um instalador amigável do Youtube Downloader Pro.
; Compilar com: ISCC installer\setup.iss /DMyAppVersion=X.Y.Z.W
; (a versão é injetada pelo workflow de release; localmente, usa um valor padrão)

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0.0"
#endif

#define MyAppName "Youtube Downloader Pro"
#define MyAppPublisher "Wanderson Saraiva Torres"
#define MyAppURL "https://github.com/wandersonstt/Youtube-Downloader-Pro"
#define MyAppExeName "YoutubeDownloaderCS.exe"

[Setup]
AppId={{2F6B7C9E-4C8B-4B3E-9C2A-YTDLPRO0001}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
; Instala na pasta do usuário (não Program Files): o app baixa/atualiza
; ffmpeg.exe e yt-dlp.exe na própria pasta de instalação, o que exigiria
; privilégio de administrador se fosse instalado em Program Files.
DefaultDirName={localappdata}\Programs\{#MyAppName}
PrivilegesRequired=lowest
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\installer-output
OutputBaseFilename=YoutubeDownloaderPro-Setup
SetupIconFile=..\youtube.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar um ícone na Área de Trabalho"; GroupDescription: "Ícones adicionais:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Executar {#MyAppName}"; Flags: nowait postinstall skipifsilent
