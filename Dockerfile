# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/FamilyHub.Api/FamilyHub.Api.csproj src/FamilyHub.Api/
COPY src/FamilyHub.Client/FamilyHub.Client.csproj src/FamilyHub.Client/
RUN dotnet restore src/FamilyHub.Api/FamilyHub.Api.csproj

COPY src/ src/
RUN dotnet publish src/FamilyHub.Api/FamilyHub.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "FamilyHub.Api.dll"]
