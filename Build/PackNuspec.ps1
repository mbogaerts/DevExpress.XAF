param(
    $Branch = "master",
    $nugetBin = "$PSScriptRoot\..\bin\Nupkg",
    $sourceDir = "$PSScriptRoot\..",
    $Filter ,
    [switch]$SkipReadMe,
    [string[]]$ChangedModules =((Get-ChildItem $nugetBin).BaseName|ConvertTo-PackageObject).Id
)

Import-Module XpandPwsh -Force -Prefix X
$ErrorActionPreference = "Stop"
Get-Variable ChangedModules | Out-Variable
New-Item $nugetBin -ItemType Directory -Force | Out-Null
Get-ChildItem $nugetBin | Remove-Item -Force -Recurse
$toolPackages = @("Xpand.VersionConverter", "Xpand.XAF.ModelEditor")
& "$PSScriptRoot\PackTools.ps1" $toolPackages $Branch

if (!$ChangedModules) {
    $ChangedModules
    Write-HostFormatted "Skipping package creation as no package changed" -ForegroundColor Yellow
    return
}

Set-Location $sourceDir
$assemblyVersions = & "$sourceDir\build\AssemblyVersions.ps1" $sourceDir

# Get-ChildItem "$sourceDir\tools\nuspec" "Xpand*$filter*.nuspec" -Recurse | ForEach-Object {
$nuspecs = Get-ChildItem "$sourceDir\build\nuspec" "Xpand.*$filter*.nuspec" -Exclude "*Tests*" -Recurse

$nugetPath = (Get-XNugetPath)

$packScript = {
    $name = $_.FullName
    "packScript=$name"
    $basePath = "$sourceDir\bin"
    if ($name -like "*Client*") {
        $basePath += "\ReactiveLoggerClient"
    }
    
    $packageName = [System.IO.Path]::GetFileNameWithoutExtension($_.FullName)
    $assemblyItem = $assemblyVersions | Where-Object { $_.name -eq $packageName }
    
    $version = $assemblyItem.Version
    if ($packageName -like "*All") {
        [xml]$coreNuspec = Get-Content "$sourceDir\build\nuspec\$packagename.nuspec"
        $version = $coreNuspec.package.metadata.Version
    }
 
    Invoke-Script {
        Write-Output "$nugetPath pack $name -OutputDirectory $($nugetBin) -Basepath $basePath -Version $version " #-f Blue
        & $nugetPath pack $name -OutputDirectory $nugetBin -Basepath $basePath -Version $version -NoPackageAnalysis
    }
    
}
$varsToImport = @("assemblyVersions", "SkipReadMe", "nugetPath", "sourceDir", "nugetBin", "SkipReadMe")
$nuspecs | Invoke-Parallel -VariablesToImport $varsToImport -Script $packScript
# $nuspecs | ForEach-Object { Invoke-Command $packScript -ArgumentList $_ }
function AddReadMe {
    param(
        $Package,
        $Directory,
        [System.IO.FileInfo[]]$assemblyList,
        $modules
    )
    if ($Package.Id -like "Xpand.XAF*") {
        $moduleName = $modules|Where-Object{$_.Package -eq $Package.id}
        $wikiName = "Modules"
        if ($moduleName) {
            $wikiName = Get-XpandPackageHome $Package.Id
        }
        $registration = "RequiredModuleTypes.Add(typeof($($moduleName.FullName)));"
        if ($package.Id -like "*all*") {
            $registration = ($modules | Where-Object { $_.platform -eq "Core" -or $package.id -like "*$($_.platform)*" } | ForEach-Object { "RequiredModuleTypes.Add(typeof($($_.FullName)));" }) -join "`r`n                                                "
        }
        
        $message = @"


        
++++++++++++++++++++++++  ++++++++
++++++++++++++++++++++##  ++++++++      ➤ 𝗘𝗫𝗖𝗟𝗨𝗦𝗜𝗩𝗘 𝗦𝗘𝗥𝗩𝗜𝗖𝗘𝗦 @ 
++++++++++++++++++++++  ++++++++++          ☞ http://HireMe.expandframework.com
++++++++++    ++++++  ++++++++++++      
++++++++++++  ++++++  ++++++++++++      ➤  ɪғ ʏᴏᴜ ʟɪᴋᴇ ᴏᴜʀ ᴡᴏʀᴋ ᴘʟᴇᴀsᴇ ᴄᴏɴsɪᴅᴇʀ ᴛᴏ ɢɪᴠᴇ ᴜs ᴀ STAR. 
++++++++++++++  ++  ++++++++++++++          ☞ https://github.com/eXpandFramework/DevExpress.XAF/stargazers
++++++++++++++    ++++++++++++++++      
++++++++++++++  ++  ++++++++++++++      ➤ Package Notes
++++++++++++  ++++    ++++++++++++         ☞ Build the project before opening the model editor.
++++++++++  ++++++++  ++++++++++++         ☞ Documentation can be found @ https://github.com/eXpandFramework/DevExpress.XAF/wiki/$wikiName.
++++++++++  ++++++++++  ++++++++++         ☞ $($package.id) only adds the required references. To register the included packages add the next line/s in the constructor of your XAF module.
++++++++  ++++++++++++++++++++++++              
++++++  ++++++++++++++++++++++++++            $registration
        
"@
        Set-Content "$Directory\ReadMe.txt" $message
    }
    else {
        Remove-Item "$Directory\ReadMe.txt" -Force -ErrorAction SilentlyContinue
    }
}

