@echo off

setlocal enabledelayedexpansion
FOR %%X in ("*9.json") DO (
	echo processing %%~nxX
	C:\tradelink.afterlife\Kadina\bin\Debug\kadina.exe --kadina-mode=script --kadina-config=C:\dima\tradelink\backtest\kadina.json --response-config=%%~nxX
	if %ERRORLEVEL% NEQ 0 goto error
	echo "")
endlocal

rem "IF" - Conditionally perform a command:
rem http://ss64.com/nt/if.html
rem

goto success

:error
echo "error during batch processing files"
echo "got error code:"
echo %ERRORLEVEL%
exit 1

:success
echo "batch successfully complete"
exit 0
