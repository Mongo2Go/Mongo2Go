@echo off

del *.nupkg
nuget pack Mongo2Go.nuspec

pause