###########################################################
# Start - Initialization - Invocation, Logging etc
###########################################################
$VerbosePreference = "SilentlyContinue"
$ErrorActionPreference = "Stop"

$scriptPath = $MyInvocation.MyCommand.Path
$scriptDir = Split-Path $scriptPath

& "$scriptDir\..\..\init.ps1"
if(-not $?)
{
    throw "Initialization failure."
    exit -9999
}
###########################################################
# End - Initialization - Invocation, Logging etc
###########################################################

$nugetExe = Join-Path $scriptDir "..\..\..\tools\nuget\nuget.exe"

# Download nuget.exe if it is not present
if(-not (Test-Path $nugetExe))
{
    $nugetDir = Split-Path $nugetExe
    if(-not (Test-Path $nugetDir)) { New-Item -ItemType Directory -Path $nugetDir -Force | Out-Null }
    Write-InfoLog "Downloading nuget.exe..." (Get-ScriptName) (Get-ScriptLineNumber)
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nugetExe
}

$install = & "$nugetExe" install WindowsAzure.ServiceBus -version 3.0 -OutputDirectory "$scriptDir\..\..\..\packages"

$sbNuget = (gci "$scriptDir\..\..\..\packages\WindowsAzure.ServiceBus.*")[0].FullName

$sbDll = Join-Path $sbNuget "lib\net45-full\Microsoft.ServiceBus.dll"

if(-not (Test-Path $sbDll))
{
    throw "ERROR: $sbDll not found. Please make sure you have WindowsAzure.ServiceBus nuget package available."
}

return $sbDll