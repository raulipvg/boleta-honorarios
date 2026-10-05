FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/GestionIngresosHonorarios.Domain/GestionIngresosHonorarios.Domain.csproj src/GestionIngresosHonorarios.Domain/
COPY src/GestionIngresosHonorarios.Application/GestionIngresosHonorarios.Application.csproj src/GestionIngresosHonorarios.Application/
COPY src/GestionIngresosHonorarios.Infrastructure/GestionIngresosHonorarios.Infrastructure.csproj src/GestionIngresosHonorarios.Infrastructure/
COPY tools/PasswordHashTool/PasswordHashTool.csproj tools/PasswordHashTool/
RUN dotnet restore tools/PasswordHashTool/PasswordHashTool.csproj
COPY src/ src/
COPY tools/PasswordHashTool/ tools/PasswordHashTool/
RUN dotnet publish tools/PasswordHashTool/PasswordHashTool.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
USER app
ENTRYPOINT ["dotnet", "PasswordHashTool.dll"]
