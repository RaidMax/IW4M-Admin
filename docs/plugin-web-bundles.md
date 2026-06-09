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
5. That's it for CSS — the host links every bundle stylesheet globally; no `<HeadContent>` needed.
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
**scaffolds a correct `Styles/tailwind.css`** for you — edit its `@source` to taste. For reference it looks
like:

```css
/* Preflight (global reset) is omitted on purpose — the host already ships one. Pull only theme + utilities,
   emitted UNLAYERED — the host scopes this stylesheet to your plugin's own DOM (see note below). */
@import "tailwindcss/theme.css";
@import "tailwindcss/utilities.css";

/* scan your components so only the classes you actually use are emitted */
@source "../Components/**/*.razor";
```

> **Host design tokens are injected for you.** The helper prepends the host's published token vocabulary
> (`SharedLibraryCore/Packaging/iw4madmin-theme.css`) ahead of your input at build, so utilities like
> `bg-surface`, `border-line`, `text-subtle`, `text-primary`, `bg-success/10` resolve to the host's runtime
> theme — your pages track the user's theme automatically, with no setup. This is what makes your stylesheet
> **self-contained**: it generates *every* class your components use, rather than leaning on the host's
> (purged) stylesheet to happen to provide one. (Opacity modifiers on these tokens collapse to the base
> colour — same as in the host's own build, because the values are runtime variables.)

> **The host scopes your CSS for you — emit unlayered.** When the host serves a plugin's stylesheet it
> rewrites it with the native CSS `@scope` at-rule so every rule applies **only inside your plugin's own
> rendered DOM** (the host stamps a `data-iw4m-plugin="<id>"` marker around your pages, render-slot widgets
> and — see below — modals). Your generic utilities (`.hidden`, `.flex`, `.sm:flex-row`, …) therefore can't
> touch the host chrome no matter what, *and* — because they're unlayered — they keep full power on your own
> markup, overriding the host's base utilities where you need to (e.g. `flex flex-col sm:flex-row`).
>
> **Do not wrap them in `layer(plugins)`.** The scope already keeps your CSS off the chrome; a lower cascade
> layer adds nothing there and *does* stop your utilities overriding the host's base utilities on your own
> pages, so your responsive/state layout silently fails (cards collapse to one column, `md:`-only content
> stays hidden, `hover:` is dead). The helper scaffolds the unlayered form and emits build warning
> **`IW4M1001`** if a hand-edited input uses `layer(plugins)`.

On build, the helper downloads the standalone Tailwind CLI (cached outside the project, once) and compiles
this — **purged to just the classes you use** — into `obj/` (keeping your source tree clean), then adds it to
the bundle as `wwwroot/plugin.css`. The host serves it and never needs to know your classes. Use host design
tokens for theme-matching, e.g. `text-[color:var(--color-action-primary)]`. Don't opt in and nothing
Tailwind-related runs.

> **Purge caveat:** only classes that appear *literally* in your `.razor` are kept. Fully dynamic class
> strings (`$"text-{color}-500"`) get purged — list them in a Tailwind `@source inline(...)` / safelist.

Tailwind and hand-written CSS coexist — Credify ships both (`plugin.css` + `custom.css`).

### Stylesheets load automatically — do not link them yourself

The host links **every `.css` file in every loaded bundle** into the document `<head>` on every page.
Because each sheet is served scoped to its plugin's own DOM (the `@scope` rewrite above), it is inert
everywhere else — so global loading is safe, and your styles work wherever the host renders your content:
your routed pages, render-slot widgets embedded in *host* pages, and modals you open via `IModalService`.

Do **not** inject `<link>` tags via `<HeadContent>`:
- it's redundant (the host already loaded the sheet), and
- `HeadContent` is **last-render-wins, not additive** — a head-injecting component on a page that has its
  own `<HeadContent>` (yours or the host's) silently replaces it, dropping OG meta tags or the other
  component's links. This bites *across* components, so a "shared head component" pattern is a trap.

`/_content/<id>/...` is where the host serves your `wwwroot/` (for your own JS imports, images, etc.).
`<id>` is your bundle id (default: the assembly name; matching is case-insensitive). Use plain URLs — not
the fingerprinted `Assets[...]` helper.

### JavaScript — import on demand, never via `<HeadContent>`

Ship `.js` in `wwwroot/` and load it as an ES module from the component that uses it, before the first
interop call:

```csharp
// e.g. in OnAfterRenderAsync(firstRender: true)
await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/blackjack.js");
```

The browser caches the module by URL — repeated imports execute the script once per page load. This loads
the script only where it's used, works when your component renders inside a *host* page (render slots,
modals), and has none of `HeadContent`'s last-render-wins hazards. A classic script that publishes
`window.*` globals also works when imported this way (modules are strict mode — declare your variables).

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
A: Your CSS is escaping its scope and clobbering the host chrome. The host scopes plugin CSS to a
`data-iw4m-plugin="<id>"` marker it stamps around your content — but content the host renders *imperatively*
(a modal opened via `IModalService`, or anything you build with a `RenderFragment`/`RenderTreeBuilder`) is
opaque to the host, so it can't mark it for you. **Fix:** wrap that content in the marker yourself:
`<div data-iw4m-plugin="<your-bundle-id>" style="display:contents">…</div>`. Routed pages and render-slot
widgets are marked automatically — you only do this for imperatively-rendered content.

**Q: My responsive/state classes don't work — cards stay single-column, `md:`-only content stays hidden,
`hover:` is dead.**
A: You wrapped your Tailwind in `layer(plugins)`. Under the host's CSS scoping that lower layer stops your
utilities overriding the host's base utilities on your *own* pages (e.g. your `sm:flex-row` can't beat the
host's `flex-col`). **Fix:** emit unlayered — `@import "tailwindcss/utilities.css";` (no `layer(...)`). The
helper scaffolds this and warns (`IW4M1001`) if `layer(plugins)` is present.

**Q: My styles are missing when my component renders inside a host page (render slot, modal).**
A: They shouldn't be — the host links every bundle stylesheet globally, precisely so slot widgets and
`IModalService` modals on host pages are styled. If styles are missing, check (a) the `<link>` for
`/_content/<id>/…` is in the document head (if not, your bundle didn't load — check the boot log), and
(b) your content is inside a `data-iw4m-plugin="<id>"` marker (imperative content must stamp its own).
Do **not** "fix" it with a `<HeadContent>` link injector — `HeadContent` is last-render-wins and will
clobber the hosting page's own head content (OG meta, etc.).

**Q: I get build warning `IW4M1001`.**
A: Your `Styles/tailwind.css` wraps utilities in `layer(plugins)`. The host scopes your CSS, so the layer is
unnecessary and breaks your responsive layout — drop `layer(...)` from your `@import`s.

**Q: A Tailwind class I use isn't in the output.**
A: Tailwind purges anything not found *literally* in your `.razor`. Dynamic strings like `$"text-{c}-500"`
are dropped. Add them to a safelist / `@source inline(...)`.

**Q: Can I use plain CSS / Sass instead of Tailwind?**
A: Yes. The bundle is tool-agnostic — drop any `.css` into `wwwroot/` (served at `/_content/<id>/…`). The host
scopes every served `.css` to your plugin's DOM, so even bespoke class names can't bleed into the host (and
host styles can't reach into yours). Author it however you like.

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
Dispose the module reference in `DisposeAsync` (catch `JSDisconnectedException`). Never inject `<script>`
tags via `<HeadContent>` (last-render-wins — clobbers the hosting page's head, and head scripts don't load
when your component renders inside a host page it didn't author).

**Q: How do I size a modal I open via `IModalService.OpenCustom`?**
A: `modalClass`/`bodyClass` land on the **host** modal shell — *outside* your scoped CSS — so they must be
classes the host stylesheet contains. Use the host's safelisted modal sizing vocabulary
(`max-w-md|lg|2xl|5xl|7xl`, `max-h-[80vh]|[90vh]` — see `WebfrontCore/wwwroot/css/src/app.css`), and note
`modalClass` is the *complete* size set: include a `max-h-*` alongside your width.

### Host-maintainer notes

- **Two independent guarantees make this work: self-containment + isolation.**
- **Self-containment (build-side):** the host *publishes* its design tokens at
  `SharedLibraryCore/Packaging/iw4madmin-theme.css`, and the `IW4MAdminTailwind` helper injects them ahead of
  each plugin's input (combined entry in `obj/`, two absolute `@import`s; the author's `@source` still
  resolves relative to the author file). So a plugin generates *every* utility its components use — including
  host-token ones (`bg-surface`, `border-line`) — and never depends on the host's purged stylesheet. Keep the
  token file in sync with `WebfrontCore/wwwroot/css/src/theme.css` `@theme`.
- **Isolation (serve-side, scoping):** when a bundle's web assets are registered (`SharedLibraryCore` →
  `PluginBundleLoader.RegisterBundle` → `PluginCssScoper`), every `.css` is rewritten to
  `@scope ([data-iw4m-plugin="<id>"]) to ([data-iw4m-host]) { … }`. `@property`/`@keyframes`/`@font-face`/`@import` are hoisted out
  (illegal inside `@scope`; leaving `@property` in silently breaks gradients/transforms). `:root`/`:host`
  blocks are hoisted too, but **self-referential declarations (`--x: var(--x)`, emitted by the injected token
  theme) are stripped** — left in a global `:root` they'd override the host's real token values with a
  circular reference and break them everywhere. A plugin can thus ship any classes (even ones the host uses)
  with no cascade-layer cooperation — the scope confines them.
- **The host loads every bundle's stylesheets globally** (`App.razor` iterates
  `PluginBundleLoader.Shared.LoadedBundles` and links each `.css` web asset). Safe because the sheets are
  scoped; necessary because bundle content also renders on *host* pages (render slots, modals) where no
  plugin page could inject a link — and `HeadContent` injection is last-render-wins, so per-page injection
  was both fragile and clobber-prone.
- **The host stamps the `data-iw4m-plugin="<id>"` marker** around each place it renders bundle content:
  routed pages (`Routes.razor` cascades the page assembly's bundle id → `MainLayout` wraps `@Body`) and
  render-slot widgets (`PluginRenderSlot` wraps each `DynamicComponent`). Wrappers use `display:contents` so
  they're layout-transparent. The id↔assembly lookup is `PluginBundleLoader.PluginIdForAssembly`.
- **Imperatively-rendered plugin content** (a modal opened via `IModalService`, anything built from a
  `RenderFragment`/`RenderTreeBuilder`) is opaque to the host, so the *plugin* stamps the marker itself —
  `<div data-iw4m-plugin="<id>" style="display:contents">…</div>`. See `ZombieServerLiveWidget.LiveContent`.
- **Host chrome rendered *inside* plugin content** is the inverse problem: within the marker, a plugin's
  unlayered scoped utilities beat the host's layered ones, so shared-utility host components collapse (a
  plugin's `.hidden` kills `SideContextMenu`'s `hidden xl:block`; a plugin's `.flex-col` kills
  `PluginPageShell`'s `xl:flex-row`). Two mechanisms, by containment:
  - **Leaf host chrome** (contains no plugin markup — e.g. `SideContextMenu`) stamps `data-iw4m-host` on
    its roots. That attribute is the **scoping limit** of every plugin sheet, so plugin CSS can't reach
    inside (donut hole; limit elements and their descendants are out of scope).
  - **Container host chrome** (wraps plugin content — e.g. `PluginPageShell`) can't be a hole (it would
    orphan the content inside), so its structural classes use the host-reserved **`wc-*` namespace**
    (defined in `app.css`, never emitted by a plugin Tailwind build) instead of shared utilities.
- **Browser support:** scoping relies on the native CSS `@scope` at-rule (Chrome/Edge 118+, Safari 17.4+,
  Firefox 128+ — all 2023–2024). On a browser without it the `@scope` block is ignored, so a plugin's
  scoped rules simply don't apply (pages render unstyled but the host chrome stays intact); the hoisted
  globals still load. Fine for the admin webfront's modern-browser audience.
