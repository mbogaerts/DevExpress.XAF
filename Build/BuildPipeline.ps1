param(
    $Branch = "lab",
    $SourcePath = "$PSScriptRoot\..",
    $GitHubUserName = "apobekiaris",
    $GitHubToken = $env:GitHubToken,
    $DXApiFeed = $env:LocalDXFeed,
    $artifactstagingdirectory,
    $bindirectory,
    [string]$AzureToken = $env:AzDevopsToken,
    [string]$CustomVersion = "20.2.3.0"
)

if (!(Get-Module eXpandFramework -ListAvailable)) {
    $env:AzDevopsToken = $AzureToken
    $env:AzOrganization = "eXpandDevOps"
    $env:AzProject = "eXpandFramework"
    $env:DxFeed = $DxApiFeed
}
"XpandPwsh"
Get-Module XpandPwsh -ListAvailable
"CustomVersion=$CustomVersion"

$ErrorActionPreference = "Stop"
$regex = [regex] '(\d{2}\.\d*)'
$result = $regex.Match($CustomVersion).Groups[1].Value;
& "$SourcePath\go.ps1" -InstallModules

Clear-NugetCache -Filter XpandPackages
Invoke-Script{
    Set-VsoVariable build.updatebuildnumber "$env:build_BuildNumber-$CustomVersion"
    $stage = "$SourcePath\buildstage"
    Remove-Item $stage -force -Recurse -ErrorAction SilentlyContinue
    Set-Location $SourcePath
    dotnet tool restore
    $latestMinors = Get-XAFLatestMinors 
    "latestMinors:"
    $latestMinors|Format-Table
    $CustomVersion = $latestMinors | Where-Object { "$($_.Major).$($_.Minor)" -eq $result }
    "CustomVersion=$CustomVersion"

    $DXVersion = Get-DevExpressVersion 

    $taskList = "Build"
    . "$SourcePath\build\UpdateDependencies.ps1" $CustomVersion
    . "$SourcePath\build\UpdateLatestProjectVersion.ps1"
    

    $bArgs = @{
        packageSources = "$(Get-PackageFeed -Xpand);$DxApiFeed"
        tasklist       = $tasklist
        dxVersion      = $CustomVersion
        ChangedModules = @($updateVersion)
        Branch         = $Branch
    }
    Write-HostFormatted "ChangedModules:" -Section
    $bArgs.ChangedModules|Sort-Object | Out-String
    $SourcePath | ForEach-Object {
        Set-Location $_
        Move-PaketSource 0 $DXApiFeed
    }

    Set-Location "$SourcePath"
    "PaketRestore $SourcePath"

    Write-HostFormatted "Start-ProjectConverter version $CustomVersion"  -Section
    Start-XpandProjectConverter -version $CustomVersion -path $SourcePath -SkipInstall

    try {
        Invoke-PaketRestore -Strict 
    }
    catch {
        "PaketRestore Failed"
        Write-HostFormatted "PaketInstall $SourcePath (due to different Version)" -section
        dotnet paket install 
    }
    if ($Branch -eq "lab") {
        Write-HostFormatted "checking for New DevExpress Version ($CustomVersion) " -Section
        $filter = "DevExpress*"
        
        [version]$currentVersion = Get-VersionPart (Get-DevExpressVersion) Build
        $outputFolder = "$([System.IO.Path]::GetTempPath())\GetNugetpackage"
        $rxdllpath=Get-ChildItem ((get-item (Get-NugetPackage -Name Xpand.XAF.Modules.Reactive -Source (Get-PackageFeed -Xpand) -OutputFolder $outputFolder -ResultType NupkgFile )).DirectoryName) "Xpand.XAF.Modules.Reactive.dll" -Recurse|Select-Object -First 1
        $assemblyReference=Get-AssemblyReference $rxdllpath.FullName
        [version]$publishdeVersion = Get-VersionPart (($assemblyReference | Where-Object { $_.Name -like $filter }).version) Build
        if ($publishdeVersion -lt $currentVersion) {
            Write-HostFormatted "new DX version detected $currentVersion"
            $trDeps = Get-NugetPackageDependencies DevExpress.ExpressApp.Core.all -Source $env:DxFeed -filter $filter -Recurse
            Push-Location 
            $projectPackages = (Get-ChildItem "$SourcePath\src\modules" *.csproj -Recurse) + (Get-ChildItem "$SourcePath\src\extensions" *.csproj -Recurse) | Invoke-Parallel -VariablesToImport "filter" -Script {
                Push-Location $_.DirectoryName
                [PSCustomObject]@{
                    Project           = $_
                    InstalledPackages = (Invoke-PaketShowInstalled -Project $_.FullName) | Where-Object { $_.id -like $filter }
                }
                Pop-Location
            }
            ($projectPackages | Where-Object { $_.InstalledPackages.id | Where-Object { $_ -in $trDeps.id } }).Project | Get-Item | ForEach-Object {
                Write-HostFormatted "Increase $($_.basename) revision" -ForegroundColor Magenta
                Update-AssemblyInfo $_.DirectoryName -Revision
                $bArgs.ChangedModules += $_.basename
            }
        }
    }

    if ($bArgs.ChangedModules) {
        $bArgs.ChangedModules = $bArgs.ChangedModules | Sort-Object -Unique
    }
    Write-HostFormatted "ChangedModules" -Section
    $bArgs.ChangedModules
    
    
    & $SourcePath\go.ps1 @bArgs

    Move-PaketSource 0 "C:\Program Files (x86)\DevExpress $(Get-VersionPart $DXVersion Minor)\Components\System\Components\Packages"
    New-Item  "$Sourcepath\Bin\Tests" -ItemType Directory -ErrorAction SilentlyContinue 
    Copy-Item "$Sourcepath\Bin" "$stage\Bin" -Recurse -Force 
    
}