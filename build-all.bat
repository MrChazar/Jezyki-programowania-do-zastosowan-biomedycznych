@echo off
setlocal EnableExtensions

cd /d "%~dp0"

echo ========================================
echo HeartRate - build wszystkich aplikacji
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

if not exist "%ANDROID_HOME%\platform-tools\adb.exe" (
    echo [BLAD] Nie znaleziono Android SDK w: %ANDROID_HOME%
    exit /b 1
)

echo.
echo [1/3] Budowanie ASP.NET Core Web API...
dotnet restore src\backend\HeartRate.slnx
if errorlevel 1 goto :build_failed

dotnet build src\backend\HeartRate.slnx -c Release --no-restore
if errorlevel 1 goto :build_failed

echo.
echo [2/3] Budowanie Angular...
pushd src\web
if errorlevel 1 goto :build_failed

call npm install
if errorlevel 1 goto :web_build_failed

call npm run build
if errorlevel 1 goto :web_build_failed

popd

echo.
echo [3/3] Budowanie .NET MAUI dla Androida...
dotnet restore src\mobile\HeartRate.Mobile\HeartRate.Mobile.csproj
if errorlevel 1 goto :build_failed

dotnet build src\mobile\HeartRate.Mobile\HeartRate.Mobile.csproj -c Release -f net11.0-android --no-restore -p:PublishReadyToRun=false
if errorlevel 1 goto :build_failed

echo.
echo ==========================================
echo Wszystkie aplikacje zbudowano poprawnie.
echo API:   src\backend\src\HeartRate.Api\bin\Release\net11.0
echo Angular: src\web\dist
echo MAUI:  src\mobile\HeartRate.Mobile\bin\Release\net11.0-android
echo ========================================
exit /b 0

:web_build_failed
popd
goto :build_failed

:build_failed
echo.
echo [BLAD] Build zostal przerwany. Sprawdz komunikat powyzej.
exit /b 1
