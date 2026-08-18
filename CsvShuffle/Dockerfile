FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG APP_VERSION

WORKDIR /src
COPY CsvShuffle.csproj .
COPY Version.props .
RUN dotnet restore CsvShuffle.csproj

COPY . .
RUN dotnet publish CsvShuffle.csproj \
	--configuration Release \
	--output /app/publish \
	--no-restore \
	${APP_VERSION:+-p:Version=${APP_VERSION}} \
	--nologo

FROM nginxinc/nginx-unprivileged:alpine AS runtime

ARG APP_VERSION
LABEL org.opencontainers.image.version=$APP_VERSION

USER root

COPY --from=build --chown=10001:10001 /app/publish/wwwroot/ /usr/share/nginx/html/

RUN chown -R 10001:10001 \
	/var/cache/nginx \
	/var/log/nginx \
	/var/run \
	/usr/share/nginx/html

USER 10001
EXPOSE 8080
