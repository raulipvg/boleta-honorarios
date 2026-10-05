FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY GestionIngresosHonorarios.sln ./
COPY src/GestionIngresosHonorarios.Domain/GestionIngresosHonorarios.Domain.csproj src/GestionIngresosHonorarios.Domain/
COPY src/GestionIngresosHonorarios.Application/GestionIngresosHonorarios.Application.csproj src/GestionIngresosHonorarios.Application/
COPY src/GestionIngresosHonorarios.Infrastructure/GestionIngresosHonorarios.Infrastructure.csproj src/GestionIngresosHonorarios.Infrastructure/
COPY src/GestionIngresosHonorarios.Api/GestionIngresosHonorarios.Api.csproj src/GestionIngresosHonorarios.Api/
RUN dotnet restore src/GestionIngresosHonorarios.Api/GestionIngresosHonorarios.Api.csproj
COPY src/ src/
RUN dotnet publish src/GestionIngresosHonorarios.Api/GestionIngresosHonorarios.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
USER app
ENTRYPOINT ["dotnet", "GestionIngresosHonorarios.Api.dll"]
