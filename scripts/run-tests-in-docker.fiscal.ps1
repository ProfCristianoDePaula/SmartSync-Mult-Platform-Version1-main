# =============================================================================
# run-tests-in-docker.fiscal.ps1
# Executa a suíte do Fiscal num container Linux (mesmo padrão do Estoque:
# contorna o Smart App Control do Windows, que bloqueia bins recém-buildados).
# Monta o workspace + socket do Docker (Testcontainers), ryuk desabilitado.
# Uso: .\scripts\run-tests-in-docker.fiscal.ps1
# =============================================================================
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

docker run --rm `
  -v "${root}:/workspace" -w /workspace `
  -v /var/run/docker.sock:/var/run/docker.sock `
  -e TESTCONTAINERS_HOST_OVERRIDE="host.docker.internal" `
  -e TESTCONTAINERS_RYUK_DISABLED="true" `
  -v fiscal_nuget:/root/.nuget/packages `
  mcr.microsoft.com/dotnet/sdk:10.0 `
  bash -c "find tests/Fiscal.Tests -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null; dotnet test tests/Fiscal.Tests -c Release --logger 'console;verbosity=normal'"
