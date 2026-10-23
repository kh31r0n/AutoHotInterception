; Inno Setup script for AutoHotInterception + the AHI Configurator.
; Build with Installer\build.ps1 (or: ISCC.exe /DAppVersion=x.y.z AutoHotInterception.iss after a Release build).
; Requires Inno Setup 6.5 or later (download + extractarchive flags).
;
; The Interception driver and library are NOT bundled: they are downloaded from the official GitHub release at
; install time (pinned version + SHA-256), after the user accepts the Interception licence terms.

#ifndef AppVersion
  #define AppVersion "0.10.0"
#endif
#define AppName "AutoHotInterception"
#define Root ".."
#define ConfiguratorBin Root + "\C#\AHIConfigurator\bin\Release"
#define CoreBin Root + "\C#\AutoHotInterception\bin\Release"

#define InterceptionUrl "https://github.com/oblitum/Interception/releases/download/v1.0.1/Interception.zip"
#define InterceptionSha256 "ad038963d6413055765128b0b931f6e765147c9916dba79e65d872b261f9af10"
#define AhkUrl "https://github.com/AutoHotkey/AutoHotkey/releases/download/v2.0.30/AutoHotkey_2.0.30_setup.exe"
#define AhkSha256 "ecfb26b4cc90cd1e5440ed491cde486e728541d63f3a4c24159e8ddae77ab772"

