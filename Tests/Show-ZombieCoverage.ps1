param(
    [Parameter(Mandatory = $true)][string[]]$CoverageFiles,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$files = @{}
$branches = New-Object 'System.Collections.Generic.List[string]'
$culture = [System.Globalization.CultureInfo]::InvariantCulture

foreach ($coverageFile in $CoverageFiles) {
    [xml]$report = Get-Content -LiteralPath $coverageFile -Raw
    foreach ($package in $report.coverage.packages.package) {
        $lineRate = [double]::Parse($package.GetAttribute('line-rate'), $culture) * 100
        $branchRate = [double]::Parse($package.GetAttribute('branch-rate'), $culture) * 100
        $branches.Add(('| {0} | {1} | {2:F1}% | {3:F1}% |' -f
            [System.IO.Path]::GetRelativePath((Get-Location).Path, (Resolve-Path -LiteralPath $coverageFile).Path), $package.name, $lineRate, $branchRate))
        foreach ($class in $package.classes.class) {
            $key = '{0}:{1}' -f $package.name, $class.filename
            if (-not $files.ContainsKey($key)) {
                $files[$key] = @{ Package = $package.name; Path = $class.filename; Lines = @{} }
            }
            foreach ($line in $class.lines.line) {
                $number = [int]$line.number
                $files[$key].Lines[$number] = $files[$key].Lines[$number] -or ([long]$line.hits -gt 0)
            }
        }
    }
}

$output = New-Object 'System.Collections.Generic.List[string]'
$output.Add('# Zombies Stats coverage')
$output.Add('')
$output.Add('Line coverage below merges hits by source file and line, including async bodies and lambdas. Branch rates are shown per input report; Cobertura percentages alone cannot reconstruct the union of branch paths.')
$output.Add('')
$output.Add('| Assembly | Covered lines | Total lines | Line coverage |')
$output.Add('| --- | ---: | ---: | ---: |')
foreach ($group in ($files.Values | Group-Object Package | Sort-Object Name)) {
    $covered = 0; $total = 0
    foreach ($file in $group.Group) {
        $total += $file.Lines.Count
        $covered += @($file.Lines.Values | Where-Object { $_ }).Count
    }
    $rate = if ($total -eq 0) { 0 } else { 100.0 * $covered / $total }
    $output.Add(('| {0} | {1} | {2} | {3:F1}% |' -f $group.Name, $covered, $total, $rate))
}
$output.Add('')
$output.Add('| Source file | Covered lines | Total lines | Line coverage |')
$output.Add('| --- | ---: | ---: | ---: |')
foreach ($file in ($files.Values | Sort-Object Package, Path)) {
    $total = $file.Lines.Count
    $covered = @($file.Lines.Values | Where-Object { $_ }).Count
    $rate = if ($total -eq 0) { 0 } else { 100.0 * $covered / $total }
    $output.Add(('| {0} | {1} | {2} | {3:F1}% |' -f $file.Path, $covered, $total, $rate))
}
$output.Add('')
$output.Add('| Report | Assembly | Line coverage | Branch coverage |')
$output.Add('| --- | --- | ---: | ---: |')
$output.AddRange($branches)
$markdown = $output -join [Environment]::NewLine
if ($OutputPath) {
    Set-Content -LiteralPath $OutputPath -Value $markdown -Encoding UTF8
}
Write-Output $markdown