Write-HostFormatted "Discover XAF XAFModules" -Section
$packages = & (Get-NugetPath) list -source $nugetBin | ConvertTo-PackageObject | Where-Object { $_.id -notin $toolPackages }
$modules = Get-MSBuildProjects "$sourceDir\src\Modules\" | ForEach-Object {
    $proj=Get-XmlContent $_.fullname
    $PackageId=$_.BaseName
    $outputType=$proj.project.propertygroup.OutputType|Where-Object{$_}
    if ($outputType -ne "WinExe"){
        $dir = $_.DirectoryName
        try {
            $platform = Get-ProjectAssemblyMetadata "$dir\Properties\AssemblyInfo.cs" "platform"
            $module = (Get-ProjectAssemblyMetadata "$dir\Properties\AssemblyInfo.cs" "Module").Replace("nameof(", "")
            $module = "$($_.BaseName).$module"
            [PSCustomObject]@{
                platform = $platform
                fullname = $module
                package =$PackageId
            }
        }
        catch {
            throw "Missing modulename in $dir"   
        }
    }
    
}

if ($Branch -ne "lab") {
    Write-HostFormatted "Update Nupkg files (ReadMe)" -Section
    $packages| ForEach-Object {
        $baseName = "$($_.Id).$($_.Version)"
        $baseName
        $zip = "$nugetbin\$baseName.zip" 
        $nupkgPath = "$nugetBin\$baseName.nupkg"
        Move-Item $nupkgPath $zip
        $unzipDir = "$nugetBin\$baseName"
        Expand-Archive $zip $unzipDir
        Remove-Item $zip
        AddReadme $_ $unzipDir $assemblyList $modules
        Compress-Files "$unzipDir" $zip 
        Move-Item $zip $nupkgPath
        Remove-Item $unzipDir -Force -Recurse
    }
    Write-HostFormatted "Update ReadMe" -Section
    & "$PSScriptRoot\UpdateReadMe.ps1" 
}
Write-HostFormatted "Remove not changed packages" -Section
if ($ChangedModules) {
    
    $core = @(Get-ChildItem "$sourceDir\bin" Xpand*.dll | Where-Object { $_.BaseName -in $ChangedModules } | ForEach-Object { (Get-AssemblyMetadata $_.FullName -Key platform).value } | Get-Unique | ForEach-Object {
            "Xpand.XAF.$_.All"
        })
    if ($core | Select-String "core") {
        $core += "Xpand.XAF.Win.All", "Xpand.XAF.Web.All"
    }
    $core = $core | Sort-Object -Unique
    $ChangedModules += $core
    $s = "lab"
    if ($Branch -ne $s) {
        $s = "Release"
    }
    $toolPackages | ForEach-Object {
        if ((Find-XpandPackage $_ $s).Version -ne (Get-NugetPackageSearchMetadata $_ -Source $nugetBin).identity.version.version) {
            $ChangedModules += $_
        }
    }
    
    "ChangedModules:"
    $ChangedModules | Write-Output
    $nupks = Get-ChildItem $nugetBin
    & (Get-NugetPath) list -source $nugetBin | ConvertTo-PackageObject | ForEach-Object {
        $p = $_
        if ($p.Id -notin $ChangedModules) {
            $nupks | Where-Object { $_.BaseName -eq "$($p.Id).$($p.Version)" }
        }
    } | Remove-Item -Verbose
    
}
