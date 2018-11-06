[CmdletBinding(PositionalBinding=$True)]
Param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[a-z0-9-]+$")]
    [String]$ResourceGroup,                         # required    needs to be alphanumeric or '-'
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[a-z0-9-]+$")]
    [String]$Namespace,                             # required    needs to be alphanumeric or '-'
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[a-z0-9-]+$")]
    [String]$Path                                   # required    needs to be alphanumeric or '-'
    )

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

try
{
    $CurrentNamespace = Get-AzureRmRelayNamespace -Name $Namespace -ResourceGroupName $ResourceGroup
}
catch
{
    Write-WarnLog "Azure Service Bus Namespace: $Namespace not found!" (Get-ScriptName) (Get-ScriptLineNumber)
}

if ($CurrentNamespace)
{
    Write-InfoLog "Deleting Azure Relay Namespace: $Namespace" (Get-ScriptName) (Get-ScriptLineNumber)
    try
    {
        Remove-AzureRmRelayNamespace -Name $Namespace -ResourceGroupName $ResourceGroup
        Write-InfoLog "Deleted Azure Relay Namespace: $Namespace" (Get-ScriptName) (Get-ScriptLineNumber)
    }
    catch
    {
        Write-ErrorLog "Failed to delete Azure Relay Namespace: $Namespace" (Get-ScriptName) (Get-ScriptLineNumber) $_
        throw
    }
}
else
{
    Write-InfoLog "The namespace: $Namespace does not exist."  (Get-ScriptName) (Get-ScriptLineNumber)
}