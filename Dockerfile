FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and restore dependencies
COPY CurrencyConverter.sln ./
COPY CurrencyConverter.API/CurrencyConverter.API.csproj CurrencyConverter.API/
COPY CurrencyConverter.Application/CurrencyConverter.Application.csproj CurrencyConverter.Application/
COPY CurrencyConverter.Domain/CurrencyConverter.Domain.csproj CurrencyConverter.Domain/
COPY CurrencyConverter.Infrastructure/CurrencyConverter.Infrastructure.csproj CurrencyConverter.Infrastructure/

RUN dotnet restore CurrencyConverter.API/CurrencyConverter.API.csproj

# Copy the entire project structure
COPY . .

# Build and publish the API project
WORKDIR /CurrencyConverter.API
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 80
ENTRYPOINT ["dotnet", "CurrencyConverter.API.dll"]
