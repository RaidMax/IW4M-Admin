# Writing a web plugin (plugin bundles)

IW4MAdmin plugins can ship their own web pages — Blazor components, CSS, JavaScript, images — that render
in the webfront, **without IW4MAdmin being recompiled** and without the host knowing your CSS classes ahead
of time. A plugin that ships web resources is packaged as a **bundle**: a single `.zip` the host loads
in-memory.

This guide uses the **Credify** plugin as the worked example.

---

## TL;DR

1. Use the Razor SDK, reference the SharedLibraryCore package, set `<IW4MAdminBundle>true</IW4MAdminBundle>`.
2. Write `.razor` pages (`@page`, `@rendermode RenderMode.InteractiveServer`).
3. Register the navbar entry in your plugin's `OnLoad`: `manager.GetPageList().Pages["Name"] = "/route";`
4. Style with Tailwind (`Styles/tailwind.css` → auto-compiled) and/or hand-written CSS in `wwwroot/`.
5. Link your CSS via `<HeadContent>` using `/_content/<id>/<file>`.
6. `dotnet build` → `dist/<id>.zip`.
7. Drop the `.zip` (or an unpacked copy) into the host's `Plugins/` folder.

No packaging script, no hand-written manifest — **build is bundle**.

---

## 1. The project file

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IW4MAdminBundle>true</IW4MAdminBundle>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="RaidMax.IW4MAdmin.SharedLibraryCore" Version="..." />
  </ItemGroup>
</Project>
```

That's the whole project file. **The only bundle-specific line is `<IW4MAdminBundle>true</IW4MAdminBundle>`.**
Referencing the package brings in the MSBuild targets, which on build produce `dist/<id>.zip`.

You don't need `OutputType`, `RazorCompileOnBuild`, `PreserveCompilationContext`, etc. — those are Razor-SDK
defaults. You don't need `PrivateAssets="all"` on the SharedLibraryCore reference — the targets set that for
you so the host-provided assemblies aren't bundled.

Optional extras:
- `<Authors>` / `<Version>` — populate the generated manifest (default to the assembly name / `1.0.0`).
- `<ImplicitUsings>` / `<Nullable>` — your preference, as in any project.
- `<IW4MAdminBundleId>` and other overrides — see the table at the end.

> While the targets are not yet in a published package, import them from a local IW4MAdmin checkout:
> ```xml
> <Import Project="<path-to>/SharedLibraryCore/Packaging/RaidMax.IW4MAdmin.SharedLibraryCore.targets"
>         Condition="Exists('<path-to>/SharedLibraryCore/Packaging/RaidMax.IW4MAdmin.SharedLibraryCore.targets')" />
> ```
> Remove this line once the package ships the targets.

## 2. Pages

A page is a routable Razor component:

```razor
@page "/credify"
@rendermode RenderMode.InteractiveServer
@inject CredifyCache Cache

<PageTitle>Credify</PageTitle>
<CredifyHead/>            @* injects the stylesheet links — see §4 *@

<div class="mx-auto max-w-3xl p-6 text-zinc-100">
    <h1 class="text-2xl font-bold">Credify</h1>
    <StatCard Title="Server bank" Value="@Cache.BankCredits.ToString("N0")"/>
</div>
```

- `@inject` any service your plugin registered in `RegisterDependencies` — pages run in the host's DI scope.
- Multiple pages and shared components are fully supported (see Credify's `Components/` and
  `Components/Shared/`). Use an `_Imports.razor` for common `@using`s. Bundles are not limited to one flat
  page — structure complex plugins with their own component library.

## 3. Navbar registration

The host renders a navbar entry for each registered page. Register in your plugin's `OnLoad` (you have
`IManager` there):

```csharp
private async Task OnLoad(IManager manager, CancellationToken token)
{
    manager.GetPageList().Pages["Credits"] = "/credify";
    // ...
}
```

Sub-pages (e.g. `/credify/leaderboard`) don't each need a navbar entry — link to them from your pages.

## 4. Styling — bring your own CSS pipeline

**The bundle is CSS-agnostic.** It packages whatever is in `wwwroot/` (plus anything you contribute via the
`@(IW4MAdminWebAsset)` item). Compile your CSS with Tailwind, Sass, PostCSS, plain CSS, or nothing — that's
your build step, run before build completes. Tailwind is offered as an optional helper for the common case.

### Hand-written CSS (any tool)

Put `.css` files directly in `wwwroot/` (e.g. `wwwroot/custom.css`). They're bundled untouched and served at
`/_content/<id>/custom.css`. If you run your own compiler (Sass/PostCSS/etc.), just have it output into
`wwwroot/` before build — done.

### Tailwind helper (optional)

Opt in with `<IW4MAdminTailwind>true</IW4MAdminTailwind>` in your `.csproj`. On first build the helper
**scaffolds a correct `Styles/tailwind.css`** for you (already emitting into `layer(plugins)`) — edit its
`@source` to taste. For reference it looks like:

```css
/* Preflight (global reset) is omitted on purpose — the host already ships one. Pull only theme + utilities,
   and emit everything into the host's `plugins` cascade layer (see the critical note below). */
