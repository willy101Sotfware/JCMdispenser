@echo off
echo Building BillAcceptor WPF Application...
dotnet build
if %ERRORLEVEL% EQU 0 (
    echo Build successful! Starting application...
    dotnet run
) else (
    echo Build failed!
    pause
)
