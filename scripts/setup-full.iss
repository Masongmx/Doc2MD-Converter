; Doc2MD Converter 完整版安装脚本（Inno Setup 7）
; 包含：主程序 + LibreOffice + OCRmyPDF 完整版 + Tesseract

#define MyAppName "Doc2MD Converter"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Doc2MD Project"
#define MyAppExeName "Doc2MD.Converter.exe"

[Setup]
AppId={{8F1A2B3C-4D5E-4F60-9A7B-8C9D0E1F2A3B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Doc2MD-Converter
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\publish\installer
OutputBaseFilename=Doc2MD-Converter-1.0.0-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
SetupIconFile=..\src\Doc2MD.Converter.App\Assets\DocProcessorIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion=1.0.0
VersionInfoDescription=Doc2MD Converter 完整版安装程序
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; 主程序
Source: "..\publish\gui\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; LibreOffice（旧式 Office 转换引擎）
Source: "..\tools\LibreOffice\*"; DestDir: "{app}\tools\LibreOffice"; Flags: ignoreversion recursesubdirs createallsubdirs
; OCRmyPDF 完整版（扫描 PDF OCR）
Source: "..\tools\OCRmyPDF\*"; DestDir: "{app}\tools\OCRmyPDF"; Flags: ignoreversion recursesubdirs createallsubdirs
; Tesseract（OCR 识别引擎）
Source: "..\tools\Tesseract\*"; DestDir: "{app}\tools\Tesseract"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent