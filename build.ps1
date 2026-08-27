[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $NoRestore,
    [string] $ResultsDirectory = 'artifacts/test-results',
    [switch] $RequireNativeOcx
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $repositoryRoot 'VB6Compiler.sln'
$cliProjectPath = Join-Path $repositoryRoot 'src\VB6.Compiler.Cli\VB6.Compiler.Cli.csproj'
$corpusProjectPath = Join-Path $repositoryRoot 'conformance\VISIA\4.8.7.1\prjVisia.vbp'
$matrixPath = Join-Path $repositoryRoot 'docs\vb6-sp6-compatibility-matrix.json'
$resultsPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $ResultsDirectory))

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repositoryRoot
try {
    if (-not (Test-Path -LiteralPath $matrixPath)) {
        throw "Compatibility matrix was not found: $matrixPath"
    }

    $matrix = Get-Content -LiteralPath $matrixPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($matrix.schemaVersion -ne '1.0' -or
        $matrix.profile -ne 'VB6Sp6' -or
        $null -eq $matrix.entries -or
        $null -eq $matrix.expectations) {
        throw "Compatibility matrix has an invalid top-level contract: $matrixPath"
    }

    $matrixEntryIds = @($matrix.entries | ForEach-Object { $_.id })
    foreach ($expectation in $matrix.expectations) {
        if ($expectation.matrixEntry -notin $matrixEntryIds) {
            throw "Compatibility matrix expectation '$($expectation.id)' references an unknown entry."
        }
    }

    if (-not $NoRestore) {
        Invoke-DotNet @('restore', $solutionPath)
    }

    Invoke-DotNet @('build', $solutionPath, '--configuration', $Configuration, '--no-restore', '-m:1')

    New-Item -ItemType Directory -Force -Path $resultsPath | Out-Null
    $testProjects = @(Get-ChildItem -Path (Join-Path $repositoryRoot 'tests') -Filter '*.Tests.csproj' -Recurse |
        Sort-Object FullName)
    if ($testProjects.Count -eq 0) {
        throw 'No test projects were found.'
    }

    $failed = [Collections.Generic.List[string]]::new()
    foreach ($project in $testProjects) {
        $projectName = [IO.Path]::GetFileNameWithoutExtension($project.Name)
        Write-Host "Testing $projectName"
        & dotnet test $project.FullName --configuration $Configuration --no-build --no-restore `
            --logger "trx;LogFileName=$projectName.trx" --results-directory $resultsPath
        if ($LASTEXITCODE -ne 0) {
            $failed.Add($projectName)
        }
    }

    New-Item -ItemType Directory -Force -Path (Join-Path $repositoryRoot 'artifacts') | Out-Null
    $reportPath = Join-Path $repositoryRoot 'artifacts\visia-report.txt'
    & dotnet run --project $cliProjectPath --configuration $Configuration --no-build -- $corpusProjectPath '--report' 2>&1 |
        Tee-Object -FilePath $reportPath
    if ($LASTEXITCODE -ne 0) {
        $failed.Add('VISIA parity report')
    }

    if ($RequireNativeOcx) {
        $winFormsProject = Join-Path $repositoryRoot 'tests\VB6.Runtime.WinForms.Tests\VB6.Runtime.WinForms.Tests.csproj'
        $previousNativeRequirement = $env:VB6_REQUIRE_NATIVE_OCX
        try {
            $env:VB6_REQUIRE_NATIVE_OCX = '1'
            Write-Host 'Testing VB6.Runtime.WinForms.Tests with required native x86 OCX coverage'
            & dotnet test $winFormsProject --configuration $Configuration --no-build --no-restore `
                --logger "trx;LogFileName=VB6.Runtime.WinForms.Tests.x86.trx" `
                --results-directory $resultsPath -- RunConfiguration.TargetPlatform=x86
            if ($LASTEXITCODE -ne 0) {
                $failed.Add('VB6.Runtime.WinForms.Tests (x86 native OCX)')
            }
        }
        finally {
            if ($null -eq $previousNativeRequirement) {
                Remove-Item Env:VB6_REQUIRE_NATIVE_OCX -ErrorAction SilentlyContinue
            }
            else {
                $env:VB6_REQUIRE_NATIVE_OCX = $previousNativeRequirement
            }
        }
    }

    if ($failed.Count -gt 0) {
        throw "Verification failed: $($failed -join ', ')"
    }
}
finally {
    Pop-Location
}
