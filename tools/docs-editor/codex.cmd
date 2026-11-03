@echo off
rem R'th Codex: double-click to open the Codex (the desktop app). For the browser version: npm start.
cd /d "%~dp0"
if not exist node_modules call npm install
call npm run app
