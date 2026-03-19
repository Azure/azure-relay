if(Get-Module -ListAvailable Az.Accounts)
{
    $True
}
else
{
    Write-ErrorLog "You need the Az PowerShell module to run these scripts. Please follow the guide to install it: https://learn.microsoft.com/powershell/azure/install-azure-powershell" (Get-ScriptName) (Get-ScriptLineNumber)
    $False
}