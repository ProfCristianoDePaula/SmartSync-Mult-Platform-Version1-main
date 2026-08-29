# Executa a suíte de integração dentro de um container Linux (SDK .NET 10).
#
# Por quê: o Smart App Control do Windows (política WDAC, 0x800711C7) bloqueia o
# carregamento de assemblies recém-buildados no bin de testes (Etapa 20/21). A
# política não pode ser desativada via registro/Defender — a única alternativa
# sem tocar no SO é rodar os testes num ambiente Linux. Este script monta o
# projeto e o socket do Docker (para o Testcontainers subir o Postgres real) e
# roda o `dotnet test` lá dentro.
#
# Uso:
#   .\scripts\run-tests-in-docker.ps1                      # suíte completa
#   .\scripts\run-tests-in-docker.ps1 -Filter "SocialAuth" # filtro por nome
#   .\scripts\run-tests-in-docker.ps1 -Image mcr.microsoft.com/dotnet/sdk:10.0
param(
    [string]$Filter = "",
    [string]$Image = "mcr.microsoft.com/dotnet/sdk:10.0"
)

$root = Split-Path -Parent $PSScriptRoot
$testArgs = "test tests/Identity.Tests --nologo -v q"
if ($Filter) { $testArgs += " --filter '$Filter'" }

Write-Host "=== Rodando testes no container $Image (Smart App Control bypass) ===" -ForegroundColor Cyan

docker run --rm `
    -e TESTCONTAINERS_RYUK_DISABLED=true `
    -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal `
    --add-host host.docker.internal:host-gateway `
    -v /var/run/docker.sock:/var/run/docker.sock `
    -v "${root}:/app" `
    -w /app `
    $Image `
    bash -c "dotnet $testArgs"
