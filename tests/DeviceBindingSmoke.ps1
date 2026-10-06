$ErrorActionPreference = 'Stop'
$sdk = Join-Path $env:LOCALAPPDATA 'FlashFix-dotnet-sdk\dotnet.exe'
$api = Join-Path $PSScriptRoot '..\src\FlashFix.Api\bin\Debug\net10.0\FlashFix.Api.dll'
$database = Join-Path $env:TEMP ("flashfix-device-test-" + [Guid]::NewGuid().ToString('N') + '.db')
$port = Get-Random -Minimum 20000 -Maximum 50000
$base = "http://127.0.0.1:$port"
$adminPassword = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
$start = [Diagnostics.ProcessStartInfo]::new($sdk)
$start.ArgumentList.Add($api)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'
$start.Environment['ASPNETCORE_URLS'] = $base
$start.Environment['FLASHFIX_SQLITE_PATH'] = $database
$start.Environment['FLASHFIX_KEY_HASH_SECRET'] = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$start.Environment['FLASHFIX_BOOTSTRAP_ADMIN_USERNAME'] = 'testadmin'
$start.Environment['FLASHFIX_BOOTSTRAP_ADMIN_PASSWORD'] = $adminPassword
$server = [Diagnostics.Process]::Start($start)

function Post($path, $body, $token) {
    $headers = @{}
    if ($token) { $headers['Authorization'] = "Bearer $token" }
    Invoke-WebRequest -Uri "$base$path" -Method Post -Headers $headers -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress) -SkipHttpErrorCheck
}

function AssertStatus($response, $expected) {
    if ([int]$response.StatusCode -ne $expected) {
        throw "Expected HTTP $expected; received $([int]$response.StatusCode): $($response.Content)"
    }
}

