param (
    [string]$ILRepackPath,
    [string]$TargetDir,
    [string]$GameFolder
)

# Remove trailing dot if present (from the csproj hack)
if ($TargetDir.EndsWith(".")) {
    $TargetDir = $TargetDir.Substring(0, $TargetDir.Length - 1)
}

$TargetDir = $TargetDir.TrimEnd("\")

Write-Host "ILRepack Path: $ILRepackPath"
Write-Host "Target Dir: $TargetDir"
Write-Host "Game Folder: $GameFolder"

$InputDll = "$TargetDir\TheSecondSeat.dll"
$OutputDll = "$TargetDir\TheSecondSeat.Merged.dll"
$ScribanDll = "$TargetDir\Scriban.dll"
$ManagedDir = "$GameFolder\RimWorldWin64_Data\Managed"

if (-not (Test-Path $InputDll)) {
    Write-Error "Input DLL not found: $InputDll"
    exit 1
}

# Run ILRepack
& $ILRepackPath /parallel /internalize /lib:$ManagedDir /lib:$TargetDir /out:$OutputDll $InputDll $ScribanDll

if ($LASTEXITCODE -ne 0) {
    Write-Error "ILRepack failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "ILRepack successful. Overwriting original DLL..."
Move-Item -Path $OutputDll -Destination $InputDll -Force

# Handle PDB
$InputPdb = [System.IO.Path]::ChangeExtension($InputDll, ".pdb")
$OutputPdb = [System.IO.Path]::ChangeExtension($OutputDll, ".pdb")
if (Test-Path $OutputPdb) {
    Move-Item -Path $OutputPdb -Destination $InputPdb -Force
}

# Backup Scriban DLL for user
$ScribanBackup = "D:\rim mod\Scriban_6.5.2.dll"
Copy-Item -Path $ScribanDll -Destination $ScribanBackup -Force

Remove-Item -Path $ScribanDll -Force
Write-Host "Merge complete."