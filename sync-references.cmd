@echo off
REM ------------------------------------------------------------------
REM  sync-references.cmd
REM  Synkroniserer dine lokale referencer fra
REM      %USERPROFILE%\Documents\Code2Web\references\
REM  ind i repo'ets
REM      references-shipped\
REM  saa de kan committes og foelger med koden.
REM
REM  Brug : sync-references.cmd
REM
REM  Note : virker uden PowerShell Execution Policy-vrov - bare en
REM         almindelig .cmd-fil.
REM ------------------------------------------------------------------

setlocal

REM Mappen hvor dette script ligger (repo-roden).
set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

set "LOCAL_REFS=%USERPROFILE%\Documents\Code2Web\references"
set "REPO_REFS=%SCRIPT_DIR%\references-shipped"

if not exist "%LOCAL_REFS%" (
    echo FEJL: Ingen lokale referencer fundet i:
    echo   %LOCAL_REFS%
    exit /b 1
)

echo Synkroniserer referencer:
echo   fra : %LOCAL_REFS%
echo   til : %REPO_REFS%
echo.

REM Genskab repo-mappen fra bunden, saa slettede/omdoebte referencer
REM ogsaa forsvinder fra repoet (ellers samles vraggods over tid).
if exist "%REPO_REFS%" rmdir /s /q "%REPO_REFS%"
mkdir "%REPO_REFS%"

REM Kopier kun .txt-filer (referencer + mapping); ignorer andet skidt.
set /a COUNT=0
for %%F in ("%LOCAL_REFS%\*.txt") do (
    copy /y "%%F" "%REPO_REFS%\" >nul
    set /a COUNT+=1
)

echo %COUNT% fil(er) kopieret.
echo.
echo Husk at committe aendringerne hvis du er tilfreds:
echo   git add references-shipped/
echo   git commit -m "Update shipped references"

endlocal
