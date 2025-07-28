FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

 Copia todo o código fonte para o diretório de trabalho
COPY . .

RUN dotnet restore ./BunnyUploader/BunnyUploader.csproj

 Publica o projeto específico, usando caminhos explícitos a partir da raiz
RUN dotnet publish ./BunnyUploader/BunnyUploader.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BunnyUploader.dll"]