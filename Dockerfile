# 1. Imagen base con SDK para compilar la aplicación en .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar el archivo del proyecto y restaurar dependencias
COPY ["LaboratorioConcurrencia.csproj", "./"]
RUN dotnet restore "LaboratorioConcurrencia.csproj"

# Copiar todo el código fuente y publicar en modo Release
COPY . .
RUN dotnet publish "LaboratorioConcurrencia.csproj" -c Release -o /app/out

# 2. Imagen final super liviana para ejecutar la API
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Exponer el puerto 5030 en el contenedor
EXPOSE 5030
ENV ASPNETCORE_URLS=http://+:5030

ENTRYPOINT ["dotnet", "LaboratorioConcurrencia.dll"]# 1. Imagen base con SDK para compilar la aplicación en .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar el archivo del proyecto y restaurar dependencias
COPY ["LaboratorioConcurrencia.csproj", "./"]
RUN dotnet restore "LaboratorioConcurrencia.csproj"

# Copiar todo el código fuente y publicar en modo Release
COPY . .
RUN dotnet publish "LaboratorioConcurrencia.csproj" -c Release -o /app/out

# 2. Imagen final super liviana para ejecutar la API
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Exponer el puerto 5030 en el contenedor
EXPOSE 5030
ENV ASPNETCORE_URLS=http://+:5030

ENTRYPOINT ["dotnet", "LaboratorioConcurrencia.dll"]