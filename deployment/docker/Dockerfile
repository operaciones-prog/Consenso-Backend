# Dockerfile para E-Supplier
# Compatible con AWS App Runner y ECS Fargate

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["esupplier.csproj", "./"]
RUN dotnet restore "esupplier.csproj"

# Copy source code and build
COPY . .
RUN dotnet build "esupplier.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "esupplier.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install dependencies for Excel reading if needed
RUN apt-get update && \
    apt-get install -y libgdiplus && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN useradd -m -u 1000 esupplier && \
    chown -R esupplier:esupplier /app

# Copy published app
COPY --from=publish /app/publish .

# Switch to non-root user
USER esupplier

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Expose port
EXPOSE 5000

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Start application
ENTRYPOINT ["dotnet", "esupplier.dll"]

