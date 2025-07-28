FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /src

# Copia o arquivo de projeto e restaura as dependências primeiro.
# Esta camada só será invalidada se o arquivo .csproj mudar.
COPY BunnyUploader/BunnyUploader.csproj ./BunnyUploader/
RUN dotnet restore ./BunnyUploader/BunnyUploader.csproj

# Copia o resto do código-fonte da aplicação.
COPY . .

# Publica a aplicação. Esta camada só será invalidada se os arquivos de código-fonte mudarem.
RUN dotnet publish ./BunnyUploader/BunnyUploader.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build-env /app/publish .

ENTRYPOINT ["dotnet", "BunnyUploader.dll"]