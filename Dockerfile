# syntax=docker/dockerfile:1.4

# Build stage: publish a self-contained single-file binary for the target platform
ARG TARGETOS
ARG TARGETARCH
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src
COPY . .

# Restore with cache and publish for the appropriate RID mapping for buildx platforms
RUN --mount=type=cache,target=/root/.nuget/packages \
	if [ "${TARGETOS}" = "linux" ]; then \
		if [ "${TARGETARCH}" = "arm64" ] || [ "${TARGETARCH}" = "aarch64" ]; then RID=linux-arm64; else RID=linux-x64; fi; \
	else \
		RID=linux-x64; \
	fi; \
	dotnet restore bdinfo-cli/bdinfo-cli.csproj && \
	dotnet publish bdinfo-cli/bdinfo-cli.csproj -c Release -r ${RID} --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o /app/publish

# Runtime stage: use Debian-based runtime for better compatibility with single-file/ReadyToRun
FROM mcr.microsoft.com/dotnet/runtime:8.0-bullseye-slim AS runtime

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["./bdinfo"]
