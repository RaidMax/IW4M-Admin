# Webfront redesign guide

Visual language for the 2026 webfront redesign. The home page (`Components/Features/Home/Pages/Home.razor`),
server card (`Components/Features/Servers/Components/ServerCard.razor`) and layout
(`Components/UI/Layout/*.razor`) are the reference implementation. Every other page should read as the
same product.

## Ground rules

- Markup and Tailwind classes only. Do not change component logic, parameters, injected services,
  `@code` behaviour, event handlers, `@bind`s, `AuthorizeView` policies or the data being displayed.
- Keep every element `id` that JavaScript touches (`wwwroot/js/*.js`, plugin JS) and every `@ref`.
- Keep `AuthorizeView` `Context="..."` names where nested; add one when you nest a new `AuthorizeView`.
- Tailwind v4. Theme tokens are exposed as utilities: `bg-background`, `bg-surface`, `bg-surface-alt`,
  `bg-surface-hover`, `text-foreground`, `text-subtle`, `text-muted`, `text-accent`, `border-line`,
  `border-line-hover`, `border-divider`, `text-primary` / `bg-primary`, `text-secondary`, `text-success`,
  `text-warning`, `text-error`, `text-info`, `text-level-*` (permission colours), `bg-input-bg`,
  `border-input-border`, `bg-table-header`, `bg-table-row-alt`. Opacity modifiers work (`bg-primary/10`).
- Fonts: body is Manrope via `font-sans` (default). Use `font-mono tabular-nums` for numbers, ids, IPs,
  pings, timestamps and console output.
- Localisation: use existing `AppState.Loc("KEY")` calls as they are. For any new label use
  `AppState.LocOr("KEY", "English fallback")`. Never hard-code English without a `LocOr`.
