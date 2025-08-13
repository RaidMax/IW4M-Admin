FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends gosu && rm -rf /var/lib/apt/lists/*

COPY Configuration/ /app_defaults/Configuration/
COPY Plugins/ /app_defaults/Plugins/
COPY Localization/ /app_defaults/Localization/

COPY . .

RUN chmod +x entrypoint.sh

HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD test "$(curl -s -o /dev/null -w '%{http_code}' http://localhost:1624/api/info)" = "200"

ENTRYPOINT ["./entrypoint.sh"]
