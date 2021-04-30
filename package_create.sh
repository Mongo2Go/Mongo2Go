#!/bin/bash

# just to be sure
#git clean -fdx

echo
echo "*** Your dotnet version:"
dotnet --version

echo
echo "*** Creating package:"
dotnet pack --configuration Release src/Mongo2Go/Mongo2Go.csproj -p:ContinuousIntegrationBuild=true

echo
echo "*** Package content:"
zipinfo src/Mongo2Go/bin/Release/Mongo2Go.*.nupkg