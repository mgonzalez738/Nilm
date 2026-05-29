# Consulte https://aka.ms/customizecontainer para aprender a personalizar su contenedor de depuración y cómo Visual Studio usa este Dockerfile para compilar sus imágenes para una depuración más rápida.

# Esta fase se usa cuando se ejecuta desde VS en modo rápido (valor predeterminado para la configuración de depuración)
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app


# Esta fase se usa para compilar el proyecto de servicio
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["EnergyMeterWorkerService/EnergyMeterWorkerService.csproj", "EnergyMeterWorkerService/"]
RUN dotnet restore "./EnergyMeterWorkerService/EnergyMeterWorkerService.csproj"
COPY . .
WORKDIR "/src/EnergyMeterWorkerService"
# Agrega -r linux-arm64 como destino
RUN dotnet build "./EnergyMeterWorkerService.csproj" -c $BUILD_CONFIGURATION -r linux-arm64 -o /app/build

# Esta fase se usa para publicar el proyecto de servicio que se copiará en la fase final.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# Agrega -r linux-arm64 como destino
RUN dotnet publish "./EnergyMeterWorkerService.csproj" -c $BUILD_CONFIGURATION -r linux-arm64 -o /app/publish --no-restore

# Esta fase se usa en producción o cuando se ejecuta desde VS en modo normal (valor predeterminado cuando no se usa la configuración de depuración)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EnergyMeterWorkerService.dll"]