@echo off
REM ------------------------------------------------------------------
REM  publish-win.cmd
REM  Bygger cliCode2Web som self-contained single-file exe og placerer
REM  den i %USERPROFILE%\cli\. Kopierer ogsaa references-shipped\
REM  ind ved siden af binaeren, saa de foelger med distributionen.
REM
REM  Forventer at du har koert sync-references.cmd foerst, saa
REM  references-shipped\ er opdateret fra dine lokale referencer.
REM
REM  Brug : publish-win.cmd  [Runtime]  [Configuration]
REM    fx : publish-win.cmd
REM    fx : publish-win.cmd win-x64 Release
REM ------------------------------------------------------------------

setlocal enabledelayedexpansion

set "RUNTIME=%~1"
if "%RUNTIME%"=="" set "RUNTIME=win-x64"

set "CONFIG=%~2"
if "%CONFIG%"=="" set "CONFIG=Release"

REM Mappen hvor dette script ligger (repo-roden).
set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

REM Find .csproj (helst cliCode2Web.csproj).
set "PROJECT="
for /r "%SCRIPT_DIR%" %%F in (cliCode2Web.csproj) do (
    if not defined PROJECT set "PROJECT=%%F"
)
if not defined PROJECT (
    for /r "%SCRIPT_DIR%" %%F in (*.csproj) do (
        if not defined PROJECT set "PROJECT=%%F"
    )
)
if not defined PROJECT (
    echo FEJL: Fandt ingen .csproj-filer under %SCRIPT_DIR%
    exit /b 1
)

set "DEST=%USERPROFILE%\cli"

echo Project      : %PROJECT%
echo Runtime      : %RUNTIME%
echo Configuration: %CONFIG%
echo Output       : %DEST%
echo.

if not exist "%DEST%" mkdir "%DEST%"

dotnet publish "%PROJECT%" ^
    -c %CONFIG% ^
    -r %RUNTIME% ^
    -p:PublishSingleFile=true ^
    --self-contained true ^
    -o "%DEST%"

if errorlevel 1 (
    echo FEJL: dotnet publish fejlede med exit code %errorlevel%
    exit /b %errorlevel%
)

REM Shipped references: kopier fra repoets references-shipped\ ind
REM ved siden af binaeren. Saadan er publish reproducerbar uanset
REM hvilken maskine du bygger fra.
set "SHIPPED_SRC=%SCRIPT_DIR%\references-shipped"
if exist "%SHIPPED_SRC%" (
    set "SHIPPED_DST=%DEST%\references"
    echo.
    echo Kopierer shipped references:
    echo   fra : %SHIPPED_SRC%
    echo   til : !SHIPPED_DST!

    if exist "!SHIPPED_DST!" rmdir /s /q "!SHIPPED_DST!"
    mkdir "!SHIPPED_DST!"

    set /a COUNT=0
    for %%F in ("%SHIPPED_SRC%\*.txt") do (
        copy /y "%%F" "!SHIPPED_DST!\" >nul
        set /a COUNT+=1
    )
    echo   (!COUNT! fil(er) kopieret^)
) else (
    echo.
    echo INFO: references-shipped\ blev ikke fundet i repoet.
    echo       Koer sync-references.cmd foerst hvis du vil have
    echo       referencer med i builden.
)

echo.
echo   Publish complete.
echo   Files are in: %DEST%

endlocal
