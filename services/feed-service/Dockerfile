FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY services/feed-service/FeedService.csproj services/feed-service/
RUN dotnet restore services/feed-service/FeedService.csproj
COPY services/feed-service/ services/feed-service/
RUN dotnet publish services/feed-service/FeedService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y curl \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8083
ENV ASPNETCORE_URLS=http://0.0.0.0:8083
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FeedService.dll"]
