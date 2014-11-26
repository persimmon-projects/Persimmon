$projectName = '.\docs\tools\Persimmon.Docs.proj'

$project = cat $projectName
$project = $project.Replace('generate.fsx', 'generate.ja.fsx')

$projectNameJa = '.\docs\tools\Persimmon.Docs.ja.proj'
$project | Out-File -Encoding UTF8 $projectNameJa

C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe $projectNameJa /property:Configuration=Release /property:VisualStudioVersion=12.0 /target:rebuild

rm $projectNameJa

C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe $projectName /property:Configuration=Release /property:VisualStudioVersion=12.0 /target:build

