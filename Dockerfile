# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src
COPY . .

RUN dotnet restore bdinfo-cli/bdinfo-cli.csproj
RUN dotnet publish bdinfo-cli/bdinfo-cli.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["./BDInfo"]
