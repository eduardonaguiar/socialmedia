FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY services/graph-service/GraphService.csproj services/graph-service/
RUN dotnet restore services/graph-service/GraphService.csproj
COPY services/graph-service/ services/graph-service/
RUN dotnet publish services/graph-service/GraphService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y curl \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8082
ENV ASPNETCORE_URLS=http://0.0.0.0:8082
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GraphService.dll"]
