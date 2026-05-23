<#
.SYNOPSIS
    Exercises every Geolocation Fallback Service endpoint against a running instance.

.DESCRIPTION
    Hits /health, the explicit /api/geolocation/{ip} route with a curated list of public
    IPv4 / IPv6 addresses, and the validation-failure cases (loopback, RFC 1918, link-local,
    multicast, unparseable).

    Prints a colored PASS/FAIL line per case with the correlation-id, and exits with the
    number of failed checks so it can be wired into CI.

.PARAMETER BaseUrl
    Root URL of the running service. Defaults to http://localhost:5000.

.EXAMPLE
    cd scripts ; .\smoke-test.ps1
    cd scripts ; .\smoke-test.ps1 -BaseUrl https://localhost:7142
#>

[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = 'Continue'

$happyPath = @(
    @{ Ip = "8.8.8.8";              Note = "Google DNS - US" }
    @{ Ip = "1.1.1.1";              Note = "Cloudflare DNS" }
    @{ Ip = "9.9.9.9";              Note = "Quad9 - CH" }
    @{ Ip = "208.67.222.222";       Note = "OpenDNS - US" }
    @{ Ip = "81.2.69.142";          Note = "UK" }
    @{ Ip = "202.108.22.5";         Note = "China - Baidu" }
    @{ Ip = "200.160.10.0";         Note = "Brazil" }
    @{ Ip = "31.13.64.35";          Note = "Ireland - Facebook" }
    @{ Ip = "13.107.42.14";         Note = "US - Microsoft" }
    @{ Ip = "2001:4860:4860::8888"; Note = "Google IPv6 DNS" }
    @{ Ip = "2606:4700:4700::1111"; Note = "Cloudflare IPv6 DNS" }
)

$invalidIps = @(
    @{ Ip = "127.0.0.1";     Reason = "loopback" }
    @{ Ip = "10.0.0.1";      Reason = "private 10/8" }
    @{ Ip = "192.168.1.1";   Reason = "private 192.168/16" }
    @{ Ip = "172.16.0.1";    Reason = "private 172.16/12" }
    @{ Ip = "169.254.1.1";   Reason = "link-local" }
    @{ Ip = "224.0.0.1";     Reason = "multicast" }
    @{ Ip = "not-an-ip";     Reason = "unparseable" }
)

$script:pass = 0
$script:fail = 0
$failures = @()

function Invoke-Probe {
    param(
        [string]$Path,
        [int]   $ExpectedStatus,
        [string]$Note
    )

    $url = "$BaseUrl$Path"
    $status = 0
    $body   = ''
    $cid    = ''

    try {
        $resp   = Invoke-WebRequest -Uri $url -Method Get -UseBasicParsing -ErrorAction Stop
        $status = [int]$resp.StatusCode
        $body   = $resp.Content
        $cid    = ($resp.Headers['X-Correlation-Id'] | Select-Object -First 1)
    }
    catch [System.Net.WebException] {
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $body   = $reader.ReadToEnd()
            $cid    = $_.Exception.Response.Headers['X-Correlation-Id']
        } else {
            $script:fail++
            $failures += $Path
            Write-Host ("[ FAIL ] {0,-50} network error: {1}" -f $Path, $_.Exception.Message) -ForegroundColor Red
            return
        }
    }
    catch {
        # PowerShell 7 surfaces non-2xx via HttpResponseException.
        $resp = $_.Exception.Response
        if ($resp) {
            $status = [int]$resp.StatusCode
            $body   = $_.ErrorDetails.Message
            try { $cid = ($resp.Headers.GetValues('X-Correlation-Id') | Select-Object -First 1) } catch {}
        } else {
            $script:fail++
            $failures += $Path
            Write-Host ("[ FAIL ] {0,-50} exception: {1}" -f $Path, $_.Exception.Message) -ForegroundColor Red
            return
        }
    }

    $summary = ''
    if ($body) {
        try {
            $obj = $body | ConvertFrom-Json -ErrorAction Stop
            if ($status -eq 200) {
                $summary = "$($obj.country) / $($obj.city)"
            } else {
                $summary = $obj.error
                if ($obj.attempted_providers) {
                    $summary += " [attempted: $($obj.attempted_providers -join ', ')]"
                }
            }
        } catch {
            $summary = ($body -replace '\s+', ' ').Substring(0, [Math]::Min(80, $body.Length))
        }
    }

    if ($status -eq $ExpectedStatus) {
        $script:pass++
        Write-Host ("[ PASS ] {0,-50} {1,3}  {2,-30}  {3}" -f $Path, $status, $summary, $Note) -ForegroundColor Green
    } else {
        $script:fail++
        $failures += $Path
        Write-Host ("[ FAIL ] {0,-50} expected {1} got {2}  cid={3}" -f $Path, $ExpectedStatus, $status, $cid) -ForegroundColor Red
        if ($summary) {
            Write-Host ("         {0}" -f $summary) -ForegroundColor DarkRed
        }
    }
}

Write-Host ""
Write-Host "Geolocation Fallback Service - smoke test"  -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl"                          -ForegroundColor Cyan
Write-Host ""

Write-Host "Health" -ForegroundColor Cyan
Invoke-Probe -Path "/health" -ExpectedStatus 200 -Note ""

Write-Host ""
Write-Host "Happy path (expecting 200)" -ForegroundColor Cyan
foreach ($t in $happyPath) {
    $encoded = [Uri]::EscapeDataString($t.Ip)
    Invoke-Probe -Path "/api/geolocation/$encoded" -ExpectedStatus 200 -Note $t.Note
}

Write-Host ""
Write-Host "Validation failures (expecting 400, no provider call)" -ForegroundColor Cyan
foreach ($t in $invalidIps) {
    $encoded = [Uri]::EscapeDataString($t.Ip)
    Invoke-Probe -Path "/api/geolocation/$encoded" -ExpectedStatus 400 -Note $t.Reason
}

Write-Host ""
$summaryColor = if ($script:fail -eq 0) { 'Green' } else { 'Red' }
Write-Host ("Results: {0} passed, {1} failed" -f $script:pass, $script:fail) -ForegroundColor $summaryColor

if ($script:fail -gt 0) {
    Write-Host ""
    Write-Host "Failed paths:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
}

exit $script:fail
