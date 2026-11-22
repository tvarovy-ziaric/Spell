FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/geometry/ shared/geometry/
COPY contracts/spell_v1.proto contracts/
COPY server/ server/
RUN dotnet publish server/Spell.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER app
ENTRYPOINT ["dotnet", "Spell.Server.dll"]
