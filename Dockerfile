FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BunnyUploader/BunnyUploader.csproj ./BunnyUploader/
WORKDIR /src/BunnyUploader
RUN dotnet restore

WORKDIR /src

FROM build AS publish
COPY . .
RUN dotnet publish BunnyUploader/BunnyUploader.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "BunnyUploader.dll"]