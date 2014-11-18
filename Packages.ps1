C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe Persimmon.sln /property:Configuration=Release /property:VisualStudioVersion=12.0 /target:rebuild

.\.nuget\nuget.exe pack .\Persimmon\Persimmon.fsproj -Build -Symbols -Properties VisualStudioVersion=12.0
.\.nuget\nuget.exe pack .\Persimmon.Runner\Persimmon.Runner.fsproj -Build -Symbols -Properties VisualStudioVersion=12.0
.\.nuget\nuget.exe pack .\Persimmon.Console\Persimmon.Console.fsproj -Properties VisualStudioVersion=12.0

if(Test-Path "nuget-packages")
{
  rm nuget-packages\*
}
else
{
  mkdir nuget-packages
}

mv .\*.nupkg nuget-packages

ls .\nuget-packages\*.nupkg | %{
  echo "..\.nuget\nuget.exe push $_" >> .\nuget-packages\Push-All.ps1
}
