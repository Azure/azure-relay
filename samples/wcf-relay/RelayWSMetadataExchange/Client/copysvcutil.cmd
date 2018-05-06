for /f "delims=" %%i in ('where svcutil.exe') do set svcutil_loc=%%i
copy "%svcutil_loc%" .
for /f "delims=" %%i in ('dir /b/s ..\packages\Microsoft.ServiceBus.dll') do set sbasm_loc=%%i
copy "%sbasm_loc%" .
