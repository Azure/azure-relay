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

function Prompt-Host([string] $Title, [object[]] $List)
{
    $List | Out-GridView -Title $Title -OutputMode Single
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
# Connect Azure Account
###########################################################

$context = Get-AzureRmContext
if ($context -eq $null -or $context.Account -eq $null)
{
    $null = Connect-AzureRmAccount
    $context = Get-AzureRmContext
}

if($context -eq $null -or $context.Account -eq $null)
{
    Write-ErrorLog "Failed to add Azure Account." (Get-ScriptName) (Get-ScriptLineNumber)
    throw "Failed to add Azure Account."
}

Write-SpecialLog ("Using Azure Account: " + $context.Account.Id) (Get-ScriptName) (Get-ScriptLineNumber)

$subscriptions = Get-AzureRmSubscription
$subName = ($subscriptions | ? { $_.Name -eq $config["AZURE_SUBSCRIPTION_NAME"] } | Select-Object -First 1 ).Name
if($subName -eq $null)
{
    $subNames = $subscriptions | Select-Object Name, Id, TenantId | Sort-Object Name
    $subName = (Prompt-Host -List $subNames -Title "Select Subscription").Name

    #Update the Azure Subscription Id in config
    & "$scriptDir\..\config\ReplaceStringInFile.ps1" $configFile $configFile @{AZURE_SUBSCRIPTION_NAME=$subName}
    
    ###########################################################
    # Refresh Run Configuration
    ###########################################################
    $config = & "$scriptDir\..\config\ReadConfig.ps1" $configFile
}

Write-SpecialLog ("Using subscription: " + $config["AZURE_SUBSCRIPTION_NAME"]) (Get-ScriptName) (Get-ScriptLineNumber)
$null = Set-AzureRmContext -Subscription $config["AZURE_SUBSCRIPTION_NAME"]

$resourceGroups = Get-AzureRMResourceGroup
$resourceGroup = ($resourceGroups | ? { $_.ResourceGroupName -eq $config["AZURE_RESOURCE_GROUP"] } | Select-Object -First 1 )
if($resourceGroup -eq $null)
{
    $resourceGroups = $resourceGroups | Select-Object ResourceGroupName, Location, ResourceId, Tags | Sort-Object ResourceGroupName
    $resourceGroupName = (Prompt-Host -List $resourceGroups -Title "Select ResourceGroup").ResourceGroupName
    $resourceGroup = Get-AzureRMResourceGroup -Name $resourceGroupName

    #Update the Azure ResourceGroup in config
    & "$scriptDir\..\config\ReplaceStringInFile.ps1" $configFile $configFile @{AZURE_RESOURCE_GROUP=$resourceGroupName; AZURE_LOCATION=$resourceGroup.Location}
    
    ###########################################################
    # Refresh Run Configuration
    ###########################################################
    $config = & "$scriptDir\..\config\ReadConfig.ps1" $configFile
}

Write-SpecialLog "Current run configuration:" (Get-ScriptName) (Get-ScriptLineNumber)
$config.Keys | sort | % { if(-not ($_.Contains("PASSWORD") -or $_.Contains("KEY"))) { Write-SpecialLog ("Key = " + $_ + ", Value = " + $config[$_]) (Get-ScriptName) (Get-ScriptLineNumber) } }

###########################################################
# Check Azure Resource Creation List
###########################################################


$startTime = Get-Date


###########################################################
# Create Azure Resources
###########################################################


Write-SpecialLog "Creating Relay Artifacts" (Get-ScriptName) (Get-ScriptLineNumber)
$sbKeys = & "$scriptDir\ServiceBus\CreateServiceBusRelay.ps1" $config["AZURE_RESOURCE_GROUP"] $config["SERVICEBUS_NAMESPACE"] $config["SERVICEBUS_ENTITY_PATH"] $config["AZURE_LOCATION"] 
if($sbKeys)
{
    $configKeys = @{
        SERVICEBUS_SEND_KEY=$sbKeys["samplesend"];
        SERVICEBUS_LISTEN_KEY=$sbKeys["samplelisten"];
        SERVICEBUS_MANAGE_KEY=$sbKeys["samplemanage"];
    }

    & "$scriptDir\..\config\ReplaceStringInFile.ps1" $configFile $configFile $configKeys
}

$finishTime = Get-Date
$totalSeconds = ($finishTime - $startTime).TotalSeconds
Write-InfoLog "Azure resources created, completed in $totalSeconds seconds." (Get-ScriptName) (Get-ScriptLineNumber)