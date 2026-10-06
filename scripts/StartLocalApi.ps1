param([switch]$InitializeAdmin)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiDll = Join-Path $projectRoot 'src\FlashFix.Api\bin\Debug\net10.0\FlashFix.Api.dll'
$sdk = Join-Path $env:LOCALAPPDATA 'FlashFix-dotnet-sdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) {
    $sdk = (Get-Command dotnet -ErrorAction Stop).Source
}
if (-not (Test-Path -LiteralPath $apiDll)) {
    throw 'Compile a API antes de iniciar: dotnet build src/FlashFix.Api/FlashFix.Api.csproj -c Debug'
}

$dataDir = Join-Path $env:LOCALAPPDATA 'FlashFix\dev'
$secretFile = Join-Path $dataDir 'api-secret.dpapi'
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
if (-not (Test-Path -LiteralPath $secretFile)) {
    $value = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $protected = ConvertFrom-SecureString (ConvertTo-SecureString $value -AsPlainText -Force)
    [IO.File]::WriteAllText($secretFile, $protected)
}
$encrypted = [IO.File]::ReadAllText($secretFile)
$secure = ConvertTo-SecureString $encrypted
$secret = [pscredential]::new('FlashFix', $secure).GetNetworkCredential().Password

$port = 5031
$baseUrl = "http://127.0.0.1:$port"
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/health" -TimeoutSec 2
    if ($health.status -eq 'ok') {
        if ($InitializeAdmin) { throw 'A API já está em execução. A inicialização da conta foi cancelada.' }
        Write-Output "API local já está em execução: $baseUrl"
        return
    }
} catch [System.Net.Http.HttpRequestException] { }
catch [System.Net.WebException] { }

$username = 'flashfix_admin'
$password = $null
if ($InitializeAdmin) {
    $password = ([Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$environment = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    ASPNETCORE_URLS = $baseUrl
    FLASHFIX_SQLITE_PATH = (Join-Path $dataDir 'api.db')
    FLASHFIX_KEY_HASH_SECRET = $secret
}
if ($InitializeAdmin) {
    $environment.FLASHFIX_BOOTSTRAP_ADMIN_USERNAME = $username
    $environment.FLASHFIX_BOOTSTRAP_ADMIN_PASSWORD = $password
}
$stdout = Join-Path $dataDir 'api.stdout.log'
$stderr = Join-Path $dataDir 'api.stderr.log'
$process = Start-Process -FilePath $sdk -ArgumentList @($apiDll) -WorkingDirectory $projectRoot `
    -WindowStyle Hidden -Environment $environment -RedirectStandardOutput $stdout `
    -RedirectStandardError $stderr -PassThru

$ready = $false
for ($attempt = 0; $attempt -lt 40; $attempt++) {
    Start-Sleep -Milliseconds 250
    if ($process.HasExited) { break }
    try {
        $health = Invoke-RestMethod -Uri "$baseUrl/health" -TimeoutSec 2
        if ($health.status -eq 'ok') { $ready = $true; break }
    } catch { }
}
if (-not $ready) {
    throw "A API não iniciou. Confira $stderr"
}

if ($InitializeAdmin) {
    $deviceId = [Guid]::NewGuid().ToString('N')
    $body = @{ username = $username; password = $password; deviceId = $deviceId } | ConvertTo-Json -Compress
    try {
        $session = Invoke-RestMethod -Uri "$baseUrl/v1/auth/login" -Method Post `
            -ContentType 'application/json' -Body $body -TimeoutSec 10
        if (-not $session.accessToken) { throw 'A API não confirmou o login.' }
    } catch {
        throw 'A API iniciou, mas não confirmou a conta. Nenhuma senha será exibida.'
    }
    Write-Output "API local: $baseUrl"
    Write-Output "Usuário administrativo: $username"
    Write-Output "Senha inicial: $password"
    Write-Output 'Guarde a senha em um gerenciador de senhas. Ela não é salva em texto claro.'
} else {
    Write-Output "API local iniciada: $baseUrl"
}
