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

$nugetExe = "nuget.exe"
$install = & "$nugetExe" install WindowsAzure.ServiceBus -OutputDirectory "$scriptDir\..\..\..\packages"

$sbNuget = (gci "$scriptDir\..\..\..\packages\WindowsAzure.ServiceBus.*")[0].FullName

$sbDll = Join-Path $sbNuget "lib\net46\Microsoft.ServiceBus.dll"

if(-not (Test-Path $sbDll))
{
    throw "ERROR: $sbDll not found. Please make sure you have WindowsAzure.ServiceBus nuget package available."
}

return $sbDll