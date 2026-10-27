# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["AgriLink/AgriLink.csproj", "AgriLink/"]
RUN dotnet restore "AgriLink/AgriLink.csproj"

# Copy everything else and build release
COPY . .
WORKDIR "/src/AgriLink"
RUN dotnet publish "AgriLink.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AgriLink.dll"]
