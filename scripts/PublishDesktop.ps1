param([ValidateSet('win-x64','win-x86','win-arm64')][string]$Runtime = 'win-x64')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sdk = Join-Path $env:LOCALAPPDATA 'FlashFix-dotnet-sdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) {
    $sdk = (Get-Command dotnet -ErrorAction Stop).Source
}
$output = Join-Path $root "dist\FlashFix.Desktop\$Runtime"
$project = Join-Path $root 'src\FlashFix.Desktop\FlashFix.Desktop.csproj'
& $sdk publish $project -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=false -p:WindowsPackageType=None -o $output --nologo
if ($LASTEXITCODE -ne 0) { throw 'A publicação do aplicativo falhou.' }

$required = @('FlashFix.Desktop.exe','FlashFix.Desktop.pri','App.xbf','MainWindow.xbf',
    'Design\Theme.xbf','Pages\AuthPage.xbf','Assets\AppIcon.ico')
foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $output $file))) {
        throw "A publicação está incompleta: $file"
    }
}
$exe = Join-Path $output 'FlashFix.Desktop.exe'
$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
Write-Output "EXE: $exe"
Write-Output "SHA-256: $hash"
Write-Output 'Distribua a pasta inteira. O EXE depende dos arquivos WinUI ao lado.'
