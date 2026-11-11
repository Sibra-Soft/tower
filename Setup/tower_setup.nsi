Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"

!ifndef PRODUCT_VERSION
  !error "Build with Setup/Build.ps1 to supply version and verified payload."
!endif
!ifndef FILE_VERSION
  !define FILE_VERSION "1.0.0.0"
!endif
!ifndef PAYLOAD_ROOT
  !define PAYLOAD_ROOT "${__FILEDIR__}\publish"
!endif
!ifndef OUTPUT_FILE
  !define OUTPUT_FILE "${__FILEDIR__}\output\Tower-${PRODUCT_VERSION}-Windows-x64-Setup.exe"
!endif

!ifdef INSTALLER_TEST
  !define PRODUCT_KEY "Software\Sibra-Soft\Tower-InstallerSmoke"
  !define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\SibraSoft.Tower.InstallerSmoke"
  !define SHORTCUT_FOLDER "Tower Installer Smoke"
!else
  !define PRODUCT_KEY "Software\Sibra-Soft\Tower"
  !define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\SibraSoft.Tower"
  !define SHORTCUT_FOLDER "Tower"
!endif
Name "Tower"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\Sibra-Soft\Tower"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
ShowInstDetails show
ShowUninstDetails show
BrandingText "Sibra-Soft"
VIProductVersion "${FILE_VERSION}"
VIAddVersionKey /LANG=1033 "ProductName" "Tower"
VIAddVersionKey /LANG=1033 "CompanyName" "Sibra-Soft"
VIAddVersionKey /LANG=1033 "FileDescription" "Tower Setup"
VIAddVersionKey /LANG=1033 "FileVersion" "${PRODUCT_VERSION}"
VIAddVersionKey /LANG=1033 "ProductVersion" "${PRODUCT_VERSION}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Sibra-Soft"

!define MUI_ABORTWARNING
!define MUI_ICON "${__FILEDIR__}\..\Resources\MAINICON.ico"
!define MUI_UNICON "${__FILEDIR__}\..\Resources\MAINICON.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${__FILEDIR__}\wizard.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "${__FILEDIR__}\wizard.bmp"
!define MUI_WELCOMEPAGE_TEXT "Welcome to Tower Setup.$\r$\n$\r$\nTower is an air traffic control simulation. This installation includes Canyon Field; additional airports can be installed from airport packages.$\r$\n$\r$\nClose Tower before continuing."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\Tower.exe"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  SetShellVarContext current
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "Tower requires 64-bit Windows."
    SetErrorLevel 1
    Quit
  ${EndIf}
  SetRegView 64
  ReadRegStr $0 HKCU "${PRODUCT_KEY}" "InstallDir"
  ${If} $0 != ""
    ${If} $INSTDIR == "$LOCALAPPDATA\Programs\Sibra-Soft\Tower"
      StrCpy $INSTDIR $0
    ${EndIf}
  ${EndIf}
FunctionEnd

!macro CheckRunning PREFIX
Function ${PREFIX}CheckRunning
  IfFileExists "$INSTDIR\Tower.exe" 0 done
  ClearErrors
  FileOpen $0 "$INSTDIR\Tower.exe" a
  IfErrors running
  FileClose $0
  Goto done
running:
  MessageBox MB_ICONSTOP "Close Tower before installing, updating or uninstalling it."
  SetErrorLevel 1
  Abort
done:
FunctionEnd
!macroend
!insertmacro CheckRunning ""
!insertmacro CheckRunning "un."

Section "Tower and Canyon Field" SEC_TOWER
  SectionIn RO
  Call CheckRunning
  ; Reuse the previous installer's exact ownership list to remove obsolete runtime files.
  ReadRegStr $0 HKCU "${PRODUCT_KEY}" "InstallDir"
  ${If} $0 == $INSTDIR
    IfFileExists "$INSTDIR\Uninstall.exe" 0 upgrade_done
    ExecWait '$\"$INSTDIR\Uninstall.exe$\" /S _?=$INSTDIR' $1
    ${If} $1 != 0
      MessageBox MB_ICONSTOP "The previous Tower installation could not be updated."
      Abort
    ${EndIf}
  ${EndIf}
upgrade_done:
  SetOverwrite on
  !include "${__FILEDIR__}\generated\install-files.nsh"
  ; Mutable settings are installed only once. User layouts, profiles and add-ons are retained.
  IfFileExists "$INSTDIR\settings.ini" settings_done
  SetOutPath "$INSTDIR"
  File "${PAYLOAD_ROOT}\settings.ini"
settings_done:
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "${PRODUCT_KEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "Tower"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "Sibra-Soft"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\Tower.exe"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "Start Menu shortcuts" SEC_START
  CreateDirectory "$SMPROGRAMS\${SHORTCUT_FOLDER}"
  CreateShortCut "$SMPROGRAMS\${SHORTCUT_FOLDER}\Tower.lnk" "$INSTDIR\Tower.exe"
  CreateShortCut "$SMPROGRAMS\${SHORTCUT_FOLDER}\Uninstall Tower.lnk" "$INSTDIR\Uninstall.exe"
SectionEnd

Section /o "Desktop shortcut" SEC_DESKTOP
  CreateShortCut "$DESKTOP\${SHORTCUT_FOLDER}.lnk" "$INSTDIR\Tower.exe"
SectionEnd

Function un.onInit
  SetShellVarContext current
  SetRegView 64
  Call un.CheckRunning
FunctionEnd

Section "Uninstall"
  ; Generated exact file list: never recursively delete the installation directory.
  !include "${__FILEDIR__}\generated\uninstall-files.nsh"
  Delete "$SMPROGRAMS\${SHORTCUT_FOLDER}\Tower.lnk"
  Delete "$SMPROGRAMS\${SHORTCUT_FOLDER}\Uninstall Tower.lnk"
  RMDir "$SMPROGRAMS\${SHORTCUT_FOLDER}"
  Delete "$DESKTOP\${SHORTCUT_FOLDER}.lnk"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  DeleteRegKey HKCU "${PRODUCT_KEY}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
SectionEnd
