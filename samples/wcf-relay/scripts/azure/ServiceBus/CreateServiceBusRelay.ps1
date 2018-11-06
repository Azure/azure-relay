[CmdletBinding(PositionalBinding=$True)]
Param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[a-z0-9-]*$")]
    [String]$ResourceGroupName,                             # required    needs to be alphanumeric or '-'
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[a-z0-9-]*$")]
    [String]$Namespace,                             # required    needs to be alphanumeric or '-'
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[a-z0-9-]*$")]
    [String]$Path,                                  # required    needs to be alphanumeric or '-'
    [Parameter(Mandatory = $true)]                            
    [String]$Location = "West Europe",              # optional    default to "West Europe"
    [String]$UserMetadata = $null,                  # optional    default to $null
    [Bool]$CreateACSNamespace = $False              # optional    default to $false
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


function Ensure-AzureRmRelayAuthorizationRule([string] $ResourceGroupName, [string] $Namespace, [string] $Name, [string] $WcfRelay, [string[]] $Rights)
{
    if (![String]::IsNullOrEmpty($WcfRelay))
    {
        $rule = Get-AzureRmRelayAuthorizationRule -Name $Name -Namespace $Namespace -ResourceGroupName $ResourceGroupName -WcfRelay $WcfRelay -ErrorAction SilentlyContinue
        if ($rule -eq $null)
        {
            $friendlyWcfRelay = $WcfRelay.Replace("~", "/")
            Write-InfoLog "Creating Rule $Name on WcfRelay $friendlyWcfRelay" (Get-ScriptName) (Get-ScriptLineNumber)
            $null = New-AzureRmRelayAuthorizationRule -Name $Name -Namespace $Namespace -ResourceGroupName $ResourceGroupName -WcfRelay $WcfRelay -Rights $Rights
        }

        return (Get-AzureRmRelayKey -ResourceGroupName $ResourceGroupName -Namespace $Namespace -WcfRelay $WcfRelay -Name $Name).PrimaryKey
    }

    $rule = Get-AzureRmRelayAuthorizationRule -Name $Name -Namespace $Namespace -ResourceGroupName $ResourceGroupName -ErrorAction SilentlyContinue
    if ($rule -eq $null)
    {
        Write-InfoLog "Creating Rule $Name on Namespace $Namespace" (Get-ScriptName) (Get-ScriptLineNumber)
        $null = New-AzureRmRelayAuthorizationRule -Name $Name -Namespace $Namespace -ResourceGroupName $ResourceGroupName -Rights $Rights
    }

    return (Get-AzureRmRelayKey -ResourceGroupName $ResourceGroupName -Namespace $Namespace -Name $Name).PrimaryKey
}

###########################################################
# End - Initialization - Invocation, Logging etc
###########################################################

$startTime = Get-Date

$SendRuleName = "samplesend"
$ListenRuleName = "samplelisten"
$ManageRuleName = "samplemanage"

# Check if the namespace already exists or needs to be created
$CurrentNamespace = Get-AzureRmRelayNamespace -Name $Namespace -ResourceGroupName $ResourceGroupName -ErrorAction SilentlyContinue
if ($CurrentNamespace -ne $null)
{
    Write-InfoLog "The Relay namespace $Namespace already exists in location: $($CurrentNamespace.Location)" (Get-ScriptName) (Get-ScriptLineNumber)
}
else
{
    Write-InfoLog "The Relay namespace $Namespace does not exist." (Get-ScriptName) (Get-ScriptLineNumber)
    Write-InfoLog "Creating Relay namespace $Namespace in $Location" (Get-ScriptName) (Get-ScriptLineNumber)
    $CurrentNamespace = New-AzureRmRelayNamespace -Location $Location -Name $Namespace -ResourceGroupName $ResourceGroupName
    Write-InfoLog "The Relay namespace $Namespace in $Location has been successfully created." (Get-ScriptName) (Get-ScriptLineNumber)
}

# These Namespace level rules are never used anywhere
#$RootSendKey = Ensure-AzureRmRelayAuthorizationRule -Name "root$SendRuleName" -Namespace $Namespace -ResourceGroupName $ResourceGroupName -Rights @("Send")
#$RootListenKey = Ensure-AzureRmRelayAuthorizationRule -Name "root$ListenRuleName" -Namespace $Namespace -ResourceGroupName $ResourceGroupName -Rights @("Listen")
#$RootManageKey = Ensure-AzureRmRelayAuthorizationRule -Name "root$ManageRuleName" -Namespace $Namespace -ResourceGroupName $ResourceGroupName -Rights @("Listen", "Manage", "Send")

function CreateRelayOfType([string] $Path, [string] $SubPath, [string] $RelayType)
{   
    $relayPath = $path + "~" + $subPath
    $wcfRelay = Get-AzureRmWcfRelay -Namespace $Namespace -ResourceGroupName $ResourceGroupName -Name $relayPath -ErrorAction SilentlyContinue
    if ($wcfRelay -ne $null)
    {
        Write-InfoLog "The WcfRelay $path/$subPath already exists in the namespace: $Namespace." (Get-ScriptName) (Get-ScriptLineNumber)        
        if ( $wcfRelay.RelayType -ne $relayType )
        {
            throw "Unexpected type RelayType for existing relay $path/$subPath"
        }
    }
    else
    {
        Write-InfoLog "Creating WcfRelay $path/$subPath of type $relayType in namespace $Namespace" (Get-ScriptName) (Get-ScriptLineNumber)
        $wcfRelay = New-AzureRmWcfRelay -ResourceGroupName $ResourceGroupName -Namespace $Namespace -Name $relayPath -WcfRelayType $relayType
        Write-InfoLog "The WcfRelay $path/$subPath in namespace $Namespace has been successfully created." (Get-ScriptName) (Get-ScriptLineNumber)
    }
    
    $SendKey = Ensure-AzureRmRelayAuthorizationRule -ResourceGroupName $ResourceGroupName -Namespace $Namespace -WcfRelay $relayPath -Name $SendRuleName -Rights @("Send")
    $ListenKey = Ensure-AzureRmRelayAuthorizationRule -ResourceGroupName $ResourceGroupName -Namespace $Namespace -WcfRelay $relayPath -Name $ListenRuleName -Rights @("Listen") 
    $ManageKey = Ensure-AzureRmRelayAuthorizationRule -ResourceGroupName $ResourceGroupName -Namespace $Namespace -WcfRelay $relayPath -Name $ManageRuleName -Rights @("Listen", "Manage", "Send")

    $keys = @{
        "$SendRuleName" = "$SendKey";
        "$ListenRuleName" = "$ListenKey";
        "$ManageRuleName" = "$ManageKey";
    }

    return $keys
}

$keys = CreateRelayOfType -Path $Path -SubPath "nettcp" -RelayType "NetTcp"
#TODO: Relay PowerShell commands do allow us to pick the SAS Key value directly so we cannot use the same
#      PrimaryKey on two difference WcfRelays
#$keys = CreateRelayOfType -Path $Path -SubPath "http" -RelayType "Http"

$finishTime = Get-Date
$totalSeconds = ($finishTime - $startTime).TotalSeconds
Write-InfoLog "CreateRelays completed in $totalSeconds seconds." (Get-ScriptName) (Get-ScriptLineNumber)

$keys