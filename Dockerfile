# Multi-stage Docker image for fly.io / Render / Azure Container Apps.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY l402-example-aspnet.csproj ./
RUN dotnet restore l402-example-aspnet.csproj

COPY . ./
RUN dotnet publish l402-example-aspnet.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "L402ExampleAspNet.dll"]
