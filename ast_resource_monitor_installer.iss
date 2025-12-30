[Setup]
AppName=AST Resource Monitor
AppVersion=1.0
DefaultDirName={pf}\AST Resource Monitor
DefaultGroupName=AST Resource Monitor
; This ensures the installer runs as Administrator to register the service
PrivilegesRequired=admin
OutputDir=.
OutputBaseFilename=ASTMonitorSetup
Compression=lzma
SolidCompression=yes

[Files]
; Point this to your published folder
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
; Install and Start the service
Filename: "{sys}\sc.exe"; Parameters: "create ASTMonitor binPath= ""{app}\AST-Resource-Monitor.exe"" start= auto"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start ASTMonitor"; Flags: runhidden

[UninstallRun]
; Stop and Delete the service before removing files
Filename: "{sys}\sc.exe"; Parameters: "stop ASTMonitor"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete ASTMonitor"; Flags: runhidden