@import "tailwindcss/theme.css" layer(plugins);
@import "tailwindcss/utilities.css" layer(plugins);

/* scan your components so only the classes you actually use are emitted */
@source "../Components/**/*.razor";
```

> **⚠️ #1 rule — emit into `layer(plugins)`.** The host reserves a lowest-priority cascade layer named
> `plugins`. Your Tailwind generates generic utilities (`.hidden`, `.flex`, …) that share names with the
> host's. If you emit them into Tailwind's default `utilities` layer, your stylesheet loads *after* the host's
> and your `.hidden` will out-order the host's responsive `.md:flex`, **collapsing the host sidebar/top bar**
> on your page. Emitting into `layer(plugins)` keeps your styles fully working on your own markup while
> guaranteeing they can never override host chrome. Always use `layer(plugins)`.
>
> **The helper guards this for you:** it scaffolds the file with `layer(plugins)` already set, and emits build
> warning **`IW4M1001`** if a hand-edited input drops it — so the footgun is hard to hit. The rule above is
> *why* it matters.

On build, the helper downloads the standalone Tailwind CLI (cached outside the project, once) and compiles
this — **purged to just the classes you use** — into `obj/` (keeping your source tree clean), then adds it to
the bundle as `wwwroot/plugin.css`. The host serves it and never needs to know your classes. Use host design
tokens for theme-matching, e.g. `text-[color:var(--color-action-primary)]`. Don't opt in and nothing
Tailwind-related runs.

> **Purge caveat:** only classes that appear *literally* in your `.razor` are kept. Fully dynamic class
> strings (`$"text-{color}-500"`) get purged — list them in a Tailwind `@source inline(...)` / safelist.

Tailwind and hand-written CSS coexist — Credify ships both (`plugin.css` + `custom.css`).

### Linking your stylesheets

Inject `<link>` tags into the host `<head>` via `<HeadContent>` (works because the host renders `<HeadOutlet/>`).
Put them in one shared component rendered by each page:

```razor
@* Components/Shared/CredifyHead.razor *@
<HeadContent>
    <link rel="stylesheet" href="/_content/credify/plugin.css"/>
    <link rel="stylesheet" href="/_content/credify/custom.css"/>
