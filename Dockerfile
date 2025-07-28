FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BunnyUploader/BunnyUploader.csproj ./BunnyUploader/

RUN dotnet restore ./BunnyUploader/BunnyUploader.csproj

COPY ./BunnyUploader ./BunnyUploader

WORKDIR /src/BunnyUploader

RUN dotnet publish BunnyUploader.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BunnyUploader.dll"]