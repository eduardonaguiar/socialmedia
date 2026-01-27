FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY workers/fanout-worker/FanoutWorker.csproj workers/fanout-worker/
RUN dotnet restore workers/fanout-worker/FanoutWorker.csproj
COPY workers/fanout-worker/ workers/fanout-worker/
RUN dotnet publish workers/fanout-worker/FanoutWorker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y curl \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8084
ENV ASPNETCORE_URLS=http://0.0.0.0:8084
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FanoutWorker.dll"]
