@echo off

set BASE_DIR=C:\dima\tradelink\backtest\_SMA_Crossing_Response_expand-me.expanded\

setlocal enabledelayedexpansion
FOR %%X in ("*4_of_9.json") DO (
	echo processing %%~nxX

	echo "command is:  --kadina-mode=script --kadina-config=%BASE_DIR%kadina.json --response-config=%BASE_DIR%\%%~nxX"

	C:\dima\tradelink\tradelink.afterlife\Kadina\bin\Debug\kadina.exe --kadina-mode=script --kadina-config=%BASE_DIR%kadina.json --response-config=%BASE_DIR%\%%~nxX
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
