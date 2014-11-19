C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe Persimmon.sln /property:Configuration=Release /property:VisualStudioVersion=12.0 /target:rebuild

.\.nuget\nuget.exe pack .\Persimmon\Persimmon.fsproj -Symbols -Properties VisualStudioVersion=12.0
.\.nuget\nuget.exe pack .\Persimmon.Runner\Persimmon.Runner.fsproj -Symbols -Properties VisualStudioVersion=12.0

$fsproj = [xml] (cat Persimmon.Console\Persimmon.Console.fsproj)

$id = ([string] $fsproj.Project.PropertyGroup.AssemblyName).Trim()

$asmInfo = cat Persimmon.Console\AssemblyInfo.fs

[void] (($asmInfo | ?{ $_.Contains('AssemblyTitle') }) -match '"([^"]+)"')
$title = $Matches[1]

[void] (($asmInfo | ?{ $_.Contains('AssemblyInformationalVersion') }) -match '"([^"]+)"')
$version = $Matches[1]

$template = cat Persimmon.Console\Persimmon.Console.nuspec.template
$template = $template.Replace('$id$', $id).Replace('$title$', $title).Replace('$version$', $version)
$template | Out-File -Encoding UTF8 .\Persimmon.Console\Persimmon.Console.nuspec

.\.nuget\nuget.exe pack .\Persimmon.Console\Persimmon.Console.nuspec

if(Test-Path "nuget-packages")
{
  rm nuget-packages -Recurse -Force
}
mkdir nuget-packages

mv .\*.nupkg nuget-packages

ls .\nuget-packages\*.nupkg | %{
  echo "..\.nuget\nuget.exe push $_" >> .\nuget-packages\Push-All.ps1
}
