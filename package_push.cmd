@echo off

IF %1.==. GOTO NoParameter

nuget.exe setApiKey %1
nuget push *.nupkg -Timeout 1800 -Source https://www.nuget.org/api/v2/package
goto end

:NoParameter
echo *** ERROR!
echo *** Please privide the API key from nuget.org like this: package_push [key]

:end