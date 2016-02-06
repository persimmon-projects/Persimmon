process
{
  $exitCode = 0

  & .\src\Persimmon.Console\bin\$env:configuration\Persimmon.Console.exe --format:xml --output:results.xml .\tests\Persimmon.Tests\bin\$env:configuration\Persimmon.Tests.dll .\tests\Persimmon.Script.Tests\bin\$env:configuration\Persimmon.Script.Tests.dll

  if (-not $?)
  {
    $exitCode = $LASTEXITCODE
  }

#  $url = "https://ci.appveyor.com/api/testresults/junit/$($env:APPVEYOR_JOB_ID)"
#  $file = '.\results.xml'
#  $wc = New-Object 'System.Net.WebClient'
#  $wc.UploadFile($url, (Resolve-Path $file))

  exit $exitCode
}