function Proof($username, $purpose, $rsa) {
    $challengeResponse = Post '/v1/auth/device-challenge' @{ username = $username; purpose = $purpose } $null
    AssertStatus $challengeResponse 200
    $challenge = $challengeResponse.Content | ConvertFrom-Json
    $payload = [Convert]::FromBase64String($challenge.payload)
    $signature = $rsa.SignData($payload, [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    @{
        challengeId = $challenge.challengeId
        devicePublicKey = [Convert]::ToBase64String($rsa.ExportSubjectPublicKeyInfo())
        deviceSignature = [Convert]::ToBase64String($signature)
    }
}

try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            if ((Invoke-WebRequest "$base/health" -TimeoutSec 1).StatusCode -eq 200) {
                $ready = $true
                break
            }
        } catch { Start-Sleep -Milliseconds 250 }
    }
    if (-not $ready) { throw "API did not start: $($server.StandardError.ReadToEnd())" }

    $deviceId = [Guid]::NewGuid().ToString('N')
    $adminKey = [Security.Cryptography.RSA]::Create(2048)
    $otherAdminKey = [Security.Cryptography.RSA]::Create(2048)
    $adminProof = Proof 'testadmin' 'login' $adminKey
    $admin = Post '/v1/auth/login' (@{
        username = 'testadmin'; password = $adminPassword; deviceId = $deviceId
    } + $adminProof) $null
    AssertStatus $admin 200
    $adminTokens = $admin.Content | ConvertFrom-Json
    $adminToken = $adminTokens.accessToken
    $otherAdminProof = Proof 'testadmin' 'login' $otherAdminKey
    $otherAdmin = Post '/v1/auth/login' (@{
        username = 'testadmin'; password = $adminPassword
        deviceId = [Guid]::NewGuid().ToString('N')
    } + $otherAdminProof) $null
    AssertStatus $otherAdmin 403
    $adminNoProof = Post '/v1/auth/login' @{
        username = 'testadmin'; password = $adminPassword; deviceId = $deviceId
    } $null
    AssertStatus $adminNoProof 403
    $adminRefreshProof = Proof 'testadmin' 'refresh' $adminKey
    $adminRefresh = Post '/v1/auth/refresh' (@{
        refreshToken = $adminTokens.refreshToken; deviceId = $deviceId
    } + $adminRefreshProof) $null
    AssertStatus $adminRefresh 200
    $adminTokens = $adminRefresh.Content | ConvertFrom-Json
    $adminToken = $adminTokens.accessToken
    $created = Post '/v1/admin/keys' @{ durationDays = 30 } $adminToken
    AssertStatus $created 201
    $createdKey = $created.Content | ConvertFrom-Json
    $key = $createdKey.key

    $first = [Security.Cryptography.RSA]::Create(2048)
    $second = [Security.Cryptography.RSA]::Create(2048)
    $password = 'DeviceTestPassword2026!'
    $registerProof = Proof 'alice' 'register' $first
    $register = Post '/v1/auth/register' (@{
        username = 'alice'; password = $password; key = $key; deviceId = $deviceId
    } + $registerProof) $null
    AssertStatus $register 200
    $userTokens = $register.Content | ConvertFrom-Json
    $userAdminAttempt = Post '/v1/admin/keys' @{ durationDays = 30 } $userTokens.accessToken
    AssertStatus $userAdminAttempt 401

    $malformedProof = Proof 'alice' 'login' $first
    $malformedProof.deviceSignature = 'invalid-base64'
    $malformed = Post '/v1/auth/login' (@{
        username = 'alice'; password = $password; deviceId = $deviceId
    } + $malformedProof) $null
    AssertStatus $malformed 403

    $sameProof = Proof 'alice' 'login' $first
    $same = Post '/v1/auth/login' (@{
        username = 'alice'; password = $password; deviceId = $deviceId
    } + $sameProof) $null
    AssertStatus $same 200
    $sameTokens = $same.Content | ConvertFrom-Json

    $otherProof = Proof 'alice' 'login' $second
    $other = Post '/v1/auth/login' (@{
        username = 'alice'; password = $password; deviceId = $deviceId
    } + $otherProof) $null
    AssertStatus $other 403

    $replay = Post '/v1/auth/login' (@{
        username = 'alice'; password = $password; deviceId = $deviceId
    } + $sameProof) $null
    AssertStatus $replay 403

    $refreshProof = Proof 'alice' 'refresh' $first
    $refreshed = Post '/v1/auth/refresh' (@{
        refreshToken = $sameTokens.refreshToken; deviceId = $deviceId
    } + $refreshProof) $null
    AssertStatus $refreshed 200

    $otherRefreshProof = Proof 'alice' 'refresh' $second
    $otherRefresh = Post '/v1/auth/refresh' (@{
        refreshToken = ($refreshed.Content | ConvertFrom-Json).refreshToken; deviceId = $deviceId
    } + $otherRefreshProof) $null
    AssertStatus $otherRefresh 401

    $reset = Post "/v1/admin/keys/$($createdKey.id)/reset-device" @{
        reason = 'Dispositivo substituído após verificação'
    } $adminToken
    AssertStatus $reset 200
    $oldAccess = Invoke-WebRequest -Uri "$base/v1/auth/me" -Headers @{
        Authorization = "Bearer $($sameTokens.accessToken)"
    } -SkipHttpErrorCheck
    AssertStatus $oldAccess 401

    $replacementProof = Proof 'alice' 'login' $second
    $replacement = Post '/v1/auth/login' (@{
        username = 'alice'; password = $password; deviceId = $deviceId
    } + $replacementProof) $null
    AssertStatus $replacement 200
    'PASS: admin device binding, admin proof on refresh, registration, role isolation, malformed proof, bound login, other device rejection, replay rejection, signed refresh, verified reset'
}
finally {
    if (-not $server.HasExited) { $server.Kill(); $server.WaitForExit() }
    $server.Dispose()
    Remove-Item -LiteralPath $database -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath "$database-shm" -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath "$database-wal" -ErrorAction SilentlyContinue
}
