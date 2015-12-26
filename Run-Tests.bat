@echo off

IF "%CONFIGURATION%" EQU "" SET CONFIGURATION=Debug

IF not exist packages\Persimmon.Console (
  echo installing Persimmon.Console...
  .\.nuget\NuGet.exe Install Persimmon.Console -ExcludeVersion -Prerelease -OutputDirectory packages
  echo ======================================================================
)

echo CONFIGURATION: %CONFIGURATION%.

echo run Persimmon.Tests.
.\packages\Persimmon.Console\tools\Persimmon.Console.exe .\Persimmon.Tests\bin\%CONFIGURATION%/Persimmon.Tests.dll
echo ======================================================================

echo run Persimmon.Script.Tests.
.\packages\Persimmon.Console\tools\Persimmon.Console.exe .\Persimmon.Script.Tests\bin\%CONFIGURATION%/Persimmon.Script.Tests.dll
