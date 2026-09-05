[CmdletBinding()]
param(
    # Only -Configuration is positional. Giving it an explicit position makes every other
    # parameter named-only, so a stray positional argument fails loudly instead of silently
    # binding to whichever string parameter happens to come next in this list.
    [Parameter(Position = 0)]
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $NoRestore,
    [string] $ResultsDirectory = 'artifacts/test-results',
    [switch] $RequireNativeOcx,

    # A repetition of a subset. Its results are recorded, but they can never turn a failed
    # overall run green -- that is the whole point of keeping them apart.
    [switch] $Rerun,
    [string] $Filter,
    [string[]] $Project,

    [string] $ReportPath = 'artifacts/verification-report.json',

    # Documents are never written by an ordinary build. This switch says so out loud, and it
    # refuses to stamp a document from a partial run.
    [switch] $UpdateVerificationDocs
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $repositoryRoot 'VB6Compiler.sln'
$cliProjectPath = Join-Path $repositoryRoot 'src\VB6.Compiler.Cli\VB6.Compiler.Cli.csproj'
$corpusProjectPath = Join-Path $repositoryRoot 'conformance\VISIA\4.8.7.1\prjVisia.vbp'
$matrixPath = Join-Path $repositoryRoot 'docs\vb6-sp6-compatibility-matrix.json'
$resultsPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $ResultsDirectory))

# Everything written below is stamped with this run. A result file that predates it belongs to an
# earlier run and is reported as stale rather than counted -- a green summary built from yesterday's
# files is worse than no summary at all.
$runStartedUtc = [DateTime]::UtcNow
$runId = $runStartedUtc.ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)

# Refused here rather than after the suite: a caller who asked for the wrong thing should learn it
# in a second, not after twenty minutes of testing.
if ($UpdateVerificationDocs -and ($Rerun -or $Filter -or $Project)) {
    throw 'UpdateVerificationDocs needs a complete run: drop -Rerun, -Filter and -Project.'
}

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

