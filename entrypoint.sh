#!/bin/sh

#
# --- Branding ---
#
cat << "EOF"
  _______          ___  _   __  __             _           _       
 |_   _\ \        / / || | |  \/  |   /\      | |         (_)      
   | |  \ \  /\  / /| || |_| \  / |  /  \   __| |_ __ ___  _ _ __  
   | |   \ \/  \/ / |__   _| |\/| | / /\ \ / _` | '_ ` _ \| | '_ \ 
  _| |_   \  /\  /     | | | |  | |/ ____ \ (_| | | | | | | | | | |
 |_____|   \/  \/      |_| |_|  |_/_/    \_\__,_|_| |_| |_|_|_| |_|
                                                                   
EOF

echo
echo "Brought to you by RaidMax"
echo "-------------------------"

#
# --- User & Group Setup ---
#
USER_ID=${PUID:-0}
GROUP_ID=${PGID:-0}

# If the provided user ID is not root (0), create a user
if [ "$USER_ID" -ne 0 ]; then
    echo "Running as user: $USER_ID:$GROUP_ID"

    addgroup --gid "$GROUP_ID" appgroup
    adduser --system --uid "$USER_ID" --gid "$GROUP_ID" --shell /bin/bash appuser
    
    chown -R appuser:appgroup /app /app_defaults
    exec gosu appuser "$0" "$@"
fi

#
# --- File & Directory Checks ---
#
CONFIG_DIR="/app/Configuration"
PLUGINS_DIR="/app/Plugins"
LOCALIZATION_DIR="/app/Localization"

if [ ! -f "$CONFIG_DIR/IW4MAdminSettings.json" ]; then
    echo "FATAL ERROR: IW4MAdminSettings.json not found in your mounted Configuration directory." >&2
    echo "Please create this file or copy it into your local configuration folder before starting." >&2
    sleep 5
    exit 1
fi

if [ ! -f "$CONFIG_DIR/LoggingConfiguration.json" ]; then
    echo "Default configuration files not found, populating..."
    cp -n /app_defaults/Configuration/* "$CONFIG_DIR/"
fi

if [ ! -f "$PLUGINS_DIR/Stats.dll" ]; then
    echo "Default plugins not found, populating..."
    cp -n -r /app_defaults/Plugins/* "$PLUGINS_DIR/"
fi

if [ ! -f "$LOCALIZATION_DIR/IW4MAdmin.en-US.json" ]; then
    echo "Default localization files not found, populating..."
    cp -n -r /app_defaults/Localization/* "$LOCALIZATION_DIR/"
fi

#
# --- Start Application ---
#
echo "Configuration verified. Starting IW4MAdmin..."
exec dotnet Lib/IW4MAdmin.dll