</HeadContent>
```

`/_content/<id>/...` is where the host serves your `wwwroot/`. `<id>` is your bundle id (default: the
assembly name; matching is case-insensitive). Use plain URLs — not the fingerprinted `Assets[...]` helper.

> **One `<HeadContent>` per page.** `HeadContent` is *last-render-wins*, not additive. If a page renders a
> shared head component (which has its own `<HeadContent>`) **and** a second `<HeadContent>`, the second
> replaces the first and the earlier `<link>`s silently vanish. Put **all** of a page's `<link>`s in a single
> `<HeadContent>` (or one shared component). A page that needs extra stylesheets beyond the shared set should
> inline them all in one `<HeadContent>` rather than adding a second.

## 5. Bundle layout & the manifest

Build produces `bin/<Configuration>/<id>.zip` (alongside where a `.nupkg` would go):

```
manifest.json     generated from your csproj metadata — do not hand-write
lib/              your plugin dll + any private dependencies
wwwroot/          served at /_content/<id>/   (your CSS output + any static assets)
gsc/              optional game-side scripts (see §7)
```

Everything generated lands under `bin/`/`obj/` (and the Tailwind CLI in a shared cache), so your source tree
and `.gitignore` stay clean.

The **manifest is generated** from your project (`id`, `name`, `version`, `author`, `entryAssembly`), so
there is no second source of truth to keep in sync with `IPluginV2`.

## 6. Private dependencies (complex plugins)

Private dependencies shipped in `lib/` are resolved from the bundle **in-memory** by a dedicated load
context. Assemblies the host already provides (SharedLibraryCore and its closure, Data, the runtime) are
delegated to the host so shared types — including `IPluginV2` — keep one identity and DI matches. The
SharedLibraryCore reference is made private automatically, so its closure is never bundled.

> A plugin that only uses host-provided dependencies (the common case) bundles just its own dll. Bundling
> *additional* private NuGet dependencies that the host does not ship is a follow-up — it needs the
> host-provided exclusion list to avoid re-bundling the host's closure. Until then, such a plugin should
> ship those deps alongside, or they must already be provided by the host.

## 7. Game scripts (GSC)

Put game-side scripts in a `gsc/` folder; they're carried in the bundle and, if the host's
`PluginGscExtractPath` is configured, extracted there on load. GSC is the only bundle content written to
disk (it has to reach the game server); the plugin dll and web assets stay in memory.

## 8. Installing

Drop `bin/<Configuration>/<id>.zip` into the host's `Plugins/` folder and restart. For local development you
can instead drop an **unpacked** copy (a folder containing `manifest.json` at its root) — same result, no
re-zip per iteration.

## Overridable MSBuild properties

| Property | Default |
|---|---|
| `IW4MAdminBundle` | (unset) — set `true` to enable bundling |
| `IW4MAdminBundleId` | `$(AssemblyName)` |
| `IW4MAdminBundleOutput` | `bin\$(Configuration)\` |
| `IW4MAdminWebRoot` | `$(MSBuildProjectDirectory)\wwwroot` |
| `IW4MAdminGscRoot` | `$(MSBuildProjectDirectory)\gsc` |
| `IW4MAdminTailwind` | (unset) — set `true` to enable the Tailwind helper |
| `IW4MAdminTailwindInput` | `$(MSBuildProjectDirectory)\Styles\tailwind.css` |
| `IW4MAdminTailwindOutput` | `obj\…\iw4m-tailwind\plugin.css` (added to the bundle as `wwwroot/plugin.css`) |

Advanced: contribute extra files to the bundle's `wwwroot/` from any tool by adding
`<IW4MAdminWebAsset Include="path" ><BundlePath>sub/path.css</BundlePath></IW4MAdminWebAsset>` items (this is
how the Tailwind helper injects its output).

## Backward compatibility

Plugins that ship as a plain `.dll` (no bundle) load exactly as before — the bundle path is additive. You
only need a bundle if you ship web resources.

---

## FAQ & troubleshooting

Common pitfalls, phrased by symptom. Most map to a one-line fix.

### Layout & CSS

**Q: My plugin page renders without the host top bar / sidebar — just my content on a bare page.**
A: A CSS cascade-layer conflict. Your Tailwind emitted generic utilities (`.hidden`, `.flex`, …) into the
default `utilities` layer; loading after the host, your `.hidden` out-orders the host's responsive `.md:flex`
and collapses every `hidden md:flex` chrome element (sidebar, top bar). **Fix:** emit your Tailwind into the
host's `plugins` layer — `@import "tailwindcss/utilities.css" layer(plugins);`. The Tailwind helper scaffolds
this for you and warns (`IW4M1001`) if it's missing.

**Q: It looks fine with `ASPNETCORE_ENVIRONMENT=Development` but breaks without it.**
A: In development the host serves `wwwroot/css/app.css`; in production it serves the minified
`wwwroot/css/app.min.css` — a *separate* artifact. Both must declare `@layer plugins;`. This is a host-side
file: if you maintain IW4MAdmin, regenerate `app.min.css` from `src/app.css` after CSS changes. As a plugin
author you don't touch it, but it's the usual reason "works in dev, breaks in prod."

**Q: Some of my `<link>` stylesheets don't load / styles randomly missing.**
A: `<HeadContent>` is **last-render-wins, not additive**. If your page renders a shared head component
(which has its own `<HeadContent>`) *and* a second `<HeadContent>`, the second replaces the first and the
earlier links vanish. **Fix:** put **all** of a page's `<link>`s in a single `<HeadContent>`.

**Q: I get build warning `IW4M1001`.**
A: Your `Styles/tailwind.css` doesn't emit into `layer(plugins)`. See the first question — add `layer(plugins)`
to your `@import`s.

**Q: A Tailwind class I use isn't in the output.**
A: Tailwind purges anything not found *literally* in your `.razor`. Dynamic strings like `$"text-{c}-500"`
are dropped. Add them to a safelist / `@source inline(...)`.

**Q: Can I use plain CSS / Sass instead of Tailwind?**
A: Yes. The bundle is tool-agnostic — drop any `.css` into `wwwroot/` (served at `/_content/<id>/…`). Bespoke
class names (e.g. `.myplugin-card`) don't collide with the host, so they don't need a layer. Only *Tailwind's
generic utilities* need `layer(plugins)`.

### Navigation

**Q: Two of my navbar entries both show as "active".**
A: The host navbar uses **prefix** matching, so `/credify` lights up on `/credify/anything`. **Fix:** register
a **single** host entry (`GetPageList().Pages["MyPlugin"] = "/myplugin";`) and ship your own in-page sub-nav
using `<NavLink … Match="NavLinkMatch.All">` for exact active state. Cleaner for multi-page plugins anyway.

### Auth & services

**Q: How do I get the logged-in user in my plugin page?**
A: `[CascadingParameter] Task<AuthenticationState>` (a framework type — no WebfrontCore reference needed).
`ClaimTypes.Sid` = the client id, `ClaimTypes.Role` = permission level. Use `<AuthorizeView>` (or an
`Identity.IsAuthenticated` check) for the logged-out state. Do **not** try to inject WebfrontCore's `AppState`
— plugins reference only SharedLibraryCore.

**Q: `CS1503: cannot convert 'Data.Models.Client.EFClient' to 'SharedLibraryCore.Database.Models.EFClient'`.**
A: There are two `EFClient` types. The **runtime** one — `SharedLibraryCore.Database.Models.EFClient` — is
what `IManager`, `IEntityService<EFClient>`, and most services use. Import `SharedLibraryCore.Database.Models`,
not `Data.Models.Client`.

**Q: How do I resolve a client id to an `EFClient`?**
A: Prefer the live one (`IManager.GetActiveClients().FirstOrDefault(c => c.ClientId == id)`) so in-memory state
stays consistent with the game server; fall back to `IEntityService<EFClient>.Get(id)` for offline users.

### Build & bundle

**Q: What's the minimum `.csproj`?**
A: `Sdk="Microsoft.NET.Sdk.Razor"` + `<TargetFramework>` + the SharedLibraryCore `PackageReference` +
`<IW4MAdminBundle>true</IW4MAdminBundle>`. That's it — `OutputType`, Razor-compile flags, and
`PrivateAssets` are handled for you.

**Q: Where does the bundle end up?**
A: `bin/<Configuration>/<id>.zip`. Generated CSS goes to `obj/`; the Tailwind CLI is cached outside the
project. Nothing generated lands in your source tree, so `.gitignore` needs no extra entries.

**Q: Why isn't my image / nested asset served?**
A: Everything under `wwwroot/` is served at `/_content/<id>/…`, including nested folders
(`wwwroot/img/x.png` → `/_content/<id>/img/x.png`). Reference plain URLs — not the fingerprinted `Assets[...]`
helper. `<id>` is your bundle id (default: assembly name; matched case-insensitively).

**Q: My plugin has its own NuGet dependencies.**
A: They're bundled into `lib/` and resolved in-memory; host-provided assemblies are excluded automatically.
(Bundling private NuGet deps the host doesn't ship is still being finalised — see §6.)

### Interactivity & JavaScript

**Q: Does interactivity (`@rendermode InteractiveServer`) work for bundle pages?**
A: Yes — bundle assemblies load into the default load context exactly like built-in pages, so routing, the
host layout, and interactive rendering all work. Buttons/`@onclick`, live updates, etc. are fully supported.

**Q: How do I use custom JavaScript?**
A: Ship a `.js` (ES module) in `wwwroot/`, then from your component
`var m = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/<id>/your.js");` and call its exports.
Dispose the module reference in `DisposeAsync` (catch `JSDisconnectedException`).

### Host-maintainer notes

- The `plugins` cascade layer must be declared in **both** `WebfrontCore/wwwroot/css/src/app.css` *and* the
  committed `wwwroot/css/app.min.css` (prod). A plugin cannot self-order its layer below the host's — the host
  must declare `plugins` first — which is why this lives host-side.
- `app.min.css` is a hand-committed bundle (`bundleconfig.json`; CI copies it). Regenerate it when host CSS
  changes, or the dev/prod divergence above reappears.