# [IO.Path]::GetRelativePath is .NET Core only, and this script has to run under Windows
# PowerShell 5.1 as well as pwsh 7.
function Get-RepositoryRelativePath {
    param([Parameter(Mandatory)][string] $Path)

    $full = [IO.Path]::GetFullPath($Path)
    $prefix = $repositoryRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if ($full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($prefix.Length).Replace('\', '/')
    }

    return $full.Replace('\', '/')
}

function Get-SourceState {
    param([string] $Root)

    $state = [ordered]@{
        commit    = $null
        branch    = $null
        dirty     = $null
        describes = $null
    }

    try {
        $commit = & git -C $Root rev-parse HEAD 2>$null
        if ($LASTEXITCODE -eq 0) { $state.commit = ($commit | Select-Object -First 1).Trim() }

        $branch = & git -C $Root rev-parse --abbrev-ref HEAD 2>$null
        if ($LASTEXITCODE -eq 0) { $state.branch = ($branch | Select-Object -First 1).Trim() }

        $status = & git -C $Root status --porcelain 2>$null
        if ($LASTEXITCODE -eq 0) { $state.dirty = @($status | Where-Object { $_ }).Count -gt 0 }
    }
    catch {
        # No git, no source state. The report says so instead of inventing one.
    }

    # A measurement taken on a dirty tree cannot be reproduced from its commit alone.
    if ($state.dirty -eq $true) {
        $state.describes = "$($state.commit) plus uncommitted changes"
    }
    elseif ($null -ne $state.commit) {
        $state.describes = $state.commit
    }

    return $state
}

<#
.SYNOPSIS
Turns one finished `dotnet test` invocation into a result record.

.DESCRIPTION
The exit code alone is not enough. A process can die before it writes its result file, and a
result file can survive from an earlier run; both look like success to a caller that only reads
the exit code. This reads the file, insists it was written by this run, and reports the reason
when it was not.
#>
function New-TestRunRecord {
    param(
        [Parameter(Mandatory)][string] $Kind,
        [Parameter(Mandatory)][string] $Project,
        [Parameter(Mandatory)][string] $TrxPath,
        [Parameter(Mandatory)][int] $ExitCode,
        [Parameter(Mandatory)][DateTime] $NotBeforeUtc
    )

    $record = [ordered]@{
        kind     = $Kind
        project  = $Project
        trx      = $null
        exitCode = $ExitCode
        total    = $null
        passed   = $null
        failed   = $null
        skipped  = $null
        outcome  = 'failed'
        reason   = $null
    }

    if (-not (Test-Path -LiteralPath $TrxPath)) {
        $record.reason = 'no result file was written for this run'
        return [pscustomobject]$record
    }

    $trxFile = Get-Item -LiteralPath $TrxPath
    $record.trx = Get-RepositoryRelativePath -Path $trxFile.FullName
    if ($trxFile.LastWriteTimeUtc -lt $NotBeforeUtc) {
        $record.reason = "result file is stale: written $($trxFile.LastWriteTimeUtc.ToString('o')), run started $($NotBeforeUtc.ToString('o'))"
        return [pscustomobject]$record
    }

    try {
        $counters = ([xml](Get-Content -LiteralPath $trxFile.FullName -Raw -Encoding UTF8)).TestRun.ResultSummary.Counters
    }
    catch {
        $record.reason = "result file could not be read: $($_.Exception.Message)"
        return [pscustomobject]$record
    }

    if ($null -eq $counters) {
        $record.reason = 'result file carries no counters'
        return [pscustomobject]$record
    }

    $record.total = [int]$counters.total
    $record.passed = [int]$counters.passed
    $record.failed = [int]$counters.failed + [int]$counters.error + [int]$counters.aborted + [int]$counters.timeout
    $record.skipped = [int]$counters.notExecuted

    if ($ExitCode -ne 0) {
        $record.reason = "test process exited with $ExitCode"
    }
    elseif ($record.failed -gt 0) {
        $record.reason = "$($record.failed) case(s) failed"
    }
    elseif ($record.total -eq 0) {
        $record.reason = 'no test case ran'
    }
    else {
        $record.outcome = 'passed'
    }

    return [pscustomobject]$record
}

<#
.SYNOPSIS
Builds the measurement text that -UpdateVerificationDocs writes into the documents.

.DESCRIPTION
The numbers in ROADMAP.md, README.md and CLAUDE.md used to be copied by hand, and they aged
without anyone noticing -- 1698 survived as a "test count" long after it had become the sum of a
standard run and a separate x86 run. Here the text is produced from the run report, so the caveats
travel with the numbers instead of being remembered separately.
#>
function Get-VerificationRegions {
    param([Parameter(Mandatory)] $Report)

    $date = ([DateTime]::Parse($Report.startedUtc)).ToUniversalTime().ToString('yyyy-MM-dd')
    $branch = if ($Report.source.branch) { $Report.source.branch } else { 'unbekannt' }
    $commit = if ($Report.source.commit) { $Report.source.commit.Substring(0, 7) } else { 'unbekannt' }
    $dirtyDe = if ($Report.source.dirty) { ' mit nicht committeten Änderungen' } else { '' }
    $dirtyEn = if ($Report.source.dirty) { ' with uncommitted changes' } else { '' }

    $standard = @($Report.runs | Where-Object { $_.kind -eq 'standard' })
    $standardCases = ($standard | Measure-Object -Property total -Sum).Sum
    $standardPassed = ($standard | Measure-Object -Property passed -Sum).Sum
    $standardFailing = @($standard | Where-Object { $_.outcome -ne 'passed' })
    $standardFailed = $standardCases - $standardPassed
    if ($standardFailed -lt 0) { $standardFailed = 0 }

    $standardLimitDe = 'Serieller Lauf über alle Testprojekte'
    $standardLimitEn = 'Serial run across every test project'
    if ($standardFailing.Count -gt 0) {
        $detail = ($standardFailing | ForEach-Object { "$($_.project): $($_.reason)" }) -join '; '
        $standardLimitDe = "Nicht bestanden -- $detail"
        $standardLimitEn = "Not passing -- $detail"
    }

    $native = @($Report.runs | Where-Object { $_.kind -eq 'native-x86' }) | Select-Object -First 1
    if ($null -eq $native) {
        $nativeResultDe = 'nicht ausgeführt'
        $nativeResultEn = 'not run'
        $nativeLimitDe = 'Ein fehlender nativer Lauf ist kein bestandener; das Gate bleibt offen'
        $nativeLimitEn = 'A missing native run is not a passed one; the gate stays open'
    }
    else {
        $nativeResultDe = "$($native.passed)/$($native.total) bestanden, $($native.skipped) übersprungen"
        $nativeResultEn = "$($native.passed)/$($native.total) passed, $($native.skipped) skipped"
        $nativeLimitDe = 'Getrennter x86-Lauf der WinForms-Tests'
        $nativeLimitEn = 'Separate x86 run of the WinForms tests'
    }

    $rerunRowsDe = ''
    $rerunRowsEn = ''
    foreach ($rerun in @($Report.runs | Where-Object { $_.kind -eq 'rerun' })) {
        $rerunRowsDe += "`n| Wiederholung $($rerun.project) | $($rerun.passed)/$($rerun.total) bestanden | Macht einen fehlgeschlagenen Gesamtlauf nicht rückwirkend grün |"
        $rerunRowsEn += "`n| Rerun of $($rerun.project) | $($rerun.passed)/$($rerun.total) passed | Does not make a failed overall run green |"
    }

    $matrix = $Report.matrix
    $reportPath = $Report.reportPath

    $regions = @{}

    $regions['roadmap-measurements'] = @"
Messung vom $date auf ``$branch`` / ``$commit``$dirtyDe, Lauf ``$($Report.runId)``:

| Messpunkt | Ergebnis | Aussagegrenze |
| --- | --- | --- |
| Release-Build | 0 Warnungen, 0 Fehler | ``TreatWarningsAsErrors``: eine Warnung bricht den Build ab |
| Standardlauf, $($standard.Count) Testprojekte | $standardCases Fälle: $standardPassed bestanden, $standardFailed fehlgeschlagen | $standardLimitDe |
| Nativer x86-Lauf mit ``VB6_REQUIRE_NATIVE_OCX=1`` | $nativeResultDe | $nativeLimitDe |
| VISIA-Analyse | $($Report.visia.analyzed)/$($Report.visia.items) Projektitems, $($Report.visia.errors) Diagnosen | Analyse und Binden, keine Laufzeitabnahme der Anwendung |$rerunRowsDe

Vollständiges Gate (Standardlauf und nativer x86-Lauf auf demselben Quellstand): **$($Report.gate.complete)**.
Der Laufbericht liegt unter ``$reportPath`` und wird nicht versioniert.
"@

    $regions['roadmap-matrix'] = @"
**Kompatibilitätsmatrix nach der Restplanung:** **$($matrix.total) Erwartungen**, davon **$($matrix.implemented) implemented**, **$($matrix.partial) partial** und **$($matrix.planned) planned**;
**$($matrix.documentedVerified)/$($matrix.total) documented-verified**, $($matrix.notYetVerified) ``not-yet-verified``, $($matrix.oracleVerified) ``oracle-verified``.
"@

    $regions['readme-measurements'] = @"
Measured on $date at ``$commit`` on ``$branch``$dirtyEn, run ``$($Report.runId)``:

| Check | Result | What it does not establish |
| --- | --- | --- |
| Release build | 0 warnings, 0 errors | ``TreatWarningsAsErrors``: one warning fails the build |
| Standard serial run, $($standard.Count) test projects | $standardCases cases: $standardPassed passed, $standardFailed failed | $standardLimitEn |
| Native x86 run with ``VB6_REQUIRE_NATIVE_OCX=1`` | $nativeResultEn | $nativeLimitEn |
| VISIA analysis | $($Report.visia.analyzed)/$($Report.visia.items) project items, $($Report.visia.errors) diagnostics | Analysis and binding only, not application runtime behavior |$rerunRowsEn

Complete gate (standard run and native x86 run on the same source state): **$($Report.gate.complete)**.
The run report is written to ``$reportPath`` and is not versioned.
"@

    $regions['readme-matrix'] = @"
The matrix reports **$($matrix.total) expectations**: **$($matrix.implemented) implemented**, **$($matrix.partial) partial**, **$($matrix.planned) planned**;
**$($matrix.documentedVerified) documented-verified**, **$($matrix.notYetVerified) not-yet-verified**, **$($matrix.oracleVerified) oracle-verified**.
"@

    $regions['readme-status-matrix'] =
    "The compatibility matrix contains $($matrix.total) expectations ($($matrix.implemented) implemented, $($matrix.partial) partial, $($matrix.planned) planned) with $($matrix.documentedVerified)/$($matrix.total) documented-verified."

    $regions['claude-matrix'] = @"
Die Matrix enthält $($matrix.total) Erwartungen: $($matrix.implemented) ``implemented``, $($matrix.partial) ``partial``, $($matrix.planned) ``planned``;
$($matrix.documentedVerified) ``documented-verified``, $($matrix.notYetVerified) ``not-yet-verified``, $($matrix.oracleVerified) ``oracle-verified``.
"@

    $regions['claude-measurements'] = @"
Stand der Prüfung $date auf ``$commit``${dirtyDe}: $standardCases Standardfälle in $($standard.Count) Projekten,
$standardPassed bestanden, $standardFailed fehlgeschlagen. Nativer x86-Lauf: $nativeResultDe.
VISIA: $($Report.visia.analyzed)/$($Report.visia.items) Projektitems, $($Report.visia.errors) Diagnosen.
Vollständiges Gate: $($Report.gate.complete). Laufbericht: ``$reportPath``.
"@

    return $regions
}

<#
.SYNOPSIS
Replaces the marked regions of a document with generated text.

.DESCRIPTION
Only the text between a matching begin/end marker pair is touched. Everything a person wrote
around it -- the caveats, the reasoning, the warnings -- stays exactly as written, because a
generator that rewrites prose loses the part of the document that is worth having.
#>
function Update-MarkedRegions {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][hashtable] $Regions
    )

    $original = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $newline = if ($original.Contains("`r`n")) { "`r`n" } else { "`n" }
    $updated = $original
    $applied = @()

    foreach ($id in $Regions.Keys) {
        $begin = "<!-- verification:$id`:begin -->"
        $end = "<!-- verification:$id`:end -->"

        $beginIndex = $updated.IndexOf($begin, [StringComparison]::Ordinal)
        if ($beginIndex -lt 0) { continue }

        $endIndex = $updated.IndexOf($end, $beginIndex, [StringComparison]::Ordinal)
        if ($endIndex -lt 0) {
            throw "$Path has an opening marker for '$id' without its closing marker."
        }

        if ($updated.IndexOf($begin, $beginIndex + $begin.Length, [StringComparison]::Ordinal) -ge 0) {
            throw "$Path carries the marker '$id' more than once."
        }

        $body = ($Regions[$id] -replace "`r`n", "`n").Trim("`n")
        $replacement = $begin + $newline + ($body -replace "`n", $newline) + $newline + $end
        $updated = $updated.Substring(0, $beginIndex) + $replacement + $updated.Substring($endIndex + $end.Length)
        $applied += $id
    }

    if ($applied.Count -eq 0) {
        return $null
    }

    if ($updated -ne $original) {
        # Not Set-Content -Encoding UTF8: under Windows PowerShell 5.1 that writes a BOM, and
        # these documents have never had one. Writing the bytes explicitly keeps both shells
        # producing the same file.
        [IO.File]::WriteAllText($Path, $updated, (New-Object Text.UTF8Encoding($false)))
    }

    return [pscustomobject]@{
        path    = Get-RepositoryRelativePath -Path $Path
        regions = $applied
        changed = $updated -ne $original
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

    $requiredExpectationFields = @($matrix.expectationSchema.required)
    $allowedImplementationValues = @($matrix.statusModel.implementation)
    $allowedVerificationValues = @($matrix.expectationSchema.verificationValues)
    foreach ($requiredField in @('id', 'matrixEntry', 'implementation', 'input', 'expected', 'verification', 'testRefs')) {
        if ($requiredField -notin $requiredExpectationFields) {
            throw "Compatibility matrix expectation schema is missing required field '$requiredField'."
        }
    }

    $seenExpectationIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

    $matrixEntryIds = @($matrix.entries | ForEach-Object { $_.id })
    foreach ($expectation in $matrix.expectations) {
        foreach ($requiredField in $requiredExpectationFields) {
            if ($null -eq $expectation.$requiredField) {
                throw "Compatibility matrix expectation is missing '$requiredField'."
            }
        }

        if (-not $seenExpectationIds.Add([string]$expectation.id)) {
            throw "Compatibility matrix contains duplicate expectation id '$($expectation.id)'."
        }

        if ($expectation.matrixEntry -notin $matrixEntryIds) {
            throw "Compatibility matrix expectation '$($expectation.id)' references an unknown entry."
        }

        if ($expectation.implementation -notin $allowedImplementationValues) {
            throw "Compatibility matrix expectation '$($expectation.id)' has an invalid implementation status."
        }

        if ($expectation.verification -notin $allowedVerificationValues) {
            throw "Compatibility matrix expectation '$($expectation.id)' has an invalid verification status."
        }

        if ($expectation.implementation -eq 'planned' -and
            $expectation.verification -ne 'not-yet-verified') {
            throw "Compatibility matrix expectation '$($expectation.id)' is planned but claims verification '$($expectation.verification)'."
        }

        if ($expectation.implementation -ne 'implemented' -and
            $expectation.verification -eq 'oracle-verified') {
            throw "Compatibility matrix expectation '$($expectation.id)' claims oracle verification without being implemented."
        }

        foreach ($testRef in @($expectation.testRefs)) {
            if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $testRef))) {
                throw "Compatibility matrix expectation '$($expectation.id)' references missing test path '$testRef'."
            }
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

    if ($Project) {
        $selected = @($testProjects | Where-Object { [IO.Path]::GetFileNameWithoutExtension($_.Name) -in $Project })
        $unknown = @($Project | Where-Object { $_ -notin @($testProjects | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.Name) }) })
        if ($unknown.Count -gt 0) {
            throw "Unknown test project(s): $($unknown -join ', ')"
        }

        $testProjects = $selected
    }

    $runs = [Collections.Generic.List[object]]::new()
    $standardKind = if ($Rerun) { 'rerun' } else { 'standard' }

    foreach ($projectFile in $testProjects) {
        $projectName = [IO.Path]::GetFileNameWithoutExtension($projectFile.Name)
        Write-Host "Testing $projectName"

        $trxName = "$projectName.trx"
        $arguments = @(
            'test', $projectFile.FullName,
            '--configuration', $Configuration, '--no-build', '--no-restore',
            '--logger', "trx;LogFileName=$trxName",
            '--results-directory', $resultsPath)
        if ($Filter) {
            $arguments += @('--filter', $Filter)
        }

        $projectStartedUtc = [DateTime]::UtcNow
        & dotnet @arguments
        $runs.Add((New-TestRunRecord -Kind $standardKind -Project $projectName `
                    -TrxPath (Join-Path $resultsPath $trxName) -ExitCode $LASTEXITCODE `
                    -NotBeforeUtc $projectStartedUtc))
    }

    New-Item -ItemType Directory -Force -Path (Join-Path $repositoryRoot 'artifacts') | Out-Null
    $visiaReportPath = Join-Path $repositoryRoot 'artifacts\visia-report.txt'
    $visiaOutput = & dotnet run --project $cliProjectPath --configuration $Configuration --no-build -- $corpusProjectPath '--report' 2>&1 |
        Tee-Object -FilePath $visiaReportPath
    $visiaExitCode = $LASTEXITCODE

    $visia = [ordered]@{
        kind     = $standardKind
        exitCode = $visiaExitCode
        analyzed = $null
        items    = $null
        errors   = $null
        outcome  = 'failed'
        reason   = $null
    }

    $visiaText = ($visiaOutput | Out-String)
    $analyzedMatch = [regex]::Match($visiaText, 'Analyzed\s+(\d+)\s+of\s+(\d+)\s+project items')
    $errorMatch = [regex]::Match($visiaText, 'Total errors:\s+(\d+)')
    if ($analyzedMatch.Success) {
        $visia.analyzed = [int]$analyzedMatch.Groups[1].Value
        $visia.items = [int]$analyzedMatch.Groups[2].Value
    }

    if ($errorMatch.Success) {
        $visia.errors = [int]$errorMatch.Groups[1].Value
    }

    if ($visiaExitCode -ne 0) {
        $visia.reason = "parity report exited with $visiaExitCode"
    }
    elseif (-not $analyzedMatch.Success -or -not $errorMatch.Success) {
        $visia.reason = 'parity report did not state its item and error counts'
    }
    elseif ($visia.errors -ne 0) {
        $visia.reason = "$($visia.errors) corpus error(s)"
    }
    elseif ($visia.analyzed -ne $visia.items) {
        $visia.reason = "only $($visia.analyzed) of $($visia.items) project items were analyzed"
    }
    else {
        $visia.outcome = 'passed'
    }

    $implementedCount = @($matrix.expectations | Where-Object implementation -eq 'implemented').Count
    $partialCount = @($matrix.expectations | Where-Object implementation -eq 'partial').Count
    $plannedCount = @($matrix.expectations | Where-Object implementation -eq 'planned').Count
    $totalCount = @($matrix.expectations).Count
    $documentedVerifiedCount = @($matrix.expectations | Where-Object verification -eq 'documented-verified').Count
    $notYetVerifiedCount = @($matrix.expectations | Where-Object verification -eq 'not-yet-verified').Count
    $oracleVerifiedCount = @($matrix.expectations | Where-Object verification -eq 'oracle-verified').Count
    Write-Host "Matrix: $implementedCount implemented, $partialCount partial, $plannedCount planned von $totalCount | $documentedVerifiedCount/$totalCount documented-verified"

    if ($RequireNativeOcx) {
        $winFormsProject = Join-Path $repositoryRoot 'tests\VB6.Runtime.WinForms.Tests\VB6.Runtime.WinForms.Tests.csproj'
        $previousNativeRequirement = $env:VB6_REQUIRE_NATIVE_OCX
        try {
            $env:VB6_REQUIRE_NATIVE_OCX = '1'
            Write-Host 'Testing VB6.Runtime.WinForms.Tests with required native x86 OCX coverage'
            $nativeTrxName = 'VB6.Runtime.WinForms.Tests.x86.trx'
            $nativeKind = if ($Rerun) { 'rerun' } else { 'native-x86' }
            $nativeStartedUtc = [DateTime]::UtcNow
            & dotnet test $winFormsProject --configuration $Configuration --no-build --no-restore `
                --logger "trx;LogFileName=$nativeTrxName" `
                --results-directory $resultsPath -- RunConfiguration.TargetPlatform=x86
            $runs.Add((New-TestRunRecord -Kind $nativeKind `
                        -Project 'VB6.Runtime.WinForms.Tests (x86 native OCX)' `
                        -TrxPath (Join-Path $resultsPath $nativeTrxName) -ExitCode $LASTEXITCODE `
                        -NotBeforeUtc $nativeStartedUtc))
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

    # Only runs that belong to the gate decide it. A repetition is recorded and never counted:
    # a targeted rerun that passes says nothing about the overall run that failed.
    $gateRuns = @($runs | Where-Object { $_.kind -ne 'rerun' })
    $rerunRuns = @($runs | Where-Object { $_.kind -eq 'rerun' })
    $failedGateRuns = @($gateRuns | Where-Object { $_.outcome -ne 'passed' })

    # A filtered or project-restricted run measured a subset. It can fail the gate, never complete it.
    $partial = [bool]$Filter -or [bool]$Project
    $standardComplete = $gateRuns.Count -gt 0 -and $failedGateRuns.Count -eq 0 -and $visia.outcome -eq 'passed' -and -not $partial
    $nativeRun = @($runs | Where-Object { $_.kind -eq 'native-x86' }) | Select-Object -First 1
    $nativeComplete = $null -ne $nativeRun -and $nativeRun.outcome -eq 'passed'

    $sourceState = Get-SourceState -Root $repositoryRoot
    $report = [ordered]@{
        schemaVersion = '1.0'
        runId         = $runId
        startedUtc    = $runStartedUtc.ToString('o')
        completedUtc  = [DateTime]::UtcNow.ToString('o')
        configuration = $Configuration
        reportPath    = $ReportPath.Replace('\', '/')
        invocation    = [ordered]@{
            rerun            = [bool]$Rerun
            filter           = $Filter
            projects         = @($Project)
            requireNativeOcx = [bool]$RequireNativeOcx
        }
        source        = $sourceState
        runs          = @($runs)
        visia         = $visia
        matrix        = [ordered]@{
            total              = $totalCount
            implemented        = $implementedCount
            partial            = $partialCount
            planned            = $plannedCount
            documentedVerified = $documentedVerifiedCount
            notYetVerified     = $notYetVerifiedCount
            oracleVerified     = $oracleVerifiedCount
        }
        gate          = [ordered]@{
            standardComplete = $standardComplete
            nativeComplete   = $nativeComplete
            # A missing native run is not a passed native run.
            complete         = $standardComplete -and $nativeComplete
        }
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent ([IO.Path]::GetFullPath((Join-Path $repositoryRoot $ReportPath)))) | Out-Null
    $reportJson = $report | ConvertTo-Json -Depth 6
    $utf8NoBom = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText((Join-Path $repositoryRoot $ReportPath), $reportJson, $utf8NoBom)
    [IO.File]::WriteAllText((Join-Path $resultsPath "verification-report-$runId.json"), $reportJson, $utf8NoBom)

    Write-Host ''
    Write-Host "Run $runId on $($sourceState.describes)"
    foreach ($group in @('standard', 'native-x86', 'rerun')) {
        $inGroup = @($runs | Where-Object { $_.kind -eq $group })
        if ($inGroup.Count -eq 0) { continue }

        $cases = ($inGroup | Measure-Object -Property total -Sum).Sum
        $passed = ($inGroup | Measure-Object -Property passed -Sum).Sum
        $failing = @($inGroup | Where-Object { $_.outcome -ne 'passed' })
        Write-Host "  $group : $cases case(s), $passed passed, $($failing.Count) project(s) not passing"
        foreach ($failure in $failing) {
            Write-Host "      $($failure.project): $($failure.reason)"
        }
    }

    Write-Host "  visia ($($visia.kind)): $($visia.outcome) -- $($visia.analyzed)/$($visia.items) items, $($visia.errors) error(s)"
    if (-not $RequireNativeOcx) {
        Write-Host '  native-x86: not run. A missing native run is not a passed one; the gate stays incomplete.'
    }

    if ($rerunRuns.Count -gt 0) {
        Write-Host '  reruns are recorded separately and never turn a failed overall run green.'
    }

    Write-Host "  gate complete: $($report.gate.complete)"
    Write-Host "Report: $ReportPath"

    if ($UpdateVerificationDocs) {
        # A subset never gets to speak for the whole. Stamping the documents from a rerun is how
        # a number becomes wrong while looking freshly measured.
        if ($Rerun -or $partial) {
            throw 'UpdateVerificationDocs needs a complete run: drop -Rerun, -Filter and -Project.'
        }

        $regions = Get-VerificationRegions -Report $report
        $touched = @()
        foreach ($document in @('docs/ROADMAP.md', 'README.md', 'CLAUDE.md')) {
            $result = Update-MarkedRegions -Path (Join-Path $repositoryRoot $document) -Regions $regions
            if ($null -ne $result) { $touched += $result }
        }

        Write-Host ''
        Write-Host 'Verification documents updated:'
        foreach ($document in $touched) {
            $state = if ($document.changed) { 'changed' } else { 'already current' }
            Write-Host "  $($document.path): $state ($($document.regions -join ', '))"
        }

        $unwritten = @($regions.Keys | Where-Object { $_ -notin @($touched | ForEach-Object { $_.regions } ) })
        if ($unwritten.Count -gt 0) {
            throw "No document carries a marker for: $($unwritten -join ', ')"
        }
    }

    $failureNames = @($failedGateRuns | ForEach-Object { $_.project })
    if ($visia.outcome -ne 'passed') {
        $failureNames += 'VISIA parity report'
    }

    $failedReruns = @($rerunRuns | Where-Object { $_.outcome -ne 'passed' } | ForEach-Object { $_.project })
    $failureNames += $failedReruns

    if ($failureNames.Count -gt 0) {
        throw "Verification failed: $($failureNames -join ', ')"
    }
}
finally {
    Pop-Location
}
