[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "Low")]
param(
    [switch]$Push,
    [string]$Message
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
Write-Verbose "Using git repository at '$repoRoot'."

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
Write-Verbose "Discovered $($rawTags.Count) tag(s) total; count of those matching semver pattern: $($matchingTags.Count)."

if ($matchingTags.Count -eq 0) {
    $major = 0
    $minor = 0
    $build = 0
    Write-Verbose "No matching version tags found. Starting from v0.0.0."
}
else {
    $latest = $matchingTags |
    Sort-Object Major, Minor, Build -Descending |
    Select-Object -First 1

    $major = $latest.Major
    $minor = $latest.Minor
    $build = $latest.Build
    Write-Verbose "Latest matching tag is '$($latest.Tag)'."
}

$nextBuild = $build + 1
$newTag = "v$major.$minor.$nextBuild"
Write-Verbose "Computed next tag as '$newTag'."

$existing = git tag --list $newTag
if (-not [string]::IsNullOrWhiteSpace($existing)) {
    throw "Tag '$newTag' already exists."
}

if ($WhatIfPreference) {
    Write-Verbose "WhatIf enabled. Skipping tag creation and push."
    $tagMessage = if ($Message) { " with message: $Message" } else { "" }
    Write-Verbose "Would create tag '$newTag'$tagMessage at HEAD."
    Write-Output $newTag
    if ($Push.IsPresent) {
        Write-Verbose "Would push tag '$newTag' to 'origin'."
    }
    return
}

if ($Message) {
    Write-Verbose "Included message: $Message"
}

if ($PSCmdlet.ShouldProcess("HEAD", "Create git tag '$newTag'")) {
    if ([string]::IsNullOrWhiteSpace($Message)) {
        Write-Verbose "Creating lightweight tag '$newTag' at HEAD."
        git tag $newTag HEAD
    }
    else {
        Write-Verbose "Creating annotated tag '$newTag' with message."
        git tag -a $newTag -m $Message HEAD
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create tag '$newTag'."
    }
}

if ($Push.IsPresent) {
    if ($PSCmdlet.ShouldProcess("origin", "Push git tag '$newTag'")) {
        Write-Verbose "Pushing tag '$newTag' to 'origin'."
        git push origin $newTag
        if ($LASTEXITCODE -ne 0) {
            throw "Tag '$newTag' was created locally, but push failed."
        }
    }
}

$headSha = git rev-parse --short HEAD
Write-Output "Created tag $newTag at $headSha"
if (-not $Push.IsPresent) {
    Write-Output "Tag is local only. Push with: git push origin $newTag"
}
else {
    Write-Output "Tag has been pushed to 'origin'."
}