[Setup]
AppId={{9DCE0421-DBB9-4774-A5AB-E0ACCE4A48BC}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=AutoHotInterception contributors
AppPublisherURL=https://github.com/evilC/AutoHotInterception
AppSupportURL=https://github.com/evilC/AutoHotInterception/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
LicenseFile={#Root}\LICENSE
OutputDir=Output
OutputBaseFilename=AHI-Setup-{#AppVersion}
PrivilegesRequired=admin
ArchitecturesAllowed=x86compatible x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ArchiveExtraction=full
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\Configurator\AHIConfigurator.exe
UninstallDisplayName={#AppName}
; Running mapping scripts lock AHK v2\Lib\*.dll; let Restart Manager close them on upgrade / uninstall
CloseApplications=yes
MinVersion=10.0

[Types]
Name: "full"; Description: "Recommended installation"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "core"; Description: "AutoHotInterception library for AutoHotkey v2, examples and Monitor"; Types: full custom; Flags: fixed
Name: "configurator"; Description: "Configurator (remap keys and mouse buttons without writing code)"; Types: full custom
; Install when missing (selected by default), or reinstall when already present (not selected by default).
; Two components each, because [Components] descriptions cannot change at runtime.
Name: "driver"; Description: "Install the Interception driver (needs a restart)"; Types: full custom; Check: not InterceptionDriverInstalled
Name: "driverreinstall"; Description: "Reinstall the Interception driver (already installed, needs a restart)"; Types: custom; Check: InterceptionDriverInstalled
Name: "autohotkey"; Description: "Download and install AutoHotkey v2"; Types: full custom; Check: not AutoHotkeyV2Installed
Name: "autohotkeyreinstall"; Description: "Download and reinstall AutoHotkey v2.0.30 (already installed)"; Types: custom; Check: AutoHotkeyV2Installed

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut for the Configurator"; Components: configurator; Flags: unchecked

[Files]
; Interception library + command line installer, downloaded and verified while preparing to install.
; Needed by every installation (AHI cannot work without interception.dll), so it is not tied to the driver component.
; AfterInstall runs the driver installer as soon as it is extracted: during file copying, so before Setup asks NeedRestart.
Source: "{#InterceptionUrl}"; DestName: "Interception.zip"; DestDir: "{app}\driver"; Hash: "{#InterceptionSha256}"; \
  ExternalSize: 389119; Flags: external download extractarchive recursesubdirs createallsubdirs ignoreversion; \
  AfterInstall: InstallDriverIfSelected

; AHK v2 library (interception.dll is copied from the extracted download, see below)
Source: "{#Root}\AHK v2\Lib\AutoHotInterception.ahk"; DestDir: "{app}\AHK v2\Lib"; Flags: ignoreversion
Source: "{#Root}\AHK v2\Lib\AHIRuntime.ahk"; DestDir: "{app}\AHK v2\Lib"; Flags: ignoreversion
Source: "{#Root}\AHK v2\Lib\CLR.ahk"; DestDir: "{app}\AHK v2\Lib"; Flags: ignoreversion
Source: "{#Root}\AHK v2\Lib\Unblocker.ps1"; DestDir: "{app}\AHK v2\Lib"; Flags: ignoreversion
Source: "{#CoreBin}\AutoHotInterception.dll"; DestDir: "{app}\AHK v2\Lib"; Flags: ignoreversion
Source: "{app}\driver\Interception\library\x86\interception.dll"; DestDir: "{app}\AHK v2\Lib\x86"; Flags: external ignoreversion
Source: "{app}\driver\Interception\library\x64\interception.dll"; DestDir: "{app}\AHK v2\Lib\x64"; Flags: external ignoreversion
; Examples and Monitor
Source: "{#Root}\AHK v2\*.ahk"; DestDir: "{app}\AHK v2"; Flags: ignoreversion

; Configurator. Only named files: bin\Release also holds a copy of interception.dll, which is not redistributed.
Source: "{#ConfiguratorBin}\AHIConfigurator.exe"; DestDir: "{app}\Configurator"; Components: configurator; Flags: ignoreversion
Source: "{#ConfiguratorBin}\AHIConfigurator.exe.config"; DestDir: "{app}\Configurator"; Components: configurator; Flags: ignoreversion
Source: "{#ConfiguratorBin}\AutoHotInterception.dll"; DestDir: "{app}\Configurator"; Components: configurator; Flags: ignoreversion
Source: "{#ConfiguratorBin}\Newtonsoft.Json.dll"; DestDir: "{app}\Configurator"; Components: configurator; Flags: ignoreversion
Source: "{app}\driver\Interception\library\x86\interception.dll"; DestDir: "{app}\Configurator\x86"; Components: configurator; Flags: external ignoreversion
Source: "{app}\driver\Interception\library\x64\interception.dll"; DestDir: "{app}\Configurator\x64"; Components: configurator; Flags: external ignoreversion

; AutoHotkey v2 installer (pinned version), run silently below
Source: "{#AhkUrl}"; DestName: "AutoHotkey_setup.exe"; DestDir: "{tmp}"; Hash: "{#AhkSha256}"; \
  ExternalSize: 3040256; Components: autohotkey autohotkeyreinstall; Flags: external download ignoreversion deleteafterinstall

Source: "{#Root}\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "{#Root}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Root}\CHANGELOG.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\AHI Configurator"; Filename: "{app}\Configurator\AHIConfigurator.exe"; Components: configurator
Name: "{group}\AHI Monitor"; Filename: "{app}\AHK v2\Monitor.ahk"; WorkingDir: "{app}\AHK v2"; Comment: "Shows input from each keyboard and mouse"
Name: "{group}\AHK v2 examples"; Filename: "{app}\AHK v2"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\AHI Configurator"; Filename: "{app}\Configurator\AHIConfigurator.exe"; Tasks: desktopicon

[Run]
Filename: "{tmp}\AutoHotkey_setup.exe"; Parameters: "/silent"; StatusMsg: "Installing AutoHotkey v2..."; \
  Components: autohotkey autohotkeyreinstall; Flags: waituntilterminated
Filename: "{app}\Configurator\AHIConfigurator.exe"; Description: "Open the Configurator"; \
  Components: configurator; Flags: postinstall nowait skipifsilent

[UninstallDelete]
Type: dirifempty; Name: "{app}\AHK v2\Lib"
Type: dirifempty; Name: "{app}\AHK v2"
Type: dirifempty; Name: "{app}"

[Messages]
FinishedRestartLabel=To finish installing the Interception driver, Windows must be restarted. Would you like to restart now?

[Code]
const
  NetFx48Release = 528040;
  InterceptionLicenseUrl = 'https://github.com/oblitum/Interception#license';

var
  InterceptionPage: TWizardPage;
  AcceptLicense: TNewCheckBox;
  DriverInstalledNow: Boolean;

function InterceptionDriverInstalled: Boolean;
begin
  { The Interception installer copies keyboard.sys / mouse.sys and registers them as the "keyboard" and "mouse" services }
  Result := FileExists(ExpandConstant('{sys}\drivers\keyboard.sys')) and
            FileExists(ExpandConstant('{sys}\drivers\mouse.sys')) and
            RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\keyboard') and
            RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\mouse');
end;

function AutoHotkeyV2Installed: Boolean;
var
  Version: String;
begin
  Result := (RegQueryStringValue(HKLM, 'SOFTWARE\AutoHotkey', 'Version', Version) or
             RegQueryStringValue(HKCU, 'SOFTWARE\AutoHotkey', 'Version', Version)) and
            (Copy(Version, 1, 2) = '2.');
end;

function NetFx48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and
            (Release >= NetFx48Release);
end;

procedure OpenLicenseLink(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExecAsOriginalUser('open', InterceptionLicenseUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure InitializeWizard;
var
  Info: TNewStaticText;
  Link: TNewStaticText;
begin
  { Interception licence page, after the AHI licence }
  InterceptionPage := CreateCustomPage(wpLicense, 'Interception driver licence',
    'AutoHotInterception is built on the Interception driver by Francisco Lopes, which has its own licence.');

  Info := TNewStaticText.Create(InterceptionPage);
  Info.Parent := InterceptionPage.Surface;
  Info.AutoSize := False;
  Info.WordWrap := True;
  Info.Width := InterceptionPage.SurfaceWidth;
  Info.Height := ScaleY(150);
  Info.Caption :=
    'Setup will download the Interception driver and library (version 1.0.1) from the official GitHub release.' + #13#10#13#10 +
    'Interception is free for non-commercial use under the GNU LGPL 3.0. ' +
    'Commercial use requires a separate licence from its author. ' +
    'The licence texts are installed with it, in the "driver\Interception\licenses" folder.' + #13#10#13#10 +
    'The driver hooks into Windows keyboard and mouse input. Installing it requires a restart.';

  Link := TNewStaticText.Create(InterceptionPage);
  Link.Parent := InterceptionPage.Surface;
  Link.Top := Info.Top + Info.Height + ScaleY(4);
  Link.Caption := 'Read about Interception''s licensing';
  Link.Cursor := crHand;
  Link.Font.Color := clBlue;
  Link.Font.Style := [fsUnderline];
  Link.OnClick := @OpenLicenseLink;

  AcceptLicense := TNewCheckBox.Create(InterceptionPage);
  AcceptLicense.Parent := InterceptionPage.Surface;
  AcceptLicense.Top := Link.Top + Link.Height + ScaleY(16);
  AcceptLicense.Width := InterceptionPage.SurfaceWidth;
  AcceptLicense.Height := ScaleY(20);
  AcceptLicense.Caption := 'I accept the Interception licence terms';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = InterceptionPage.ID) and not AcceptLicense.Checked then
  begin
    MsgBox('AutoHotInterception cannot work without Interception. Please accept its licence terms to continue.', mbError, MB_OK);
    Result := False;
  end
  else if (CurPageID = wpSelectComponents) and WizardIsComponentSelected('configurator') and not NetFx48Installed then
  begin
    Result := MsgBox('The Configurator needs .NET Framework 4.8, which was not found on this PC.' + #13#10 +
                     'You can install it later from Microsoft. Continue anyway?', mbConfirmation, MB_YESNO) = IDYES;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo,
  MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := MemoDirInfo + NewLine + NewLine + MemoComponentsInfo + NewLine + NewLine;
  if MemoTasksInfo <> '' then Result := Result + MemoTasksInfo + NewLine + NewLine;
  Result := Result + 'Downloads:' + NewLine + Space + 'Interception 1.0.1 (github.com/oblitum/Interception)' + NewLine;
  if WizardIsComponentSelected('autohotkey') or WizardIsComponentSelected('autohotkeyreinstall') then
    Result := Result + Space + 'AutoHotkey 2.0.30 (github.com/AutoHotkey/AutoHotkey)' + NewLine;
  if WizardIsComponentSelected('driverreinstall') then
    Result := Result + NewLine + 'The Interception driver will be reinstalled. Windows must be restarted afterwards.'
  else if InterceptionDriverInstalled then
    Result := Result + NewLine + 'The Interception driver is already installed and will be kept.'
  else if not WizardIsComponentSelected('driver') then
    Result := Result + NewLine + 'WARNING: the Interception driver will not be installed. AutoHotInterception will not work until it is.';
end;

function DriverInstallerExe: String;
begin
  Result := ExpandConstant('{app}\driver\Interception\command line installer\install-interception.exe');
end;

{ Runs install-interception.exe /install or /uninstall and returns what it printed (also written to the Setup log).
  Its exit code is not documented, so callers check the printed message instead. }
function RunInterceptionInstaller(const Param: String): String;
var
  ResultCode: Integer;
  Output: TExecOutput;
begin
  if ExecAndCaptureOutput(DriverInstallerExe, Param, ExtractFileDir(DriverInstallerExe), SW_HIDE,
       ewWaitUntilTerminated, ResultCode, Output) then
    Result := Trim(StringJoin(#13#10, Output.StdOut) + #13#10 + StringJoin(#13#10, Output.StdErr))
  else
    Result := 'install-interception.exe could not be started: ' + SysErrorMessage(ResultCode);
  Log('install-interception ' + Param + ' (exit code ' + IntToStr(ResultCode) + '): ' + Result);
end;

{ AfterInstall of the Interception download, so it runs right after extraction.
  install-interception.exe cannot overwrite a driver that is loaded, so a reinstall is /uninstall followed by /install
  (files of a loaded driver can be deleted; the new ones take effect after the restart). }
procedure InstallDriverIfSelected;
var
  Output: String;
  Reinstall: Boolean;
begin
  Reinstall := WizardIsComponentSelected('driverreinstall');
  if not (Reinstall or WizardIsComponentSelected('driver')) then Exit;

  if Reinstall then
  begin
    WizardForm.StatusLabel.Caption := 'Removing the current Interception driver...';
    Output := RunInterceptionInstaller('/uninstall');
    if Pos('Interception uninstalled', Output) = 0 then
    begin
      MsgBox('The current Interception driver could not be removed, so it was not reinstalled.' + #13#10 +
             'The installed driver was left as it is.' + #13#10#13#10 + Output, mbError, MB_OK);
      Exit;
    end;
    DriverInstalledNow := True; { the removal alone already needs a restart }
  end;

  WizardForm.StatusLabel.Caption := 'Installing the Interception driver...';
  Output := RunInterceptionInstaller('/install');
  if Pos('Interception successfully installed', Output) > 0 then
  begin
    DriverInstalledNow := True;
    Exit;
  end;

  { Never leave the keyboard / mouse filters registered without their driver files:
    Windows would start without a working keyboard and mouse }
  if not (FileExists(ExpandConstant('{sys}\drivers\keyboard.sys')) and FileExists(ExpandConstant('{sys}\drivers\mouse.sys'))) then
    RunInterceptionInstaller('/uninstall');

  if Reinstall then
    MsgBox('The old Interception driver was removed, but the new one could not be installed.' + #13#10 +
           'Restart Windows, then run this setup again to install the driver.' + #13#10#13#10 + Output, mbError, MB_OK)
  else
    MsgBox('The Interception driver could not be installed.' + #13#10#13#10 + Output + #13#10#13#10 +
           'You can try again later from an administrator command prompt:' + #13#10 +
           '"' + DriverInstallerExe + '" /install', mbError, MB_OK);
end;

function NeedRestart: Boolean;
begin
  Result := DriverInstalledNow;
end;

{ ---------------------------------------------------------------- Uninstall }

var
  DriverRemovedNow: Boolean;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Exe: String;
  ResultCode: Integer;
begin
  if CurUninstallStep <> usUninstall then Exit;

  Exe := ExpandConstant('{app}\driver\Interception\command line installer\install-interception.exe');
  if FileExists(Exe) and InterceptionDriverInstalled and
     (SuppressibleMsgBox('Also uninstall the Interception driver?' + #13#10#13#10 +
        'Choose No if other programs use it. Removing it requires a restart.',
        mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES) then
  begin
    if Exec(Exe, '/uninstall', ExtractFileDir(Exe), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      DriverRemovedNow := True;
  end;

  { The Configurator's "start at sign-in" entry points into the folder being removed }
  RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'AutoHotInterception');

  if DirExists(ExpandConstant('{userappdata}\AutoHotInterception')) and
     (SuppressibleMsgBox('Also delete your Configurator settings and mappings?', mbConfirmation,
        MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES) then
    DelTree(ExpandConstant('{userappdata}\AutoHotInterception'), True, True, True);
end;

function UninstallNeedRestart: Boolean;
begin
  Result := DriverRemovedNow;
end;
