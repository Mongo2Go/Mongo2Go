#!/bin/bash

# for now machine spec does not have a runner that is supported with .net core 1.0, we would need to move up to core 1.1 for the runner to work it seems.
# How to do it: https://github.com/machine/machine.specifications/wiki/.NET-Core-(.NET-CLI)

# dotnet test src/Mongo2GoTests/Mongo2GoTests.csproj