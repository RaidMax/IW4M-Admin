# 7 Days to Die integration

IW4MAdmin communicates with each 7 Days to Die server directly through its Telnet console. The native
`server-output.log` supplies chat events; no protocol-translating bridge or generated game log is required.

For a server on the same host, set `ManualLogPath` to the absolute path of `server-output.log` as seen by
IW4MAdmin. When IW4MAdmin runs in a container, mount that file or its containing directory read-only.

For a remote server, run [IW4MAdmin-GameLogServer](https://github.com/RaidMax/IW4MAdmin-GameLogServer) on
the game host. Set `GameLogServerUrl` to its HTTP endpoint and set `ManualLogPath` to the absolute
`server-output.log` path on that remote host.

Each entry in `IW4MAdminSettings.json` represents one game server and has its own `IPAddress`, Telnet
`Port`, `Password`, `ManualLogPath`, and optional `GameLogServerUrl`. This supports any mix of local and
remote 7 Days to Die servers.

7DTD Telnet and GameLogServer HTTP traffic are not encrypted. Do not expose either service directly to
the public Internet. For remote hosts, restrict access with firewall rules and carry the traffic over a
private network, VPN, or authenticated encrypted tunnel.

Live Radar reads player coordinates through Telnet and proxies terrain tiles from the official 7DTD Web
Dashboard operated alongside the game server. It does not send player data to a third-party map service.
Enable `WebDashboardEnabled` and `EnableMapRendering` on the game server, create a restricted
web token with access to `web.map`, and configure the dashboard URL, token name, and a local secret-file
path. Mount the secret file read-only when IW4MAdmin runs in a container.
Create token names with letters, digits, or underscores; the 7DTD `webtokens` command rejects hyphens.

For player safety, 7DTD Live Radar endpoints require an authenticated IW4MAdmin webfront session. This
prevents anonymous visitors from retrieving exact player coordinates or explored terrain. Radar requests
are rate limited, and Telnet responses and proxied map tiles have bounded sizes.

Example server entry:

```json
{
  "IPAddress": "203.0.113.10",
  "Port": 8081,
  "Password": "your-telnet-password",
  "RConParserVersion": "7 Days to Die Parser",
  "EventParserVersion": "7 Days to Die Parser",
  "ManualLogPath": "/absolute/path/to/server-output.log",
  "GameLogServerUrl": "http://203.0.113.10:1625",
  "ExternalWeb": {
    "Url": "http://203.0.113.10:8080",
    "TokenName": "iw4m_radar",
    "TokenFile": "/run/secrets/7dtd_dashboard_token"
  }
}
```

Omit `GameLogServerUrl` when IW4MAdmin can read `ManualLogPath` directly. Each 7DTD server can use a
different dashboard URL and token file.
