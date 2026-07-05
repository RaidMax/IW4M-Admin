# Plugin data: config, databases, and files

Every IW4MAdmin plugin gets its own isolated folder on disk — `Plugins/<YourPlugin>/` — for its
config files, its own database, and any other files it owns. You never compose paths by hand; the
host derives the folder from your plugin and routes everything into it.

This guide uses the **Credify** plugin as the worked example.

---

## TL;DR

```csharp
public class Plugin : IPluginV2
{
    public static void RegisterDependencies(IServiceCollection services)
    {
        services.AddConfiguration<MyConfiguration>();   // → Plugins/MyPlugin/MyConfiguration.json
        services.AddDatabase<MyDbContext>();            // → Plugins/MyPlugin/MyDbContext.db
    }

    // inject your config (loaded) and a context factory (migrated)
    public Plugin(MyConfiguration config, IDbContextFactory<MyDbContext> dbFactory) { }
}
```

- Folder name comes from your plugin automatically — no key, no path string.
- Configs save/load/hot-reload as before, just inside your folder.
- Databases get a connection string, an isolated factory, automatic migrations, and WAL — for free.
- Want a raw file? Inject `IPluginDataStore<Plugin>` and call `GetPath(...)`.

---

## 1. Configuration

Write a config type and register it. The file lands in *your* plugin folder.

```csharp
public class MyConfiguration : IBaseConfiguration
{
    public int DailyBonus { get; set; } = 100;
    public string Name() => nameof(MyConfiguration);
}

public static void RegisterDependencies(IServiceCollection services)
{
    services.AddConfiguration<MyConfiguration>();          // file = MyConfiguration.json
    // or a custom file name:
    services.AddConfiguration<MyConfiguration>("Settings"); // file = Settings.json
}

public Plugin(MyConfiguration config)
{
    var bonus = config.DailyBonus;   // already loaded from disk (default written on first run)
}
```

The host knows it's *your* config because `MyConfiguration` is defined in your plugin. Save, load,
and hot-reload behave exactly as they did before — only the location changed.

> **Tip (obfuscated/premium plugins):** if your build obfuscates type names, pass an explicit file
> name (`AddConfiguration<MyConfiguration>("Settings")`) so the file name stays stable across builds.
> Your folder name is always stable.

---

## 2. A database of your own

Plugins can own a SQLite database, fully separate from the host's database (the host may run
Postgres or MySQL — yours stays SQLite, with its own migration history, never FK'd into host tables).

### 2.1 Entities

Plain POCOs — your schema, your rules. Store client ids as bare ints; resolve names at runtime via
the host.

```csharp
public class PlayerWallet
{
    public int Id { get; set; }
    public int ClientId { get; set; }   // not an FK into host tables
    public long Balance { get; set; }
}
```

### 2.2 Context

Give the context two constructors: the host calls the options overload at runtime; `dotnet ef` falls
back to the parameterless one at design time, where `OnConfiguring` hands it just the provider shape.

```csharp
public class MyDbContext : DbContext
{
    // Runtime: the host's AddDatabase<MyDbContext> factory passes the sandboxed SQLite options.
    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options) { }

    // Design-time: `dotnet ef` constructs this with no host, no DI.
    public MyDbContext() { }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // Only fires at design time (the runtime options are already configured). No connection string
        // and no throwaway file — `migrations add` never connects; it only needs the provider services
        // to diff your model.
        if (!options.IsConfigured)
            options.UseSqlite();
    }

    public DbSet<PlayerWallet> Wallets => Set<PlayerWallet>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<PlayerWallet>().HasIndex(w => w.ClientId).IsUnique();
    }
}
```

### 2.3 The EF design-time package

Generating migrations uses standard EF tooling (`dotnet ef`), so your plugin references the EF design
package — privately, as a dev-only dependency that never ships at runtime or flows to consumers:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.*" PrivateAssets="all" />
```

That is the **only** design-time plumbing you need. You do not write an
`IDesignTimeDbContextFactory` — with none present, `dotnet ef` constructs your context through its
parameterless constructor (2.2) and `OnConfiguring` supplies the provider. No factory class, no
throwaway `design.db` file.

> **How EF finds your context.** Offline, `dotnet ef` tries an `IDesignTimeDbContextFactory<T>` first,
> then the app's host builder, then the context's own constructor. A plugin library has neither of the
> first two, so tooling falls to the parameterless ctor — which is exactly what 2.2 provides.

### 2.4 Register

One line. The host supplies the path, an isolated factory, migrations, and WAL.

```csharp
public static void RegisterDependencies(IServiceCollection services)
{
    services.AddDatabase<MyDbContext>();   // → Plugins/MyPlugin/MyDbContext.db
}

