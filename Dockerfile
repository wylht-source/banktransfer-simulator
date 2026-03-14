# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY BankingApi.sln .
COPY src/BankingApi.API/BankingApi.API.csproj src/BankingApi.API/
COPY src/BankingApi.Application/BankingApi.Application.csproj src/BankingApi.Application/
COPY src/BankingApi.Domain/BankingApi.Domain.csproj src/BankingApi.Domain/
COPY src/BankingApi.Infrastructure/BankingApi.Infrastructure.csproj src/BankingApi.Infrastructure/
COPY tests/BankingApi.Tests/BankingApi.Tests.csproj tests/BankingApi.Tests/

# Restore dependencies
RUN dotnet restore

# Copy all source code
COPY . .

# Build and publish
RUN dotnet publish src/BankingApi.API/BankingApi.API.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "BankingApi.API.dll"]
