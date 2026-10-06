param(
    [ValidateRange(1, 100)]
    [int]$MaxBudget = 5
)

$ErrorActionPreference = 'Stop'
$strix = Get-Command strix -ErrorAction Stop
$docker = Get-Command docker -ErrorAction SilentlyContinue
if ($null -eq $docker) {
    $dockerExecutable = Join-Path $env:LOCALAPPDATA 'Programs\DockerDesktop\resources\bin\docker.exe'
    if (-not (Test-Path -LiteralPath $dockerExecutable)) {
        throw 'Docker CLI não foi encontrado.'
    }
} else {
    $dockerExecutable = $docker.Source
}
& $dockerExecutable version --format '{{.Server.Version}}' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Docker não está em execução.' }
if (-not $env:LLM_API_KEY) {
    throw 'Configure LLM_API_KEY apenas nesta sessão antes de iniciar o scan.'
}

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$snapshot = Join-Path ([IO.Path]::GetTempPath()) ('FlashFix-Strix-' + [Guid]::NewGuid().ToString('N'))
$snapshotPath = [IO.Path]::GetFullPath($snapshot)
$tempPath = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
if (-not $snapshotPath.StartsWith($tempPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Pasta temporária inválida.'
}

New-Item -ItemType Directory -Path $snapshotPath | Out-Null
try {
    $sourceFiles = Get-ChildItem -LiteralPath (Join-Path $repo 'src') -File -Recurse |
        Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
            $_.Extension -in '.cs', '.csproj', '.xaml', '.json'
        }
    foreach ($file in $sourceFiles) {
        $relative = $file.FullName.Substring($repo.TrimEnd('\').Length + 1)
        $destination = Join-Path $snapshotPath $relative
        New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }

    $env:STRIX_LLM = 'gemini/gemini-3.8-flash'
    $scanArgs = @(
        '-n', '-t', $snapshotPath, '--scan-mode', 'standard', '--scope-mode', 'full',
        '--max-budget', $MaxBudget.ToString(), '--instruction',
        'Revise autenticação, vínculo da licença ao dispositivo, autorização administrativa, rotação de sessão e validação de entradas. O alvo é uma cópia temporária do código. Não execute ações contra serviços externos.'
    )
    & $strix.Source @scanArgs
    if ($LASTEXITCODE -notin 0, 2) { throw "Strix terminou com código $LASTEXITCODE." }
}
finally {
    if (Test-Path -LiteralPath $snapshotPath) {
        Remove-Item -LiteralPath $snapshotPath -Recurse -Force
    }
}
