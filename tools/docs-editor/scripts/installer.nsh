; The R'th Codex's uninstaller, beyond electron-builder's own (package.json "build.nsis"): uninstalling
; leaves nothing behind. electron-builder removes the program and, with deleteAppDataOnUninstall, the
; Codex's own folder (%APPDATA%\rth-docs-editor: settings, sign-in, AI conversations, and the docs it
; fetched into Docs\). This adds the updates it downloaded
; (%LOCALAPPDATA%\rth-docs-editor-updater), the rth-codex:// link the Codex registers for itself, and a copy of the
; docs that an older Codex fetched into a folder the person chose (named in fetched-folder.txt, and
; removed only if it carries the Codex's mark, .git\codex-fetched). None of it runs for an update.

!macro customUnInstall
  ${ifNot} ${isUpdated}
    DeleteRegKey HKCU "Software\Classes\rth-codex"
    RMDir /r "$LOCALAPPDATA\${APP_FILENAME}-updater" ; updates it downloaded
    ClearErrors
    FileOpen $0 "$APPDATA\${APP_FILENAME}\fetched-folder.txt" r
    ${ifNot} ${Errors}
      FileRead $0 $1
      FileClose $0
      ${if} $1 != ""
        ${if} ${FileExists} "$1\.git\codex-fetched"
          RMDir /r "$1"
        ${endif}
      ${endif}
    ${endif}
  ${endif}
!macroend
