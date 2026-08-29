# ---------- Stage 1: build ----------
# Imagem do SDK apenas para restaurar e publicar.
# https://aka.ms/containerimages
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia apenas os csproj para aproveitar o cache de camadas do Docker
# (restore só roda de novo quando as dependências mudam).
COPY ["Identity.slnx", "./"]
COPY ["src/Identity.Api/Identity.Api.csproj", "src/Identity.Api/"]
COPY ["src/Identity.Application/Identity.Application.csproj", "src/Identity.Application/"]
COPY ["src/Identity.Domain/Identity.Domain.csproj", "src/Identity.Domain/"]
COPY ["src/Identity.Infrastructure/Identity.Infrastructure.csproj", "src/Identity.Infrastructure/"]
RUN dotnet restore "src/Identity.Api/Identity.Api.csproj"

# Copia o código-fonte restante e publica sem re-restaurar.
COPY src/ ./src/
WORKDIR /src/src/Identity.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---------- Stage 2: runtime ----------
# Imagem somente runtime (sem SDK), enxuta.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl entra para o healthcheck do container (compose) — sem gastar peso de SDK.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Usuário não-root (UID 1654 é o padrão "app" das imagens .NET 10).
# Toda a aplicação roda como este usuário — nunca root no container.
# Pastas de chaves RSA (volume jwtkeys) e de logs do Serilog precisam ser
# graváveis por ele.
RUN mkdir -p /app/keys /app/logs && chown -R $APP_UID:$APP_UID /app/keys /app/logs

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "Identity.Api.dll"]