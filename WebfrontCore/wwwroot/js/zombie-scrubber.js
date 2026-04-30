// zombie-scrubber.js
// Konva-based multi-lane match timeline scrubber. Owned by ZombieMatchScrubber.razor.
// State (filter, zoom, scrub time) lives in C# via JSInterop; this module handles
// rendering + interaction only.
//
// Public API (window.zombieScrubber):
//   init(elementId, payload, dotnetRef, focusClientId?)   — build stage, attach handlers
//   dispose(elementId)                                    — destroy stage, free refs
//   setFilter(elementId, filter)                          — toggle category visibility
//   setZoom(elementId, level)                             — scale time axis (1..20x)
//   setLaneMode(elementId, mode)                          — 'qualified' (default) | 'all'
//   setScrubTime(elementId, seconds)                      — move cursor (programmatic)
//   focusClient(elementId, clientId | null)               — dim other lanes; null = reset

(function () {
    'use strict';

    const stages = new Map(); // elementId -> ScrubberInstance

    // Visual config — mirrors old C# GetEventVisuals. Tailwind palette mapped to hex
    // so Konva can fill shapes directly without a DOM round-trip. `glyph` is the
    // Phosphor font codepoint (rendered via fontFamily: "Phosphor", which is loaded
    // via App.razor's stylesheet imports).
    const PH = (cp) => String.fromCodePoint(cp);
    const CATEGORY_VISUALS = {
        'powerup':       { fill: '#facc15', glyph: PH(0xe2de), z: 30, tick: false }, // ph-lightning
        'danger':        { fill: '#f97316', glyph: PH(0xe4e0), z: 40, tick: false }, // ph-warning
        'critical':      { fill: '#ef4444', glyph: PH(0xe916), z: 50, tick: false }, // ph-skull
        'success':       { fill: '#22c55e', glyph: PH(0xe2ac), z: 30, tick: false }, // ph-heartbeat
        'perk':          { fill: '#a855f7', glyph: PH(0xe700), z: 25, tick: false }, // ph-pill
        'weapon':        { fill: '#3b82f6', glyph: PH(0xe636), z: 20, tick: false }, // ph-knife
        'weapon-abandon':{ fill: '#fb923c', glyph: PH(0xe636), z: 20, tick: false }, // ph-knife (abandon variant)
        'box':           { fill: '#60a5fa', glyph: PH(0xe1da), z: 20, tick: false }, // ph-cube
        'box-pass':      { fill: '#fb923c', glyph: PH(0xe1da), z: 20, tick: false }, // ph-cube (passed/missed)
        'box-teddy':     { fill: '#f472b6', glyph: PH(0xe9fa), z: 25, tick: false }, // ph-spiral
        'door':          { fill: '#f59e0b', glyph: PH(0xe7e6), z: 15, tick: false }, // ph-door-open
        'trap':          { fill: '#f87171', glyph: PH(0xe2de), z: 25, tick: false }, // ph-lightning (trap)
        'build':         { fill: '#10b981', glyph: PH(0xe5d4), z: 20, tick: false }, // ph-wrench
        'session-join':  { fill: '#94a3b8', glyph: PH(0xe428), z: 15, tick: false }, // ph-sign-in
        'session-leave': { fill: '#64748b', glyph: PH(0xe42a), z: 15, tick: false }, // ph-sign-out
        'round':         { fill: '#94a3b8', glyph: '',        z: 60, tick: true  }, // round markers = tick lines
        // Match-level events live in their own top-axis tick band (drawn once, not
        // per-lane). Both the canonical EE-complete trophy and the per-step progress
        // markers render up there; the per-step variant uses a smaller dimmer disc
        // so it visually reads as "progress" not "achievement".
        'easter-egg':      { fill: '#fbbf24', glyph: PH(0xe67e), z: 55, tick: false }, // ph-trophy
        'easter-egg-step': { fill: '#f59e0b', glyph: PH(0xe67e), z: 50, tick: false }, // ph-trophy (step)
        'default':         { fill: '#94a3b8', glyph: '',        z: 10, tick: true  }
    };

    const FILTER_RULES = {
        'all':      () => true,
        'critical': c => c === 'danger' || c === 'critical' || c === 'round' || c === 'easter-egg' || c === 'easter-egg-step',
        'powerups': c => c === 'powerup' || c === 'round' || c === 'easter-egg' || c === 'easter-egg-step',
        'economy':  c => ['weapon','weapon-abandon','box','box-pass','door','trap','build','perk','round','easter-egg','easter-egg-step'].includes(c)
    };

    const LANE_HEIGHT = 36;
    const LANE_GAP = 8;
    const TOP_PAD = 24;            // axis labels (round-number text)
    const TICKBAND_HEIGHT = 28;    // match-level event row above lanes
    const TICKBAND_GAP = 4;        // separator between tickband and first lane
    const SIDE_PAD_MULTI = 96;     // lane name labels (left) when multi-lane
    const SIDE_PAD_SINGLE = 16;    // single-lane: no labels, narrow margin
    const BOTTOM_PAD = 12;
    const DOT_RADIUS = 8;
    const TICKBAND_DOT_RADIUS = 6; // smaller than lane dots so band reads "secondary"
    const TICK_HEIGHT = 18;
    const SCRUB_DEBOUNCE_MS = 50;

    class ScrubberInstance {
        constructor(elementId, payload, dotnetRef, focusClientId) {
            this.elementId = elementId;
            this.payload = payload;
            this.dotnetRef = dotnetRef;
            this.focusClientId = focusClientId ?? null;
            this.filter = 'all';
            // Default lane mode: hide drop-ins so the timeline matches the leaderboard's
            // qualified roster. If the payload has zero qualified lanes (legacy match where
            // the qualifier wasn't computed), fall back to 'all' to avoid an empty stage.
            const anyQualified = (payload.lanes || []).some(l => l.isQualified);
            this.laneMode = anyQualified ? 'qualified' : 'all';
            this.zoom = 1;
            this.scrubSeconds = payload.minSeconds;
            this._scrubDebounce = null;
            this._tooltipEl = null;

            this._build();
            this._wireResize();
            // Initial scrub time → first event if available
            const firstSec = this._firstEventSeconds();
            if (firstSec != null) this.setScrubTime(firstSec);
        }

        _firstEventSeconds() {
            for (const lane of this._visibleLanes()) {
                if (lane.events.length > 0) return lane.events[0].seconds;
            }
            return null;
        }

        // Lanes drawn at the current laneMode. 'qualified' hides drop-ins; 'all' shows
        // every lane. Drives stage height, lane Y positions, and event placement.
        _visibleLanes() {
            if (this.laneMode === 'all') return this.payload.lanes;
            return this.payload.lanes.filter(l => l.isQualified);
        }

        _container() {
            return document.getElementById(this.elementId);
        }

        _innerWidth() {
            const c = this._container();
            if (!c) return 800;
            // Account for padding (p-2 = 8px each side). Base width = container at 1x.
            return Math.max(c.clientWidth - 16, 400);
        }

        // Actual stage width — grows with zoom so the container scrolls.
        _stageWidth() {
            return this._innerWidth() * this.zoom;
        }

        _sidePad() {
            return this._visibleLanes().length > 1 ? SIDE_PAD_MULTI : SIDE_PAD_SINGLE;
        }

        // Y-coord at which lane content begins. When match-level events exist, lanes
        // shift down to make room for the top-axis tick band; otherwise lanes sit
        // immediately under the round-label TOP_PAD.
        _laneAreaTop() {
            return TOP_PAD + (this._hasMatchLevelEvents() ? TICKBAND_HEIGHT + TICKBAND_GAP : 0);
        }

        _hasMatchLevelEvents() {
            return (this.payload.matchLevelEvents || []).length > 0;
        }

        _stageHeight() {
            return this._laneAreaTop() + this._visibleLanes().length * (LANE_HEIGHT + LANE_GAP) + BOTTOM_PAD;
        }

        _build() {
            const container = this._container();
            if (!container) return;
            container.innerHTML = '';

            this.stage = new Konva.Stage({
                container: container,
                width: this._stageWidth(),
                height: this._stageHeight()
            });

            this.bgLayer = new Konva.Layer({ listening: false });
            this.laneLayer = new Konva.Layer({ listening: true });
            this.scrubLayer = new Konva.Layer({ listening: true });

            this.stage.add(this.bgLayer);
            this.stage.add(this.laneLayer);
            this.stage.add(this.scrubLayer);

            this._drawBackground();
            this._drawMatchLevelBand();
            this._drawLanes();
            this._drawScrubber();

            // Mirror _redrawAll: skip cache at large widths (see comment there).
            if (this._stageWidth() <= 4000) this.bgLayer.cache();

            this._wireMouseHandlers();
            this._wireScrollCull();
            this._applyFocus();

            // Konva paints to canvas, so glyph text on the tickband ticks (Phosphor
            // font) only renders if the font is loaded at draw time. On a cold page
            // load Phosphor often hasn't finished loading by first paint — the dot
            // backs render but the glyph is missing. document.fonts.ready resolves
            // once all CSS-declared fonts finish; redraw then so the glyphs land.
            // No-op if the document is already font-stable.
            if (document?.fonts?.ready) {
                document.fonts.ready.then(() => {
                    if (this.stage) this._redrawAll();
                });
            }
        }

        // Repopulate viewport-windowed event nodes when the user scrolls past the
        // 400px padding band. We don't re-cull on every scroll tick — that'd defeat
        // the perf gain. The padding gives ~half-a-viewport of slack before the
        // next repopulate fires, and we rAF-coalesce.
        _wireScrollCull() {
            const c = this._container();
            if (!c) return;
            this._lastCullScrollLeft = c.scrollLeft;
            this._scrollHandler = () => {
                // Pin lane names + watermark every scroll tick — cheap (transforms
                // only), no gating needed.
                this._syncNamesOverlayScroll();
                this._syncBandWatermarkScroll();
                if (this._scrollRaf != null) return;
                this._scrollRaf = requestAnimationFrame(() => {
                    this._scrollRaf = null;
                    if (!this._container()) return;
                    const sl = this._container().scrollLeft;
                    // Only repopulate when we've drifted close to the padding edge
                    // (200px = half the 400px slack). Avoids redrawing on tiny
                    // mouse-wheel scrolls.
                    if (Math.abs(sl - (this._lastCullScrollLeft ?? 0)) < 200) return;
                    this._lastCullScrollLeft = sl;
                    this._redrawLaneAndBandLayers();
                });
            };
            c.addEventListener('scroll', this._scrollHandler, { passive: true });
        }

        // Re-render the cull-sensitive layers without touching the (heavy) bgLayer.
        _redrawLaneAndBandLayers() {
            this.laneLayer.destroyChildren();
            this._drawMatchLevelBandTicks();
            this._drawLanes();
            this._applyFilter();
            this._applyFocus();
            this.laneLayer.batchDraw();
        }

        _timeToX(seconds) {
            const span = Math.max(this.payload.maxSeconds - this.payload.minSeconds, 1);
            const usable = this._stageWidth() - this._sidePad() - 16;
            return this._sidePad() + ((seconds - this.payload.minSeconds) / span) * usable;
        }

        _xToTime(x) {
            const span = Math.max(this.payload.maxSeconds - this.payload.minSeconds, 1);
            const usable = this._stageWidth() - this._sidePad() - 16;
            const clampedX = Math.min(Math.max(x - this._sidePad(), 0), usable);
            return this.payload.minSeconds + (clampedX / usable) * span;
        }

        _laneY(idx) {
            return this._laneAreaTop() + idx * (LANE_HEIGHT + LANE_GAP) + LANE_HEIGHT / 2;
        }

        _drawBackground() {
            const lanes = this._visibleLanes();
            const bandTop = TOP_PAD - 6;
            // Round bands span the full vertical area (tickband + lanes) so columns
            // visually read as "this round's slice" across both regions.
            const bandHeight = (this._laneAreaTop() - TOP_PAD) + lanes.length * (LANE_HEIGHT + LANE_GAP) + 6;
            this.payload.roundBands.forEach((band, i) => {
                const x1 = this._timeToX(band.startSeconds);
                const x2 = this._timeToX(band.endSeconds);
                const w = Math.max(x2 - x1, 0.5);
                this.bgLayer.add(new Konva.Rect({
                    x: x1, y: bandTop, width: w,
                    height: bandHeight,
                    fill: i % 2 === 0 ? 'rgba(255,255,255,0.02)' : 'rgba(255,255,255,0.05)'
                }));
                // Round label
                if (w > 22) {
                    this.bgLayer.add(new Konva.Text({
                        x: x1 + 4, y: 4,
                        text: 'R' + band.roundNumber,
                        fontSize: 10,
                        fontFamily: 'monospace',
                        fill: '#64748b'
                    }));
                }
            });

            // Lane separator lines (canvas). Lane NAMES are drawn via a sticky-style
            // DOM overlay (see _renderLaneNamesOverlay) so they stay pinned to the
            // left edge as the user scrolls a zoomed timeline.
            lanes.forEach((lane, idx) => {
                const y = this._laneY(idx);
                this.bgLayer.add(new Konva.Line({
                    points: [this._sidePad(), y, this._stageWidth() - 8, y],
                    stroke: 'rgba(148,163,184,0.15)',
                    strokeWidth: 1
                }));
            });
            this._renderLaneNamesOverlay();
        }

        // DOM overlay for lane names — Konva text in bgLayer would scroll off-screen
        // at high zoom (canvas is wider than the scroll viewport). The overlay sits
        // inside the scroll container and gets translated by scrollLeft on each
        // scroll tick, giving a pinned-to-the-left effect without sticky CSS quirks.
        _renderLaneNamesOverlay() {
            const container = this._container();
            if (!container) return;
            const lanes = this._visibleLanes();
            const showLaneLabels = lanes.length > 1;

            if (!this._namesOverlay) {
                this._namesOverlay = document.createElement('div');
                this._namesOverlay.style.cssText =
                    'position:absolute;top:0;left:0;pointer-events:none;z-index:5;' +
                    'will-change:transform;';
                container.style.position = container.style.position || 'relative';
                container.appendChild(this._namesOverlay);
            }
            this._namesOverlay.innerHTML = '';
            this._namesOverlay.style.display = showLaneLabels ? 'block' : 'none';
            if (!showLaneLabels) return;

            const sidePad = this._sidePad();
            // Same offset rationale as _renderBandWatermarkOverlay — overlay top
            // is in container coords (padded), canvas y is in stage-content coords.
            const stageOffset = this.stage?.content?.offsetTop ?? 0;
            lanes.forEach((lane, idx) => {
                const div = document.createElement('div');
                div.textContent = this._stripColors(lane.name);
                div.title = this._stripColors(lane.name);
                div.style.cssText =
                    'position:absolute;left:8px;' +
                    'top:' + (stageOffset + this._laneY(idx) - 8) + 'px;' +
                    'width:' + (sidePad - 12) + 'px;' +
                    'font:bold 11px sans-serif;color:#cbd5e1;' +
                    'white-space:nowrap;overflow:hidden;text-overflow:ellipsis;' +
                    // Subtle background fade so dots/lines passing under the names
                    // don't visually intrude. Match the scroll viewport bg.
                    'background:linear-gradient(to right, rgba(15,23,42,0.85) 80%, transparent);' +
                    'padding:2px 4px;';
                this._namesOverlay.appendChild(div);
            });
            // Pin to current scrollLeft so the names stay at the visible left edge.
            this._syncNamesOverlayScroll();
        }

        _syncNamesOverlayScroll() {
            if (!this._namesOverlay) return;
            const c = this._container();
            if (!c) return;
            this._namesOverlay.style.transform = 'translateX(' + c.scrollLeft + 'px)';
        }

        // Top-axis tick band — match-level events (canonical EE marker, per-step
        // progress dots). Renders once at TOP_PAD + TICKBAND_HEIGHT/2; events get the
        // same hover/click treatment as lane events but are not lane-bound.
        _drawMatchLevelBand() {
            this._drawMatchLevelBandBackground();
            this._drawMatchLevelBandTicks();
        }

        // Static band components (just the tint) — live in bgLayer. The watermark
        // caption is a DOM overlay (see _renderBandWatermarkOverlay) so it stays
        // viewport-centered regardless of scroll/zoom; a stage-wide canvas text
        // would be off-screen at high zoom + scroll.
        _drawMatchLevelBandBackground() {
            if (!this._hasMatchLevelEvents()) {
                this._renderBandWatermarkOverlay();
                return;
            }
            const bandX = this._sidePad();
            const bandW = Math.max(this._stageWidth() - this._sidePad() - 8, 1);

            // Subtle row tint so the band is distinguishable from the round-label area
            this.bgLayer.add(new Konva.Rect({
                x: bandX, y: TOP_PAD,
                width: bandW,
                height: TICKBAND_HEIGHT,
                fill: 'rgba(251,191,36,0.04)',
                cornerRadius: 4
            }));

            this._renderBandWatermarkOverlay();
        }

        // DOM overlay for the "EASTER EGGS" caption. Positioned at the band's Y
        // coords, viewport-width, flex-centered → always visible at the middle of
        // the scroll viewport. Translated by scrollLeft on each scroll tick so it
        // stays pinned visually as the user pans a zoomed timeline.
        _renderBandWatermarkOverlay() {
            const container = this._container();
            if (!container) return;
            const visible = this._hasMatchLevelEvents();
            if (!this._bandWatermark) {
                this._bandWatermark = document.createElement('div');
                this._bandWatermark.style.cssText =
                    'position:absolute;left:0;pointer-events:none;z-index:4;' +
                    'display:flex;align-items:center;justify-content:center;' +
                    // line-height:1 so the glyph box matches the flex centering;
                    // default 1.2 leaves the text visually high in its box.
                    'font:900 18px sans-serif;line-height:1;' +
                    'color:rgba(251,191,36,0.35);' +
                    'letter-spacing:0.25em;text-transform:uppercase;' +
                    'will-change:transform;';
                container.style.position = container.style.position || 'relative';
                container.appendChild(this._bandWatermark);
            }
            this._bandWatermark.style.display = visible ? 'flex' : 'none';
            if (!visible) return;
            this._bandWatermark.textContent = 'Easter Eggs';
            // The overlay's top:0 is relative to the (padded) scroll container,
            // but Konva coords (TOP_PAD) start at the konvajs-content div which
            // sits below the container's padding. Add stage-content offsetTop so
            // the band y maps 1:1 between canvas and DOM.
            const stageOffset = this.stage?.content?.offsetTop ?? 0;
            this._bandWatermark.style.top = (stageOffset + TOP_PAD) + 'px';
            this._bandWatermark.style.height = TICKBAND_HEIGHT + 'px';
            this._bandWatermark.style.width = (container.clientWidth - 16) + 'px';
            this._syncBandWatermarkScroll();
        }

        _syncBandWatermarkScroll() {
            if (!this._bandWatermark) return;
            const c = this._container();
            if (!c) return;
            this._bandWatermark.style.transform = 'translateX(' + c.scrollLeft + 'px)';
        }

        // Cull-sensitive band components (tick dots + glyphs) — live in laneLayer,
        // re-rendered on scroll so off-screen ticks don't stay in memory.
        _drawMatchLevelBandTicks() {
            if (!this._hasMatchLevelEvents()) return;
            const y = TOP_PAD + TICKBAND_HEIGHT / 2;
            const win = this._visibleXWindow();
            this.payload.matchLevelEvents.forEach(evt => {
                const cat = evt.category || 'default';
                const v = CATEGORY_VISUALS[cat] || CATEGORY_VISUALS.default;
                const x = this._timeToX(evt.seconds);
                if (x < win.min || x > win.max) return;

                const group = new Konva.Group({ x: x, y: y });
                group.add(new Konva.Circle({
                    x: 0, y: 0, radius: TICKBAND_DOT_RADIUS,
                    fill: v.fill,
                    stroke: '#0f172a',
                    strokeWidth: 1.5,
                    shadowColor: v.fill,
                    shadowBlur: 3,
                    shadowOpacity: 0.4
                }));
                if (v.glyph) {
                    group.add(new Konva.Text({
                        x: -TICKBAND_DOT_RADIUS, y: -TICKBAND_DOT_RADIUS,
                        width: TICKBAND_DOT_RADIUS * 2, height: TICKBAND_DOT_RADIUS * 2,
                        text: v.glyph,
                        fontFamily: 'Phosphor',
                        fontSize: 10,
                        fill: '#0f172a',
                        align: 'center',
                        verticalAlign: 'middle',
                        listening: false
                    }));
                }
                group._evt = evt;
                group._isMatchLevel = true;
                group._tooltip = evt.time + ' • ' + evt.label;
                this.laneLayer.add(group);
            });
        }

        // Visible-x window in stage coords: events outside this band get culled
        // from the laneLayer at draw time. At zoom 15× a 12000px stage is ~99% off-
        // screen at any moment; drawing all events anyway means thousands of nodes
        // for ~10 visible. ±400px padding so events near the viewport edge stay
        // populated when the user nudges scroll without forcing a redraw.
        _visibleXWindow() {
            const c = this._container();
            if (!c) return { min: -Infinity, max: Infinity };
            const pad = 400;
            return { min: c.scrollLeft - pad, max: c.scrollLeft + c.clientWidth + pad };
        }

        _drawLanes() {
            const win = this._visibleXWindow();
            this._visibleLanes().forEach((lane, idx) => {
                const y = this._laneY(idx);

                // Gaps (behind events)
                lane.gaps.forEach(gap => {
                    const x1 = this._timeToX(gap.start);
                    const x2 = this._timeToX(gap.end);
                    if (x2 <= x1) return;
                    // Cull gaps fully outside the viewport (rare — match-spanning
                    // gaps stay visible since x2 - x1 covers the window).
                    if (x2 < win.min || x1 > win.max) return;
                    const isCompact = gap.compact;
                    let gx, gw;
                    if (isCompact) {
                        const mid = (x1 + x2) / 2;
                        gw = Math.max(Math.min((x2 - x1) * 0.15, 6), 2);
                        gx = mid - gw / 2;
                    } else {
                        gx = x1;
                        gw = Math.max(x2 - x1, 1);
                    }
                    const rect = new Konva.Rect({
                        x: gx, y: y - LANE_HEIGHT / 2 + 4, width: gw, height: LANE_HEIGHT - 8,
                        fill: 'rgba(248,113,113,0.18)',
                        stroke: 'rgba(248,113,113,0.7)',
                        strokeWidth: 1,
                        dash: [4, 3]
                    });
                    rect._tooltip = gap.tooltip;
                    rect._isGap = true;
                    this.laneLayer.add(rect);
                });

                // Events
                lane.events.forEach(evt => {
                    const cat = evt.category || 'default';
                    const v = CATEGORY_VISUALS[cat] || CATEGORY_VISUALS.default;
                    const x = this._timeToX(evt.seconds);
                    if (x < win.min || x > win.max) return;

                    let shape;
                    if (v.tick) {
                        // Round markers + unknown categories render as vertical ticks
                        shape = new Konva.Rect({
                            x: x - 1, y: y - TICK_HEIGHT / 2,
                            width: 2, height: TICK_HEIGHT,
                            fill: v.fill, opacity: 0.6,
                            cornerRadius: 1
                        });
                        shape._evt = evt;
                        shape._lane = lane;
                        shape._tooltip = evt.time + ' • ' + evt.label;
                        this.laneLayer.add(shape);
                    } else {
                        // Group: filled disc + Phosphor glyph centered.
                        const group = new Konva.Group({ x: x, y: y });
                        group.add(new Konva.Circle({
                            x: 0, y: 0, radius: DOT_RADIUS,
                            fill: v.fill,
                            stroke: '#0f172a',
                            strokeWidth: 2,
                            shadowColor: v.fill,
                            shadowBlur: 4,
                            shadowOpacity: 0.4
                        }));
                        if (v.glyph) {
                            group.add(new Konva.Text({
                                x: -DOT_RADIUS, y: -DOT_RADIUS,
                                width: DOT_RADIUS * 2, height: DOT_RADIUS * 2,
                                text: v.glyph,
                                fontFamily: 'Phosphor',
                                fontSize: 12,
                                fill: '#0f172a',
                                align: 'center',
                                verticalAlign: 'middle',
                                listening: false
                            }));
                        }
                        // Lift the hover/click target to the group itself so the icon
                        // doesn't intercept events with its own bounding box.
                        group._evt = evt;
                        group._lane = lane;
                        group._tooltip = evt.time + ' • ' + evt.label;
                        this.laneLayer.add(group);
                    }
                });
            });
        }

        _drawScrubber() {
            const x = this._timeToX(this.scrubSeconds);
            const top = TOP_PAD - 8;
            const bottom = this._laneAreaTop() + this._visibleLanes().length * (LANE_HEIGHT + LANE_GAP);

            this.scrubLine = new Konva.Line({
                points: [x, top, x, bottom],
                stroke: '#22d3ee', strokeWidth: 2, dash: [4, 3]
            });
            this.scrubHandle = new Konva.Rect({
                x: x - 6, y: top - 6, width: 12, height: 12,
                fill: '#22d3ee', cornerRadius: 2,
                draggable: true,
                dragBoundFunc: (pos) => ({
                    x: Math.min(Math.max(pos.x, this._sidePad() - 6), this._stageWidth() - 16),
                    y: top - 6
                })
            });

            this.scrubHandle.on('dragmove', () => {
                const handleX = this.scrubHandle.x() + 6;
                this.scrubSeconds = this._xToTime(handleX);
                this.scrubLine.points([handleX, top, handleX, bottom]);
                this.scrubLayer.batchDraw();
                this._notifyScrub();
            });

            this.scrubLayer.add(this.scrubLine);
            this.scrubLayer.add(this.scrubHandle);
        }

        _notifyScrub() {
            if (this._scrubDebounce) clearTimeout(this._scrubDebounce);
            this._scrubDebounce = setTimeout(() => {
                if (this.dotnetRef) {
                    this.dotnetRef.invokeMethodAsync('OnScrubChanged', this.scrubSeconds);
                }
            }, SCRUB_DEBOUNCE_MS);
        }

        _wireMouseHandlers() {
            // Walk parent chain — events fire on inner shapes (Circle/Text inside
            // Group); the metadata (_evt/_tooltip) lives on the Group or Rect.
            const findTarget = (node) => {
                while (node && node !== this.laneLayer) {
                    if (node._tooltip || node._evt) return node;
                    node = node.getParent && node.getParent();
                }
                return null;
            };

            this.laneLayer.on('mouseover', (e) => {
                const t = findTarget(e.target);
                if (!t || !t._tooltip) return;
                this._showTooltip(t._tooltip);
                document.body.style.cursor = t._evt ? 'pointer' : 'help';
            });
            this.laneLayer.on('mouseout', () => {
                this._hideTooltip();
                document.body.style.cursor = '';
            });
            this.laneLayer.on('click tap', (e) => {
                const t = findTarget(e.target);
                if (!t || !t._evt) return;
                if (this.dotnetRef) {
                    this.dotnetRef.invokeMethodAsync('OnEventClicked', t._lane.clientId, t._evt.seconds);
                }
                this.setScrubTime(t._evt.seconds);
            });

            // Scroll wheel → cursor-anchored zoom. Anchor = pointer position in
            // stage coords; we shift container.scrollLeft post-zoom so the time
            // under the cursor stays under the cursor.
            this.stage.on('wheel', (e) => {
                e.evt.preventDefault();
                // Multiplicative step keeps wheel-feel consistent across the 1..20 range
                // (linear step at 0.25 took ~80 ticks to hit 20x; 1.15x scale = ~22 ticks).
                const factor = e.evt.deltaY < 0 ? 1.15 : 1 / 1.15;
                const newZoom = Math.min(Math.max(this.zoom * factor, 1), 20);
                if (Math.abs(newZoom - this.zoom) < 0.01) return;

                const pointer = this.stage.getPointerPosition();
                const anchorStageX = pointer ? pointer.x : this._stageWidth() / 2;

                // Coalesce multiple wheel events within a frame: at high zoom each
                // _redrawAll re-caches a stage-width-wide bitmap (linear in canvas
                // area), so a 60-event-per-second trackpad would fire 60 redraws and
                // turn the wheel into a slideshow. rAF collapses bursts to one
                // redraw per frame; the latest target zoom always wins.
                this._pendingZoom = { zoom: newZoom, anchor: anchorStageX };
                if (this._zoomRaf == null) {
                    this._zoomRaf = requestAnimationFrame(() => {
                        this._zoomRaf = null;
                        const p = this._pendingZoom;
                        this._pendingZoom = null;
                        if (p) this._setZoomAnchored(p.zoom, p.anchor);
                    });
                }
            });
        }

        // Zoom while keeping the time at `anchorStageX` (stage coords) under the
        // same on-screen point. Shifts container.scrollLeft to compensate.
        _setZoomAnchored(newZoom, anchorStageX) {
            const container = this._container();
            if (!container) { this.setZoom(newZoom); return; }

            const oldZoom = this.zoom;
            const oldScrollLeft = container.scrollLeft;
            const viewportX = anchorStageX - oldScrollLeft;
            const ratio = newZoom / oldZoom;

            this.zoom = newZoom;
            this._redrawAll();

            // After redraw, anchor's new stage X = oldStageX * ratio. Set scroll
            // so viewportX stays the same.
            const newStageX = anchorStageX * ratio;
            container.scrollLeft = Math.max(0, newStageX - viewportX);

            // _redrawAll synced overlays at the OLD scrollLeft; we just changed
            // it, so re-sync now (same JS task) — otherwise the overlays paint
            // for one frame at the old position, then the native scroll event
            // fires next frame and snaps them back. That's the "flicker".
            this._syncBandWatermarkScroll();
            this._syncNamesOverlayScroll();
            this._lastCullScrollLeft = container.scrollLeft;

            // Notify Razor so the toolbar zoom display stays in sync (wheel path
            // doesn't go through Razor; button path is idempotent).
            if (this.dotnetRef) {
                this.dotnetRef.invokeMethodAsync('OnZoomChanged', newZoom);
            }
        }

        _wireResize() {
            this._resizeHandler = () => {
                if (!this.stage) return;
                this.stage.width(this._stageWidth());
                this._redrawAll();
                // _redrawAll re-runs _drawMatchLevelBandBackground which resizes
                // the watermark overlay; lane names overlay is repopulated by
                // _drawBackground. Both pick up the new container.clientWidth.
            };
            window.addEventListener('resize', this._resizeHandler);
        }

        _redrawAll() {
            this.bgLayer.destroyChildren();
            // Without this, an existing cached bitmap from a prior zoom level keeps
            // rendering at its old width even though we destroyed + redrew children
            // — Konva treats the layer as cached and skips child draw. Visually,
            // round bands "lag behind" icons (which live in the un-cached laneLayer).
            this.bgLayer.clearCache();
            this.laneLayer.destroyChildren();
            this.scrubLayer.destroyChildren();
            // Stage dimensions track current zoom — must update both before redraw.
            const sw = this._stageWidth();
            this.stage.width(sw);
            this.stage.height(this._stageHeight());
            this._drawBackground();
            this._drawMatchLevelBand();
            this._drawLanes();
            this._drawScrubber();
            // bgLayer.cache() snapshots a stage-wide bitmap (~sw × stageHeight px).
            // At low/medium zoom this speeds subsequent batchDraws (filter/focus
            // toggles). At high zoom it allocates ~16 MB per re-cache (zoom 20x at
            // 800px base = 16000px wide), and we re-cache on every wheel tick — so
            // skip the cache once the bitmap would be expensive. Trade-off: filter
            // toggles redraw raw children at high zoom, but those are rare during
            // active zooming.
            if (sw <= 4000) this.bgLayer.cache();
            this._applyFilter();
            this._applyFocus();
            // Sync .draw() instead of batchDraw to eliminate the per-zoom-tick
            // flash: stage.width() resizes (and clears) the canvas synchronously,
            // and batchDraw defers the repaint to next rAF — so the browser
            // composites a frame with the cleared canvas in between. Sync draw
            // repopulates the pixels before the JS task ends, so no blank frame
            // is ever composited. We're already inside our own rAF coalescer
            // (wheel handler), so the rAF coalescing batchDraw provides is moot.
            this.bgLayer.draw();
            this.laneLayer.draw();
            this.scrubLayer.draw();
        }

        _showTooltip(text) {
            if (!this._tooltipEl) {
                this._tooltipEl = document.createElement('div');
                this._tooltipEl.style.cssText =
                    'position:fixed;pointer-events:none;background:#0f172a;color:#e2e8f0;' +
                    'padding:6px 10px;border-radius:6px;font-size:11px;border:1px solid #334155;' +
                    'z-index:9999;white-space:nowrap;box-shadow:0 4px 12px rgba(0,0,0,0.4)';
                document.body.appendChild(this._tooltipEl);
            }
            this._tooltipEl.textContent = text;
            this._tooltipEl.style.display = 'block';
            const move = (ev) => {
                if (!this._tooltipEl) return;
                this._tooltipEl.style.left = (ev.clientX + 12) + 'px';
                this._tooltipEl.style.top = (ev.clientY + 12) + 'px';
            };
            this._mouseMove = move;
            window.addEventListener('mousemove', move);
        }

        _hideTooltip() {
            if (this._tooltipEl) this._tooltipEl.style.display = 'none';
            if (this._mouseMove) {
                window.removeEventListener('mousemove', this._mouseMove);
                this._mouseMove = null;
            }
        }

        _applyFilter() {
            const rule = FILTER_RULES[this.filter] || FILTER_RULES.all;
            // Iterate top-level lane children — events are on Groups (dots) or
            // Rects (gaps + ticks). Inner shapes inherit visibility from their parent.
            this.laneLayer.getChildren().forEach(node => {
                if (node._isGap) return; // gaps always visible
                if (!node._evt) return;
                node.visible(rule(node._evt.category || 'default'));
            });
        }

        _applyFocus() {
            this.laneLayer.getChildren().forEach(node => {
                // Match-level events live above the lanes and aren't bound to a player —
                // they stay full opacity regardless of focus selection.
                if (node._isMatchLevel) return;
                if (!node._lane) return;
                if (this.focusClientId == null) {
                    node.opacity(1);
                } else {
                    node.opacity(node._lane.clientId === this.focusClientId ? 1 : 0.25);
                }
            });
        }

        _stripColors(s) {
            return (s || '').replace(/\^[0-9]/g, '');
        }

        // ── public methods ──

        setFilter(filter) {
            this.filter = filter;
            this._applyFilter();
            this.laneLayer.batchDraw();
        }

        setZoom(level) {
            // Button-press path: anchor at the visible viewport center so the
            // user doesn't lose their place when clicking +/-.
            const container = this._container();
            const anchor = container
                ? container.scrollLeft + container.clientWidth / 2
                : this._stageWidth() / 2;
            this._setZoomAnchored(level, anchor);
        }

        setScrubTime(seconds) {
            this.scrubSeconds = seconds;
            const x = this._timeToX(seconds);
            const top = TOP_PAD - 8;
            const bottom = this._laneAreaTop() + this._visibleLanes().length * (LANE_HEIGHT + LANE_GAP);
            if (this.scrubHandle) this.scrubHandle.x(x - 6);
            if (this.scrubLine) this.scrubLine.points([x, top, x, bottom]);
            this.scrubLayer.batchDraw();
            this._notifyScrub();
        }

        focusClient(clientId) {
            this.focusClientId = clientId;
            this._applyFocus();
            this.laneLayer.batchDraw();
        }

        setLaneMode(mode) {
            if (mode !== 'qualified' && mode !== 'all') return;
            if (this.laneMode === mode) return;
            this.laneMode = mode;
            this._redrawAll();
        }

        dispose() {
            window.removeEventListener('resize', this._resizeHandler);
            if (this._zoomRaf != null) {
                cancelAnimationFrame(this._zoomRaf);
                this._zoomRaf = null;
            }
            if (this._scrollRaf != null) {
                cancelAnimationFrame(this._scrollRaf);
                this._scrollRaf = null;
            }
            const c = this._container();
            if (c && this._scrollHandler) {
                c.removeEventListener('scroll', this._scrollHandler);
                this._scrollHandler = null;
            }
            if (this._namesOverlay) {
                this._namesOverlay.remove();
                this._namesOverlay = null;
            }
            if (this._bandWatermark) {
                this._bandWatermark.remove();
                this._bandWatermark = null;
            }
            this._hideTooltip();
            if (this._tooltipEl) {
                this._tooltipEl.remove();
                this._tooltipEl = null;
            }
            if (this.stage) {
                this.stage.destroy();
                this.stage = null;
            }
            if (this.dotnetRef) {
                // Caller-owned reference; do NOT dispose here. Razor disposes on its end.
                this.dotnetRef = null;
            }
        }
    }

    window.zombieScrubber = {
        init(elementId, payload, dotnetRef, focusClientId) {
            if (typeof Konva === 'undefined') {
                console.error('zombieScrubber: Konva not loaded');
                return;
            }
            // Defensive: dispose if already exists
            if (stages.has(elementId)) {
                stages.get(elementId).dispose();
                stages.delete(elementId);
            }
            // Camelcase normalization (System.Text.Json default is camelCase for output)
            stages.set(elementId, new ScrubberInstance(elementId, payload, dotnetRef, focusClientId));
        },
        dispose(elementId) {
            const inst = stages.get(elementId);
            if (inst) {
                inst.dispose();
                stages.delete(elementId);
            }
        },
        setFilter(elementId, filter) { const i = stages.get(elementId); if (i) i.setFilter(filter); },
        setZoom(elementId, level)    { const i = stages.get(elementId); if (i) i.setZoom(level); },
        setLaneMode(elementId, mode) { const i = stages.get(elementId); if (i) i.setLaneMode(mode); },
        setScrubTime(elementId, sec) { const i = stages.get(elementId); if (i) i.setScrubTime(sec); },
        focusClient(elementId, cid)  { const i = stages.get(elementId); if (i) i.focusClient(cid); }
    };
})();
