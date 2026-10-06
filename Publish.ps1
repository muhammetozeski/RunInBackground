[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'RunInBackGround.csproj'
$packageCache = Join-Path $env:USERPROFILE '.nuget\packages'
foreach ($variant in @('SelfContained', 'FrameworkDependent')) {
    $standalone = $variant -eq 'SelfContained'
    $output = Join-Path $PSScriptRoot "publish\$variant"
    & dotnet publish $project -c Release -r win-x64 --self-contained $standalone.ToString().ToLowerInvariant() `
        -o $output --source $packageCache -p:NuGetAudit=false -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true "-p:EnableCompressionInSingleFile=$($standalone.ToString().ToLowerInvariant())" `
        -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "Publishing $variant failed: $LASTEXITCODE" }
}
