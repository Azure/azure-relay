[CmdletBinding(PositionalBinding=$True)]
Param(
    [parameter(Mandatory=$true)]
    [string]$ExampleDir,
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
# Main Script
###########################################################

# Make sure you run this in Microsoft Azure Powershell prompt
if(-not (& "$scriptDir\CheckAzurePowershell.ps1"))
{
    Write-ErrorLog "Check Azure Powershell Failed! You need to run this script from Azure Powershell." (Get-ScriptName) (Get-ScriptLineNumber)
    throw "Check Azure Powershell Failed! You need to run this script from Azure Powershell."
}

###########################################################
# Get Run Configuration
###########################################################
# Make sure you run this in Microsoft Azure Powershell prompt
if(-not (Test-Path $configFile))
{
    Write-ErrorLog "No run configuration file found at '$configFile'" (Get-ScriptName) (Get-ScriptLineNumber)
    throw "No run configuration file found at '$configFile'"
}
$config = & "$scriptDir\..\config\ReadConfig.ps1" $configFile

###########################################################
# Add Azure Account
###########################################################
$tenantId = $config["AZURE_TENANT_ID"]
$connectParams = @{}
if($tenantId) { $connectParams["TenantId"] = $tenantId }

$context = Get-AzContext
if($context -eq $null)
{
    $context = Connect-AzAccount @connectParams
    if($context -eq $null)
    {
        Write-ErrorLog "Failed to connect to Azure Account." (Get-ScriptName) (Get-ScriptLineNumber)
        throw "Failed to connect to Azure Account."
    }
    $context = Get-AzContext
}
elseif($tenantId -and $context.Tenant.Id -ne $tenantId)
{
    Write-InfoLog "Current context is for tenant $($context.Tenant.Id), switching to $tenantId" (Get-ScriptName) (Get-ScriptLineNumber)
    $context = Connect-AzAccount @connectParams
    $context = Get-AzContext
}
Write-SpecialLog ("Using Azure Account: " + $context.Account.Id) (Get-ScriptName) (Get-ScriptLineNumber)

# Use the tenant from the current context if not explicitly configured
if(-not $tenantId) { $tenantId = $context.Tenant.Id }
$subscriptionParams = @{}
if($tenantId) { $subscriptionParams["TenantId"] = $tenantId }

$subscriptions = Get-AzSubscription @subscriptionParams
$subName = ($subscriptions | ? { $_.Name -eq $config["AZURE_SUBSCRIPTION_NAME"] } | Select-Object -First 1 ).Name
if($subName -eq $null)
{
    $subNames = $subscriptions | % { "`r`n" + $_.Name + " - " + $_.Id}
    Write-InfoLog ("Available Subscription Names (Name - Id):" + $subNames) (Get-ScriptName) (Get-ScriptLineNumber)

    $subName = Read-Host "Enter subscription name"

    #Update the Azure Subscription Id in config
    & "$scriptDir\..\config\ReplaceStringInFile.ps1" $configFile $configFile @{AZURE_SUBSCRIPTION_NAME=$subName}
    
    ###########################################################
    # Refresh Run Configuration
    ###########################################################
    $config = & "$scriptDir\..\config\ReadConfig.ps1" $configFile
}

Write-SpecialLog "Current run configuration:" (Get-ScriptName) (Get-ScriptLineNumber)
$config.Keys | sort | % { if(-not ($_.Contains("PASSWORD") -or $_.Contains("KEY"))) { Write-SpecialLog ("Key = " + $_ + ", Value = " + $config[$_]) (Get-ScriptName) (Get-ScriptLineNumber) } }

Write-SpecialLog ("Using subscription: " + $config["AZURE_SUBSCRIPTION_NAME"]) (Get-ScriptName) (Get-ScriptLineNumber)
$setContextParams = @{ SubscriptionName = $config["AZURE_SUBSCRIPTION_NAME"] }
if($tenantId) { $setContextParams["Tenant"] = $tenantId }
Set-AzContext @setContextParams

###########################################################
# Check Azure Resource Creation List
###########################################################


$startTime = Get-Date

###########################################################
# Create Resource Group
###########################################################
$rgName = $config["AZURE_RESOURCE_GROUP"]
$location = $config["AZURE_LOCATION"]
Write-SpecialLog "Creating Resource Group: $rgName in $location" (Get-ScriptName) (Get-ScriptLineNumber)
$null = New-AzResourceGroup -Name $rgName -Location $location -Force

###########################################################
# Create Azure Resources
###########################################################


Write-SpecialLog "Creating ServiceBus Relay" (Get-ScriptName) (Get-ScriptLineNumber)
        
$setContextParams = @{ SubscriptionName = $subName }
if($tenantId) { $setContextParams["Tenant"] = $tenantId }
Set-AzContext @setContextParams
& "$scriptDir\..\init.ps1"
Write-InfoLog "Creating Relay" (Get-ScriptName) (Get-ScriptLineNumber)
$sbKeys = & "$scriptDir\ServiceBus\CreateServiceBusRelay.ps1" $config["AZURE_RESOURCE_GROUP"] $config["SERVICEBUS_NAMESPACE"] $config["SERVICEBUS_ENTITY_PATH"] $config["AZURE_LOCATION"]
if($sbKeys)
{
    & "$scriptDir\..\config\ReplaceStringInFile.ps1" $configFile $configFile @{SERVICEBUS_SEND_KEY=$sbKeys["samplesend"]}
    & "$scriptDir\..\config\ReplaceStringInFile.ps1" $configFile $configFile @{SERVICEBUS_LISTEN_KEY=$sbKeys["samplelisten"]}
    & "$scriptDir\..\config\ReplaceStringInFile.ps1" $configFile $configFile @{SERVICEBUS_MANAGE_KEY=$sbKeys["samplemanage"]}
}

$finishTime = Get-Date
$totalSeconds = ($finishTime - $startTime).TotalSeconds
Write-InfoLog "Azure resources created, completed in $totalSeconds seconds." (Get-ScriptName) (Get-ScriptLineNumber)