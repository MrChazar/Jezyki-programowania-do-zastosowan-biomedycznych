@echo off
setlocal EnableExtensions EnableDelayedExpansion

cd /d "%~dp0"
set "PROJECT_ROOT=%CD%"

echo ========================================
echo HeartRate - uruchamianie developmentu
echo ========================================

where dotnet >nul 2>&1
if errorlevel 1 (
    if exist "C:\Program Files\dotnet\dotnet.exe" (
        set "PATH=C:\Program Files\dotnet;%PATH%"
    ) else (
        echo [BLAD] Nie znaleziono .NET SDK.
        exit /b 1
    )
)

where npm >nul 2>&1
if errorlevel 1 (
    echo [BLAD] Nie znaleziono npm. Zainstaluj Node.js i otworz nowy terminal.
    exit /b 1
)

if not defined ANDROID_HOME set "ANDROID_HOME=%LOCALAPPDATA%\Android\Sdk"
if not defined JAVA_HOME set "JAVA_HOME=%LOCALAPPDATA%\Android\Jdk"

sc query MongoDB 2>nul | findstr /I "RUNNING" >nul
if errorlevel 1 (
    echo [INFO] Lokalna usluga MongoDB nie dziala. Proba uruchomienia...
    net start MongoDB >nul 2>&1
    if errorlevel 1 (
        echo [BLAD] Nie mozna uruchomic uslugi MongoDB.
        echo        Uruchom PowerShell jako administrator i wykonaj: Start-Service MongoDB
        exit /b 1
    )
)
echo [OK] MongoDB dziala.

set "ANDROID_RUN_TARGET="
if not exist "%ANDROID_HOME%\platform-tools\adb.exe" (
    echo [UWAGA] Nie znaleziono adb.exe w: %ANDROID_HOME%\platform-tools
) else (
    for /f "skip=1 tokens=1,2" %%D in ('"%ANDROID_HOME%\platform-tools\adb.exe" devices 2^>nul') do (
        if /I "%%E"=="device" if not defined ANDROID_RUN_TARGET set "ANDROID_RUN_TARGET=%%D"
    )
)

if defined ANDROID_RUN_TARGET (
    echo [OK] Wykryto uruchomione urzadzenie Android: !ANDROID_RUN_TARGET!
) else if exist "%ANDROID_HOME%\emulator\emulator.exe" (
    for /f "delims=" %%A in ('"%ANDROID_HOME%\emulator\emulator.exe" -list-avds 2^>nul') do (
        if not defined ANDROID_RUN_TARGET set "ANDROID_RUN_TARGET=%%A"
    )
    if defined ANDROID_RUN_TARGET (
        echo [INFO] Emulator nie dziala. .NET uruchomi AVD: !ANDROID_RUN_TARGET!
    ) else (
        echo [UWAGA] Brak skonfigurowanego emulatora Android.
        echo         Utworz go w Android Studio: Tools ^> Device Manager.
    )
) else (
    echo [UWAGA] Nie znaleziono emulator.exe ani podlaczonego telefonu.
)

if /I "%~1"=="--check" (
    echo [OK] Kontrola srodowiska zakonczona. Nie uruchomiono aplikacji.
    exit /b 0
)

echo [INFO] Sprawdzanie zaleznosci Angular...
pushd "%PROJECT_ROOT%\src\web"
call npm install
if errorlevel 1 (
    popd
    echo [BLAD] Nie udalo sie zainstalowac zaleznosci Angular.
    exit /b 1
)
popd

echo [1/3] Uruchamianie Web API na http://localhost:5000 ...
start "HeartRate API" /D "%PROJECT_ROOT%" cmd.exe /d /k dotnet watch --project src\backend\src\HeartRate.Api\HeartRate.Api.csproj

echo [2/3] Uruchamianie Angular na http://localhost:4200 ...
start "HeartRate Angular" /D "%PROJECT_ROOT%\src\web" cmd.exe /d /k npm run dev

echo [3/3] Uruchamianie .NET MAUI ...
if defined ANDROID_RUN_TARGET (
    if exist "%PROJECT_ROOT%\src\mobile\HeartRate.Mobile\obj\Debug\net11.0-android\adb.props" (
        del /q "%PROJECT_ROOT%\src\mobile\HeartRate.Mobile\obj\Debug\net11.0-android\adb.props"
    )
    start "HeartRate MAUI" /D "%PROJECT_ROOT%" cmd.exe /d /k dotnet watch --project src\mobile\HeartRate.Mobile\HeartRate.Mobile.csproj -f net11.0-android --device !ANDROID_RUN_TARGET!
) else (
    echo [POMINIETO] MAUI wymaga emulatora lub telefonu z wlaczonym debugowaniem USB.
)

echo.
echo Gotowe. Kazda aplikacja dziala w osobnym oknie terminala.
echo Zamkniecie wybranego okna zatrzyma tylko odpowiadajaca mu aplikacje.

endlocal
