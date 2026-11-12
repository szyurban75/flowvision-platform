<#
.SYNOPSIS
    Stores a SQL password for a FlowVision service, encrypted with DPAPI at LocalMachine scope (ADR-001 secondary option).

.DESCRIPTION
    Use this on sites without AD/gMSA. The password is encrypted for this machine only and written to
    %ProgramData%\FlowVision\secrets\<Name>.bin (the path the services read; see DpapiSecretProvider.DefaultPath).
    The file is readable only by the service account and Administrators. The script decrypts the file once after
    writing to confirm it can be read back. It never prints the password.

    Run in an elevated PowerShell on the server, as an Administrator.

.PARAMETER Name
    Secret name, for example sql-admin. Must match Platform:SecretName in the service's appsettings.json.

.PARAMETER ServiceAccount
    Account the service runs as, for example DOMAIN\svc-flowvision-admin or a local account. Gets read access to the file.

.PARAMETER Password
    SecureString with the SQL password. If omitted, the script prompts for it.

.PARAMETER SecretsFolder
    Folder for the secret files. Default: %ProgramData%\FlowVision\secrets.

.EXAMPLE
    .\Set-DpapiSecret.ps1 -Name sql-admin -ServiceAccount 'DOMAIN\svc-flowvision-admin'
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9-]+$')]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    [string]$ServiceAccount,

    [securestring]$Password,

    [string]$SecretsFolder = (Join-Path $env:ProgramData 'FlowVision\secrets')
)

$ErrorActionPreference = 'Stop'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script in an elevated PowerShell (as Administrator).'
}

if (-not $Password) {
    $Password = Read-Host -Prompt "SQL password for $Name" -AsSecureString
}

Add-Type -AssemblyName System.Security

$path = Join-Path $SecretsFolder ($Name + '.bin')

if (-not $PSCmdlet.ShouldProcess($path, 'Write DPAPI-protected secret')) {
    return
}

New-Item -ItemType Directory -Force -Path $SecretsFolder | Out-Null

# Convert the SecureString to bytes without leaving a plain-text string behind.
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
try {
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    $clear = [Text.Encoding]::UTF8.GetBytes($plain)
    $plain = $null
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

try {
    $protected = [Security.Cryptography.ProtectedData]::Protect($clear, $null, [Security.Cryptography.DataProtectionScope]::LocalMachine)
}
finally {
    [Array]::Clear($clear, 0, $clear.Length)
}

[IO.File]::WriteAllBytes($path, $protected)

# Only the service account and Administrators may read the file.
$acl = New-Object Security.AccessControl.FileSecurity
$acl.SetAccessRuleProtection($true, $false)
$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($ServiceAccount, 'Read', 'Allow')))
$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule('BUILTIN\Administrators', 'FullControl', 'Allow')))
Set-Acl -Path $path -AclObject $acl

# Read back to confirm the protected file decrypts on this machine. The value is not printed.
$check = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($path), $null, [Security.Cryptography.DataProtectionScope]::LocalMachine)
$ok = $check.Length -gt 0
[Array]::Clear($check, 0, $check.Length)

if (-not $ok) { throw "Read-back check failed for $path." }
Write-Output "Stored $path (readable by $ServiceAccount and Administrators)."
