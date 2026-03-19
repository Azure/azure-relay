[CmdletBinding(PositionalBinding=$True)]
Param(
    [parameter(Mandatory=$true)]
    [string]$configFile
)

###########################################################
# Start - Initialization - Invocation, Logging etc
###########################################################
$VerbosePreference = "SilentlyContinue"
$ErrorActionPreference = "Stop"

$scriptPath = $MyInvocation.MyCommand.Path
$scriptDir = Split-Path $scriptPath

& "$scriptDir\..\init.ps1"
if(-not $?)
{
    throw "Initialization failure."
    exit -9999
}
###########################################################
# End - Initialization - Invocation, Logging etc
###########################################################

###########################################################
# Create Run Configuration
###########################################################

Write-SpecialLog "Step 0: Creating Run Configuration" (Get-ScriptName) (Get-ScriptLineNumber)

$config = @{
    SERVICEBUS_NAMESPACE = "relaySample" + [System.DateTime]::Now.ToString("yyMMddHHmmss");
    AZURE_RESOURCE_GROUP = "relaySampleRG" + [System.DateTime]::Now.ToString("yyMMddHHmmss");
    AZURE_TENANT_ID="";
    AZURE_LOCATION="North Europe";
}

if(-not (Test-Path $configFile))
{
    Write-InfoLog "Creating a new run configuration at $configFile" (Get-ScriptName) (Get-ScriptLineNumber)
    &$scriptDir\ReplaceStringInFile.ps1 "$scriptDir\configurations.properties.template" $configFile $config
}
else
{
    Write-InfoLog "An existing run configuration was found at $configFile, just updating newer entries." (Get-ScriptName) (Get-ScriptLineNumber)
    &$scriptDir\ReplaceStringInFile.ps1 $configFile $configFile $config

    # Append any new config keys that don't exist in the file yet
    $existingConfig = & "$scriptDir\ReadConfig.ps1" $configFile
    $content = Get-Content $configFile
    foreach($key in $config.Keys)
    {
        if(-not $existingConfig.ContainsKey($key))
        {
            Write-InfoLog "Adding missing config entry: $key" (Get-ScriptName) (Get-ScriptLineNumber)
            $content += "$key = $($config[$key])"
        }
    }
    Set-Content -Path $configFile -Value $content
}

$config = & "$scriptDir\ReadConfig.ps1" $configFile
$config.Keys | sort | % { if(-not ($_.Contains("PASSWORD") -or $_.Contains("KEY"))) { Write-InfoLog ("Key = " + $_ + ", Value = " + $config[$_]) (Get-ScriptName) (Get-ScriptLineNumber) } }

$configFile