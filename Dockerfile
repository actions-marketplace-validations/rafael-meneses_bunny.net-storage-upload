FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BunnyUploader/BunnyUploader.csproj ./BunnyUploader/
WORKDIR /src/BunnyUploader
RUN dotnet restore

WORKDIR /src
COPY . .
RUN dotnet publish BunnyUploader/BunnyUploader.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BunnyUploader.dll"]