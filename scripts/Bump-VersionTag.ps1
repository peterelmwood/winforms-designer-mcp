param(
    [switch]$Push
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "git was not found on PATH."
}

$repoRoot = git rev-parse --show-toplevel 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
    throw "Current directory is not inside a git repository."
}

$rawTags = git tag --list "v*"
$matchingTags = @()

foreach ($tag in $rawTags) {
    if ($tag -match "^v(?<major>\d+)\.(?<minor>\d+)\.(?<build>\d+)$") {
        $matchingTags += [PSCustomObject]@{
            Tag   = $tag
            Major = [int]$Matches["major"]
            Minor = [int]$Matches["minor"]
            Build = [int]$Matches["build"]
        }
    }
}

if ($matchingTags.Count -eq 0) {
    $major = 0
    $minor = 0
    $build = 0
}
else {
    $latest = $matchingTags |
    Sort-Object Major, Minor, Build -Descending |
    Select-Object -First 1

    $major = $latest.Major
    $minor = $latest.Minor
    $build = $latest.Build
}

$nextBuild = $build + 1
$newTag = "v$major.$minor.$nextBuild"

$existing = git tag --list $newTag
if (-not [string]::IsNullOrWhiteSpace($existing)) {
    throw "Tag '$newTag' already exists."
}

git tag $newTag HEAD
if ($LASTEXITCODE -ne 0) {
    throw "Failed to create tag '$newTag'."
}

if ($Push.IsPresent) {
    git push origin $newTag
    if ($LASTEXITCODE -ne 0) {
        throw "Tag '$newTag' was created locally, but push failed."
    }
}

$headSha = git rev-parse --short HEAD
Write-Output "Created tag $newTag at $headSha"
if (-not $Push.IsPresent) {
    Write-Output "Tag is local only. Push with: git push origin $newTag"
}
