@echo off

del *.nupkg
del /s /q src\Mongo2Go\bin\*

dotnet build -c Release
nuget pack Mongo2Go.nuspec