FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY services/post-service/PostService.csproj services/post-service/
RUN dotnet restore services/post-service/PostService.csproj
COPY services/post-service/ services/post-service/
RUN dotnet publish services/post-service/PostService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y curl \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8081
ENV ASPNETCORE_URLS=http://0.0.0.0:8081
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PostService.dll"]
