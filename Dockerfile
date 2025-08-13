# Use the official ASP.NET Core runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends gosu && rm -rf /var/lib/apt/lists/*

# Create backup locations for default files that won't be overwritten by a mount
COPY Configuration/ /app_defaults/Configuration/
COPY Plugins/ /app_defaults/Plugins/
COPY Localization/ /app_defaults/Localization/

# Copy all application files to the working directory
COPY . .

# Copy and prepare the startup script
COPY entrypoint.sh .
RUN chmod +x entrypoint.sh

# Set the entrypoint to run the startup script
ENTRYPOINT ["./entrypoint.sh"]