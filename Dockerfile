FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /source

COPY ["Instruments/Instruments.csproj", "Instruments/"]
COPY ["Logging/Logging.csproj", "Logging/"]
COPY ["Orders/Orders.csproj", "Orders/"]
COPY ["OrderBook/OrderBook.csproj", "OrderBook/"]
COPY ["MatchingEngine/MatchingEngine.csproj", "MatchingEngine/"]
COPY ["Valkyrie/Valkyrie.csproj", "Valkyrie/"]

RUN dotnet restore "Valkyrie/Valkyrie.csproj"

COPY ["Instruments/", "Instruments/"]
COPY ["Logging/", "Logging/"]
COPY ["Orders/", "Orders/"]
COPY ["OrderBook/", "OrderBook/"]
COPY ["MatchingEngine/", "MatchingEngine/"]
COPY ["Valkyrie/", "Valkyrie/"]

RUN dotnet publish "Valkyrie/Valkyrie.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Valkyrie.dll"]
