# ---- Build ------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy only the project files first, restore, THEN copy the source. Docker caches
# each layer, so editing a .cs file no longer invalidates the restore — which is
# the slowest step by far.

COPY src/NovaWallet.Domain/*.csproj          src/NovaWallet.Domain/
COPY src/NovaWallet.Application/*.csproj     src/NovaWallet.Application/
COPY src/NovaWallet.Infrastructure/*.csproj  src/NovaWallet.Infrastructure/
COPY src/NovaWallet.Api/*.csproj             src/NovaWallet.Api/

RUN dotnet restore src/NovaWallet.Api/NovaWallet.Api.csproj

COPY src/ src/

RUN dotnet publish src/NovaWallet.Api/NovaWallet.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---- Runtime ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Run as a non-root user. The aspnet images ship with a uid 64198 'app' account
# on Alpine; creating our own keeps it explicit and portable.
RUN adduser --disabled-password --no-create-home --uid 10001 novawallet
USER novawallet

COPY --from=build --chown=novawallet:novawallet /app/publish .

# Plain HTTP inside the container; TLS terminates at the ingress in a real
# deployment, and self-signed certs would only complicate `docker compose up`.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

ENTRYPOINT ["dotnet", "NovaWallet.Api.dll"]