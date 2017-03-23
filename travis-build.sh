#!/bin/bash
set -e

dotnet restore
dotnet build -c Release