public Plugin(IDbContextFactory<MyDbContext> dbFactory)
{
    // ready to use; schema already migrated before your OnLoad runs
}

private async Task DoWork(IDbContextFactory<MyDbContext> dbFactory, CancellationToken token)
{
    await using var db = await dbFactory.CreateDbContextAsync(token);
    var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.ClientId == 42, token);
}
```

You do **not** write a connection string, a hand-rolled factory, a migrate-on-load call, or WAL
setup. The host does all of it.

### 2.5 Multiple databases

Allowed. The default file name is the context type; pass a name to disambiguate.

```csharp
services.AddDatabase<MyDbContext>();             // MyDbContext.db
services.AddDatabase<AnalyticsDbContext>("stats"); // stats.db  (same plugin folder)
```

---

## 3. Migrations

Authoring migrations is standard EF — run it at development time against your context:

```bash
dotnet ef migrations add AddWallets \
  -p MyPlugin/MyPlugin.csproj -s MyPlugin/MyPlugin.csproj \
  -c MyPlugin.MyDbContext
```

This produces a `Migrations/` folder that compiles into your plugin DLL. At runtime the host applies
any pending migrations automatically, on startup, before your `OnLoad` — you write no apply code.

Your migration history lives in your own `.db`. It never touches the host's migration history or the
host's database provider.

> If a migration fails, only **your** database is disabled and the failure is logged once — the host
> and other plugins keep running.

---

## 4. Raw files

Your plugin folder is a **real directory you fully own** — build whatever subfolder structure you
need under it. Inject your data directory and resolve paths inside it, using your own plugin type as
the marker:

```csharp
public Plugin(IPluginDataStore<Plugin> data)
{
    var cache  = data.GetPath("cache", "tokens.bin");          // Plugins/MyPlugin/cache/tokens.bin
    var player = data.GetPath("playerdata", "42", "stats.json"); // nested dirs created for you
    // ... read/write freely
}
```

- `RootPath` — your folder, created on first access.
- `GetPath(params string[])` — resolves inside your folder, **creating any intermediate
  directories**; rejects paths that try to escape your folder.
- `EnsureAsset(name, overwrite)` — copies a resource embedded in your DLL out to your folder on first
  run (handy for seeding a default file or a folder of assets); nested resource paths are supported.

You can also push a config or database into a subfolder by putting a path in the name:

```csharp
services.AddDatabase<MyDbContext>("data/wallets");   // → Plugins/MyPlugin/data/wallets.db
services.AddConfiguration<MyConfiguration>("config/main"); // → Plugins/MyPlugin/config/main.json
```

---

## 5. Where it all lands

Your data folder sits in `Plugins/` right next to your DLL. A basic plugin stays flat; a larger one
nests however it likes — both live under the same owned folder:

```
Plugins/
├── MyPlugin.dll                 ← your binary
├── MyPlugin/                    ← your owned data folder
│   ├── MyConfiguration.json
│   ├── MyDbContext.db
│   ├── MyDbContext.db-wal
│   ├── cache/
│   │   └── tokens.bin
│   └── playerdata/
│       └── 42/stats.json
├── Credify.dll
└── Credify/
    ├── CredifyConfiguration.json   (+ the rest)
    └── Credify.db
```

---

## 6. Migrating an existing plugin

When you switch a config from the old shared `Configuration/` folder to `AddConfiguration` routing,
the host moves your existing file into your plugin folder once, on next start. No data loss, nothing
to do by hand.

Databases are not auto-moved (a live SQLite file has `-wal`/`-shm` sidecars). If you already ship a
database in the shared `Database/` folder, point it at the new location yourself before your first
query, or start fresh.

---

## 7. FAQ

**Why is the folder named after my assembly and not my `Name` property?**
Your `Name` ("Simple Stats") is a display string with spaces; the assembly name (`Stats`) is stable,
path-safe, and already how the host identifies your plugin internally. It also stays put under
obfuscation.

**Can I share a config type between two plugins?**
Avoid it. The folder is derived from where the type is *defined*, so a type in a shared library would
route both plugins into the shared library's folder. Define config and context types in your own
plugin.

**Does any of this break my existing plugin?**
No. If you don't call the new APIs, nothing changes. Adoption is opt-in, one registration at a time.
