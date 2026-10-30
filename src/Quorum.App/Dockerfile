FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Quorum.App/Quorum.App.csproj src/Quorum.App/
RUN dotnet restore src/Quorum.App/Quorum.App.csproj
COPY src/Quorum.App/ src/Quorum.App/
RUN dotnet publish src/Quorum.App/Quorum.App.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Quorum.App.dll"]
