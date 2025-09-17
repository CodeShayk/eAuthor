# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project file(s) and restore as distinct layers for better caching
COPY ["src/eAuthor.API/eAuthor.API.csproj", "src/eAuthor.API/"]
RUN dotnet restore "src/eAuthor.API/eAuthor.API.csproj"

# Copy the rest of the source
COPY . .

# Build
WORKDIR "/src/src/eAuthor.API"
RUN dotnet build "eAuthor.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "eAuthor.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Kestrel will listen on 8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# If you want to run as non-root, uncomment the next line (aspnet image includes 'app' user)
# USER app

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "eAuthor.API.dll"]