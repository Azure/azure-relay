$scriptPath = $MyInvocation.MyCommand.Path
$scriptDir = Split-Path $scriptPath

Remove-Module Logging-ServiceBusRelaySamples -ErrorAction SilentlyContinue
Import-Module "$scriptDir\Logging-ServiceBusRelaySamples.psm1" -Force

Write-InfoLog "An info message." (Get-ScriptName) (Get-ScriptLineNumber)
Write-SpecialLog "A special message." (Get-ScriptName) (Get-ScriptLineNumber)

try
{
    throw "A warning"
}
catch
{
    Write-WarnLog "A warning has been raised." (Get-ScriptName) (Get-ScriptLineNumber) $_
}

try
{
    throw "An error"
}
catch
{
    Write-ErrorLog "An error has occurred." (Get-ScriptName) (Get-ScriptLineNumber) $_
}

Remove-Module Logging-ServiceBusRelaySamples
#Remove-Item "run" -Recurse -Force -ErrorAction SilentlyContinue