- No new NuGet packages, no new JS files, no inline `<style>`.
- Never leave a page with an empty `<h1>`; `FocusOnNavigate` targets `h1`, so keep exactly one `h1`
  (or the page's existing heading element) per page.

## Page skeleton

```razor
<div class="p-4 md:p-6 xl:p-8 max-w-[1680px] mx-auto space-y-5">
    <!-- heading row -->
    <div class="flex flex-wrap items-end gap-x-6 gap-y-3">
        <div class="min-w-0">
            <h1 class="text-2xl font-extrabold tracking-tight text-gradient-title">@AppState.Loc("...")</h1>
            <p class="text-sm text-muted mt-0.5">subtitle / counts</p>
        </div>
        <!-- optional right-aligned tabs / actions -->
        <div class="ml-auto flex items-center gap-2">...</div>
    </div>

    ...sections...
</div>
```

Narrow content (settings, forms, about) can use `max-w-4xl` instead of `max-w-[1680px]`.

## Building blocks

**Tabs / segmented control** (filters, periods, servers):

```razor
<div class="inline-flex gap-1 p-1 rounded-lg bg-surface border border-line max-w-full overflow-x-auto">
    <a class="px-3 py-1.5 rounded-md text-sm font-medium whitespace-nowrap transition-colors bg-surface-alt text-foreground shadow-sm">Active</a>
    <a class="px-3 py-1.5 rounded-md text-sm font-medium whitespace-nowrap transition-colors text-subtle hover:text-foreground hover:bg-surface-hover">Other</a>
</div>
```

**Filter chips** (multi-choice filters, with optional count):

```razor
<button class="px-3 py-1.5 rounded-full border text-xs font-semibold transition-colors border-primary text-primary bg-primary/10">Bans <span class="font-mono text-muted">212</span></button>
<button class="px-3 py-1.5 rounded-full border text-xs font-semibold transition-colors border-line bg-surface text-subtle hover:text-foreground">Kicks</button>
```

**Stat tile**:

```razor
<div class="rounded-xl bg-surface border border-line px-4 py-3.5">
    <div class="text-[11px] font-semibold uppercase tracking-wider text-muted">Label</div>
    <div class="mt-1 text-2xl font-extrabold tracking-tight tabular-nums leading-none">1,234<span class="text-base font-semibold text-muted"> / 5,000</span></div>
    <div class="mt-1.5 text-xs text-muted">detail</div>
</div>
```

Tiles sit in `grid grid-cols-2 xl:grid-cols-4 gap-3`.

**Card**:

```razor
<section class="rounded-xl bg-surface border border-line overflow-hidden">
    <div class="flex items-center gap-3 px-4 py-2.5 border-b border-line">
        <h2 class="flex-1 text-[11px] font-semibold uppercase tracking-wider text-muted">Section</h2>
        <span class="text-xs text-muted">meta</span>
    </div>
    <div class="p-4">...</div>
</section>
```

Only one level of card. Do not nest cards inside cards; use dividers (`divide-y divide-line`) for lists inside a card.

**Table** (inside a card, `overflow-x-auto` wrapper):

```razor
<div class="overflow-x-auto">
<table class="w-full text-sm">
    <thead>
        <tr class="text-[10px] font-semibold uppercase tracking-wider text-muted">
            <th class="text-left px-4 py-2.5 border-b border-line">Column</th>
        </tr>
    </thead>
    <tbody class="divide-y divide-line">
        <tr class="hover:bg-surface-hover transition-colors">
            <td class="px-4 py-2.5 align-middle">value</td>
            <td class="px-4 py-2.5 align-middle font-mono tabular-nums text-muted whitespace-nowrap">12 Sep 21:02</td>
        </tr>
    </tbody>
</table>
</div>
```

**Pills** (status / penalty type):

```razor
<span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[11px] font-bold uppercase tracking-wide bg-error/15 text-error">Ban</span>
```

Colour by meaning: ban `bg-error/15 text-error`, temp ban `bg-warning/15 text-warning`, kick `bg-orange-500/15 text-orange-400`,
warning `bg-yellow-500/15 text-yellow-300`, unban `bg-success/15 text-success`, flagged `bg-level-flagged/15 text-level-flagged`,
neutral `bg-surface-alt text-subtle`. Permission level: `border border-current bg-transparent` plus `AppState.GetLevelColorClass(level)`.
`AppState.GetPenaltyBadgeClass(type)` already exists and may be used as-is.

**Buttons**:

```razor
<button class="inline-flex items-center gap-1.5 h-9 px-3 rounded-lg border border-line bg-surface-alt text-sm font-semibold text-foreground hover:bg-surface-hover transition-colors">Default</button>
<button class="inline-flex items-center gap-1.5 h-9 px-3 rounded-lg bg-primary text-sm font-semibold text-white hover:brightness-110 transition">Primary</button>
<button class="inline-flex items-center gap-1.5 h-9 px-3 rounded-lg border border-error/40 bg-surface-alt text-sm font-semibold text-error hover:bg-error/10 transition-colors">Destructive</button>
<button class="w-9 h-9 rounded-lg flex items-center justify-center text-muted hover:text-foreground hover:bg-surface-hover transition-colors" title="...">icon only</button>
```

Icons: Phosphor, `<i class="ph ph-name">`, `ph-bold` / `ph-fill` variants. Button icons `text-base`.

**Inputs**:

```razor
<input class="block w-full h-10 px-3 rounded-lg bg-background border border-line text-sm text-foreground placeholder-muted focus:outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 transition-all"/>
<select class="block w-full h-10 px-3 rounded-lg bg-background border border-line text-sm text-foreground focus:outline-none focus:border-primary"> ... </select>
<textarea class="block w-full min-h-[8rem] p-3 rounded-lg bg-background border border-line text-sm font-mono text-foreground focus:outline-none focus:border-primary"></textarea>
```

Labels: `text-[11px] font-semibold uppercase tracking-wider text-muted mb-1.5`.

**Toggle** (boolean filter): reuse `<ToggleSwitch>` from `Components/UI/Controls`.

**Empty state**:

```razor
<div class="flex flex-col items-center justify-center gap-2 py-10 text-muted text-sm">
    <i class="ph ph-tray text-3xl text-secondary"></i>
    <span>@AppState.Loc("...")</span>
</div>
```

**Loading**: `rounded-xl bg-surface border border-line h-24 animate-pulse`.

**Activity / event feed row**: `grid grid-cols-[18px_1fr_auto] gap-2 items-baseline px-4 py-1.5 text-sm`, icon first,
timestamp last in `font-mono text-[11px] text-muted`.

**Profile hero**: `relative overflow-hidden rounded-xl border border-line bg-surface-alt p-5 grid gap-4 md:grid-cols-[auto_1fr_auto] items-center`,
avatar initial `w-16 h-16 rounded-2xl bg-surface border-2 border-current flex items-center justify-center text-2xl font-extrabold`
coloured by level class, name `text-2xl font-extrabold tracking-tight`, meta row `flex flex-wrap gap-x-4 gap-y-1 text-sm text-subtle`,
actions in a `flex flex-wrap gap-1.5 justify-end` group of the buttons above.

**Console output**: `bg-black/40 font-mono text-[13px] leading-relaxed p-4 rounded-lg border border-line max-h-[60vh] overflow-y-auto`,
command lines `text-primary`, success `text-success`, errors `text-error`, timestamps `text-muted mr-2`.

## Things to avoid

- `shadow-lg` / `shadow-xl` on ordinary cards (reserve shadows for floating menus and modals).
- `rounded-lg` everywhere; page-level containers are `rounded-xl`, small controls `rounded-md` / `rounded-lg`.
- Coloured section headers or gradient backgrounds beyond `text-gradient-title` on the page heading and the profile hero.
- Bootstrap class names (`d-flex`, `btn`, `card`, `text-primary` in Bootstrap sense). The project has no Bootstrap.
- Hard-coded hex colours; use the tokens.
