# syntax=docker/dockerfile:1

# ---------- Etapa 1: compilación ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /origen

COPY Directory.Build.props ./
COPY src/Backend.Domain/Backend.Domain.csproj src/Backend.Domain/
COPY src/Backend.Application/Backend.Application.csproj src/Backend.Application/
COPY src/Backend.Infrastructure/Backend.Infrastructure.csproj src/Backend.Infrastructure/
COPY src/Backend.Presentation.Api/Backend.Presentation.Api.csproj src/Backend.Presentation.Api/

RUN dotnet restore src/Backend.Presentation.Api/Backend.Presentation.Api.csproj

COPY src/ src/

RUN dotnet publish src/Backend.Presentation.Api/Backend.Presentation.Api.csproj \
    -c Release -o /app/publicado --no-restore

# ---------- Etapa 2: ejecución sin privilegios ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publicado .

# Almacén de fotografías del catálogo (Almacen:RutaBase). Debe existir y ser
# escribible por el usuario sin privilegios `app`; en compose se monta un volumen.
RUN mkdir -p /var/lib/reservas && chown -R app:app /var/lib/reservas

# La imagen oficial ya incluye el usuario sin privilegios `app` (uid/gid 1654).
USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "Backend.Presentation.Api.dll"]
