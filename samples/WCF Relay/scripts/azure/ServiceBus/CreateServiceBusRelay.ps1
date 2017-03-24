[CmdletBinding(PositionalBinding=$True)]
Param(
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
###########################################################
# End - Initialization - Invocation, Logging etc
###########################################################

$serviceBusDll = & "$scriptDir\GetServiceBusDll.ps1" (Get-ScriptName) (Get-ScriptLineNumber)

Write-InfoLog "Adding the $serviceBusDll assembly to the script..." (Get-ScriptName) (Get-ScriptLineNumber)
Add-Type -Path $serviceBusDll
Write-InfoLog "The $serviceBusDll assembly has been successfully added to the script." (Get-ScriptName) (Get-ScriptLineNumber)

$startTime = Get-Date



$SendKey = [Microsoft.ServiceBus.Messaging.SharedAccessAuthorizationRule]::GenerateRandomKey()
$ListenKey = [Microsoft.ServiceBus.Messaging.SharedAccessAuthorizationRule]::GenerateRandomKey() 
$ManageKey = [Microsoft.ServiceBus.Messaging.SharedAccessAuthorizationRule]::GenerateRandomKey()

$SendRuleName = "samplesend"
$ListenRuleName = "samplelisten"
$ManageRuleName = "samplemanage"

$SendAccessRights = [Microsoft.ServiceBus.Messaging.AccessRights[]]([Microsoft.ServiceBus.Messaging.AccessRights]::Send)
$ListenAccessRights = [Microsoft.ServiceBus.Messaging.AccessRights[]]([Microsoft.ServiceBus.Messaging.AccessRights]::Listen)
$ManageAccessRights = [Microsoft.ServiceBus.Messaging.AccessRights[]]([Microsoft.ServiceBus.Messaging.AccessRights]::Manage,[Microsoft.ServiceBus.Messaging.AccessRights]::Send,[Microsoft.ServiceBus.Messaging.AccessRights]::Listen)


# Create Azure Service Bus namespace
$CurrentNamespace = Get-AzureSBNamespace -Name $Namespace

# Check if the namespace already exists or needs to be created
if ($CurrentNamespace)
{
    Write-InfoLog "The namespace: $Namespace already exists in location: $($CurrentNamespace.Region)" (Get-ScriptName) (Get-ScriptLineNumber)
}
else
{
    Write-InfoLog "The namespace: $Namespace does not exist." (Get-ScriptName) (Get-ScriptLineNumber)
    Write-InfoLog "Creating namespace: $Namespace in location: $Location" (Get-ScriptName) (Get-ScriptLineNumber)
    $CurrentNamespace = New-AzureSBNamespace -Name $Namespace -Location $Location -CreateACSNamespace $CreateACSNamespace -NamespaceType Messaging
    #introduce a delay so that the namespace info can be retrieved
    sleep -s 15
    $CurrentNamespace = Get-AzureSBNamespace -Name $Namespace
    Write-InfoLog "The namespace: $Namespace in location: $Location has been successfully created." (Get-ScriptName) (Get-ScriptLineNumber)
    
    $null = New-AzureSBAuthorizationRule -Name "root$SendRuleName" -Namespace $Namespace -Permission $("Send") -PrimaryKey $SendKey
    $null = New-AzureSBAuthorizationRule -Name "root$ListenRuleName" -Namespace $Namespace -Permission $("Listen") -PrimaryKey $ListenKey
    $null = New-AzureSBAuthorizationRule -Name "root$ManageRuleName" -Namespace $Namespace -Permission $("Manage", "Listen","Send") -PrimaryKey $ManageKey
}

# Create the NamespaceManager object to create the Relay
Write-InfoLog "Creating a NamespaceManager object for the namespace: $Namespace" (Get-ScriptName) (Get-ScriptLineNumber)
$NamespaceManager = [Microsoft.ServiceBus.NamespaceManager]::CreateFromConnectionString($CurrentNamespace.ConnectionString);
Write-InfoLog "NamespaceManager object for the namespace: $Namespace has been successfully created." (Get-ScriptName) (Get-ScriptLineNumber)


$RelayTypeMap = @{
   "NetTcp" =  [Microsoft.ServiceBus.RelayType]::NetTcp;
   "NetEvent" =  [Microsoft.ServiceBus.RelayType]::NetEvent;
   "Http" =  [Microsoft.ServiceBus.RelayType]::Http;
   "None" =  [Microsoft.ServiceBus.RelayType]::None
}



$scriptCreateRelayOfType = {
    param($path,$subPath,$relayType)

    $RelayDescription = $null
    
    $relayPath = Join-Path $path $subPath 
    if ($NamespaceManager.RelayExistsAsync($relayPath).GetAwaiter().GetResult())
    {
        Write-InfoLog "The relay: $relayPath already exists in the namespace: $Namespace." (Get-ScriptName) (Get-ScriptLineNumber)
        $RelayDescription = $NamespaceManager.GetRelayAsync($relayPath).GetAwaiter().GetResult()
        if ( $RelayDescription.RelayType -ne $relayType )
        {
            throw "Unexpected type $RelayDescription.RelayType for existing relay $relayPath"
        }
    }
    else
    {
        Write-InfoLog "Creating the relay: $relayPath of type $relayType in the namespace: $Namespace" (Get-ScriptName) (Get-ScriptLineNumber)
        $RelayDescription = New-Object -TypeName Microsoft.ServiceBus.Messaging.RelayDescription -ArgumentList $relayPath, $relayType
        $RelayDescription = $NamespaceManager.CreateRelayAsync($RelayDescription).GetAwaiter().GetResult();
        Write-InfoLog "The relay: $Path in the namespace: $Namespace has been successfully created." (Get-ScriptName) (Get-ScriptLineNumber)
        $RelayDescription = $NamespaceManager.GetRelayAsync($relayPath).GetAwaiter().GetResult()
    }
    
    $Rule = New-Object -TypeName Microsoft.ServiceBus.Messaging.SharedAccessAuthorizationRule -ArgumentList $SendRuleName, $SendKey, $SendAccessRights
    $SendRule = New-Object -TypeName Microsoft.ServiceBus.Messaging.SharedAccessAuthorizationRule -ArgumentList $SendRuleName, $SendKey, $SendAccessRights
    $ListenRule = New-Object -TypeName Microsoft.ServiceBus.Messaging.SharedAccessAuthorizationRule -ArgumentList $ListenRuleName, $ListenKey, $ListenAccessRights
    $ManageRule = New-Object -TypeName Microsoft.ServiceBus.Messaging.SharedAccessAuthorizationRule -ArgumentList $ManageRuleName, $ManageKey, $ManageAccessRights
    
    if ( $RelayDescription.Authorization.TryGetSharedAccessAuthorizationRule($SendRuleName, [ref]$Rule))
    {
        $Rule.PrimaryKey = $SendKey
    }
    else
    {
        $RelayDescription.Authorization.Add($SendRule)
    }
    
    if ( $RelayDescription.Authorization.TryGetSharedAccessAuthorizationRule($ListenRuleName, [ref]$Rule))
    {
        $Rule.PrimaryKey = $ListenKey
    }
    else
    {
        $RelayDescription.Authorization.Add($ListenRule)
    }
    
    if ( $RelayDescription.Authorization.TryGetSharedAccessAuthorizationRule($ManageRuleName, [ref]$Rule))
    {
        $Rule.PrimaryKey = $ManageKey
    }
    else
    {
        $RelayDescription.Authorization.Add($ManageRule)
    }
    
    $RelayDescription = $NamespaceManager.UpdateRelayAsync($RelayDescription).GetAwaiter().GetResult();
}

& Invoke-Command $scriptCreateRelayOfType -ArgumentList $Path, "nettcp", $RelayTypeMap."NetTcp"
& Invoke-Command $scriptCreateRelayOfType -ArgumentList $Path, "http", $RelayTypeMap."Http"

$finishTime = Get-Date
$totalSeconds = ($finishTime - $startTime).TotalSeconds
Write-InfoLog "CreateRelays completed in $totalSeconds seconds." (Get-ScriptName) (Get-ScriptLineNumber)

$keys = @{
  "$SendRuleName" = "$SendKey";
  "$ListenRuleName" = "$ListenKey";
  "$ManageRuleName" = "$ManageKey";
}

Write-InfoLog "keys $keys"

return $keys