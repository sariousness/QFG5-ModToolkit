#ifndef AppVersion
  #define AppVersion "1.2.0"
#endif

#ifndef AppSource
  #define AppSource "..\dist\portable"
#endif

#define AppName "QFG5Extractor"
#define AppExeName "QFG5Extractor.exe"

[Setup]
AppId={{A1D4ECCE-3B21-4FF9-8679-A37E59271BB9}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=QFG5 Modding Toolkit
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\dist
OutputBaseFilename={#AppName}-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x86 x64
ArchitecturesInstallIn64BitMode=x64

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: postinstall nowait skipifsilent

[Code]
function HasDotNet4(RootKey: Integer): Boolean;
var
  InstallValue: Cardinal;
begin
  Result :=
    (RegQueryDWordValue(RootKey, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Install', InstallValue) and (InstallValue = 1)) or
    (RegQueryDWordValue(RootKey, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Client', 'Install', InstallValue) and (InstallValue = 1));
end;

function InitializeSetup(): Boolean;
begin
  Result := HasDotNet4(HKLM32);
  if IsWin64 then
    Result := Result or HasDotNet4(HKLM64);

  if not Result then
    MsgBox(
      'QFG5Extractor requires the .NET Framework 4.0 or later runtime. Install it from Microsoft, then run this setup again: https://dotnet.microsoft.com/en-us/download/dotnet-framework/net40',
      mbError, MB_OK);
end;
