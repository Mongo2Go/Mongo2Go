@echo off

del *.nupkg
dotnet pack -o ..\..\ /p:NuspecFile=..\..\Mongo2Go.nuspec

pause