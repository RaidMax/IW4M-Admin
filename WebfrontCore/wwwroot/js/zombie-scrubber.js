// zombie-scrubber.js
// DOM-based multi-lane match timeline scrubber. Replaces the prior Konva canvas
// implementation — see git history for the canvas version. Owned by
// ZombieMatchScrubber.razor; state (filter, zoom, scrub time) lives in C# via
// JSInterop. This module renders + handles interaction.
//
// Why DOM over canvas: zoom in the canvas implementation triggered a full
// destroy+rebuild of ~6000 Konva nodes per rAF tick plus a stage-wide bitmap
// re-cache, producing 180-200ms frames (5 fps) on iGPU/Mac/VM hardware. DOM
// rendering offloads zoom to the browser compositor — `--zoom` CSS variable
// updates a parent width via calc(); GPU layer transforms reposition the
// absolutely-positioned children with zero JavaScript on the hot path.
//
// Public API (window.zombieScrubber) — preserved verbatim from the canvas
// implementation so the Razor caller is unchanged:
//   init(elementId, payload, dotnetRef, focusClientId?, initialLaneMode?)
//   dispose(elementId)
//   setFilter(elementId, filter)
//   setZoom(elementId, level)
//   setLaneMode(elementId, mode)              'qualified' | 'all'
//   setScrubTime(elementId, seconds)
//   focusClient(elementId, clientId | null)

(function () {
    'use strict';

    const instances = new Map();

    // Visual config — preserved verbatim from the prior CATEGORY_VISUALS table.
    // Maps an event category to a Phosphor glyph class + tailwind colour token.
    // The fill column drives both the dot background and the dot's drop-shadow
    // colour (CSS filter, GPU-composited — far cheaper than canvas shadowBlur).
    const CATEGORY_VISUALS = {
        'powerup':         { fill: '#facc15', icon: 'ph-lightning',          z: 30, tick: false },
        'danger':          { fill: '#f97316', icon: 'ph-warning',            z: 40, tick: false },
        'critical':        { fill: '#ef4444', icon: 'ph-skull',              z: 50, tick: false },
        'success':         { fill: '#22c55e', icon: 'ph-heartbeat',          z: 30, tick: false },
        'perk':            { fill: '#a855f7', icon: 'ph-pill',               z: 25, tick: false },
        'weapon':          { fill: '#3b82f6', icon: 'ph-knife',              z: 20, tick: false },
        'weapon-abandon':  { fill: '#fb923c', icon: 'ph-knife',              z: 20, tick: false },
        'box':             { fill: '#60a5fa', icon: 'ph-cube',               z: 20, tick: false },
        'box-pass':        { fill: '#fb923c', icon: 'ph-cube',               z: 20, tick: false },
        'box-teddy':       { fill: '#f472b6', icon: 'ph-spiral',             z: 25, tick: false },
        'door':            { fill: '#f59e0b', icon: 'ph-door-open',          z: 15, tick: false },
        'trap':            { fill: '#f87171', icon: 'ph-lightning',          z: 25, tick: false },
        'build':           { fill: '#10b981', icon: 'ph-wrench',             z: 20, tick: false },
        'session-join':    { fill: '#94a3b8', icon: 'ph-sign-in',            z: 15, tick: false },
        'session-leave':   { fill: '#64748b', icon: 'ph-sign-out',           z: 15, tick: false },
        'round':           { fill: '#94a3b8', icon: '',                       z: 60, tick: true  },
        'easter-egg':      { fill: '#fbbf24', icon: 'ph-trophy',             z: 55, tick: false },
        'easter-egg-step': { fill: '#f59e0b', icon: 'ph-trophy',             z: 50, tick: false },
        'power-on':        { fill: '#facc15', icon: 'ph-lightning',          z: 56, tick: false },
        'power-off':       { fill: '#94a3b8', icon: 'ph-lightning-slash',    z: 56, tick: false },
        'bank-deposit':    { fill: '#22c55e', icon: 'ph-piggy-bank',         z: 20, tick: false },
        'bank-withdraw':   { fill: '#fbbf24', icon: 'ph-hand-coins',         z: 20, tick: false },
        'locker-store':    { fill: '#60a5fa', icon: 'ph-lockers',            z: 20, tick: false },
        'locker-retrieve': { fill: '#34d399', icon: 'ph-lockers',            z: 20, tick: false },
        'gum-activate':    { fill: '#ec4899', icon: 'ph-sparkle',            z: 30, tick: false },
        'gum-take':        { fill: '#a855f7', icon: 'ph-gift',               z: 20, tick: false },
        'gum-leave':       { fill: '#94a3b8', icon: 'ph-heart-break',        z: 20, tick: false },
        'default':         { fill: '#94a3b8', icon: '',                       z: 10, tick: true  }
    };

    // Filter rules — same set + semantics as the prior implementation. Dots that
    // fail the active rule get a `.zsr-dim` class (pure-CSS opacity drop), so
    // toggling filters is a single class flip on each dot, not a re-render.
    const FILTER_RULES = {
        'all':      () => true,
        'critical': c => c === 'danger' || c === 'critical' || c === 'round' || c === 'easter-egg' || c === 'easter-egg-step' || c === 'power-on' || c === 'power-off',
        'powerups': c => c === 'powerup' || c === 'round' || c === 'easter-egg' || c === 'easter-egg-step' || c === 'power-on' || c === 'power-off',
        'economy':  c => ['weapon','weapon-abandon','box','box-pass','door','trap','build','perk','round','easter-egg','easter-egg-step','power-on','power-off'].includes(c)
    };

    const SCRUB_DEBOUNCE_MS = 50;
    const ZOOM_DEBOUNCE_MS  = 150;
    // Zoom range matches the toolbar +/- nominal cap. No canvas-cap clamp needed
    // (no canvas), but keep the same bound so the toolbar UX is identical.
    const ZOOM_MIN = 1;
    const ZOOM_MAX = 20;

    class ScrubberInstance {
        constructor(elementId, payload, dotnetRef, focusClientId, initialLaneMode) {
            this.elementId = elementId;
            this.payload = payload;
            this.dotnetRef = dotnetRef;
            this.focusClientId = focusClientId ?? null;
            this.filter = 'all';
            // Lane-mode resolution priority — same rule as the prior implementation:
            //   1. Razor-supplied `initialLaneMode` (authoritative when consumer
            //      has resolved it, e.g. dedicated match page = 'all', leaderboard
            //      card = 'qualified').
            //   2. Default to 'qualified' when any lane qualifies, else 'all'
            //      (legacy pre-qualifier match — empty stage otherwise).
            if (initialLaneMode === 'qualified' || initialLaneMode === 'all') {
                this.laneMode = initialLaneMode;
            } else {
                const anyQualified = (payload.lanes || []).some(l => l.isQualified);
                this.laneMode = anyQualified ? 'qualified' : 'all';
            }
            this.zoom = 1;
            this.scrubSeconds = payload.minSeconds;
            this._scrubDebounce = null;
            this._zoomDebounce = null;
            this._scrubDragging = false;

            // Cached span for percent math; never zero so % calcs don't NaN.
            this.span = Math.max(payload.maxSeconds - payload.minSeconds, 1);

            this._build();

            // Initial scrub time → first event of any visible lane (parity with
            // the prior implementation's behaviour). Falls back to minSeconds.
            const firstSec = this._firstEventSeconds();
            if (firstSec != null) this.setScrubTime(firstSec);
        }

        _firstEventSeconds() {
            for (const lane of this._visibleLanes()) {
                if (lane.events && lane.events.length > 0) return lane.events[0].seconds;
            }
            return null;
        }

        _visibleLanes() {
            if (this.laneMode === 'all') return this.payload.lanes;
            return this.payload.lanes.filter(l => l.isQualified);
        }

        _container() {
            return document.getElementById(this.elementId);
        }

        _hasMatchLevelEvents() {
            return (this.payload.matchLevelEvents || []).length > 0;
        }

        _pct(seconds) {
            // Percentage of the time axis the given second falls at.
            return ((seconds - this.payload.minSeconds) / this.span) * 100;
        }

        _stripColors(s) {
            return (s || '').replace(/\^[0-9]/g, '');
        }

        // ── DOM construction ───────────────────────────────────────────────────
        // We build the full subtree once via DocumentFragment and replace the host
        // contents in a single mutation. Subsequent zoom / filter / focus / scrub
        // operations are CSS-variable or class-toggle updates — no rebuilds.
        _build() {
            const host = this._container();
            if (!host) return;

            // Reset host: drop any prior render (e.g. lane-mode rebuild) and seed
            // baseline state. We own the inline style.
            host.innerHTML = '';
            host.classList.add('zsr-host');
            host.style.setProperty('--zoom', String(this.zoom));
            host.dataset.filter = this.filter;
            if (this.focusClientId != null) {
                host.dataset.focusClient = String(this.focusClientId);
            } else {
                delete host.dataset.focusClient;
            }

            const visibleLanes = this._visibleLanes();
            const showLaneLabels = visibleLanes.length > 1;

            const root = document.createElement('div');
            root.className = 'zsr-shell' + (showLaneLabels ? '' : ' zsr-shell--single');

            // ── Lane name column (left, doesn't scroll) ──
            // Position: outside the scroll-area entirely, in a sibling grid column.
            // No sticky tricks, no scroll-sync handlers.
            if (showLaneLabels) {
                const side = document.createElement('div');
                side.className = 'zsr-side';
                visibleLanes.forEach((lane, idx) => {
                    const cell = document.createElement('div');
                    cell.className = 'zsr-side-cell';
                    cell.style.setProperty('--lane-idx', String(idx));
                    cell.title = this._stripColors(lane.name);
                    cell.textContent = this._stripColors(lane.name);
                    side.appendChild(cell);
                });
                root.appendChild(side);
            }

            // ── Scroll area (right column, horizontally scrolls when zoomed) ──
            const scrollArea = document.createElement('div');
            scrollArea.className = 'zsr-scroll';
            this._scrollEl = scrollArea;

            // ── Track (the actually-zoomed surface) ──
            // Width = 100% × var(--zoom). All children use percent-based positioning
            // so the browser repositions everything via GPU compositor on zoom.
            const track = document.createElement('div');
            track.className = 'zsr-track';
            const trackHeight = this._trackHeight(visibleLanes.length);
            track.style.setProperty('--track-height', trackHeight + 'px');
            track.style.setProperty('--lane-count', String(visibleLanes.length));
            this._trackEl = track;

            // ── Round-band stripes (full-height, alternating tint) ──
            // Rendered first so they sit behind everything else in the track.
            const bands = document.createElement('div');
            bands.className = 'zsr-bands';
            (this.payload.roundBands || []).forEach((band, i) => {
                const left = this._pct(band.startSeconds);
                const width = Math.max(this._pct(band.endSeconds) - left, 0.05);
                const el = document.createElement('div');
                el.className = 'zsr-band ' + (i % 2 === 0 ? 'zsr-band--even' : 'zsr-band--odd');
                el.style.left = left + '%';
                el.style.width = width + '%';
                if (width > 1.5) {
                    const lbl = document.createElement('span');
                    lbl.className = 'zsr-band-label';
                    lbl.textContent = 'R' + band.roundNumber;
                    el.appendChild(lbl);
                }
                bands.appendChild(el);
            });
            track.appendChild(bands);

            // ── Match-level event tickband (top, EE quest markers) ──
            if (this._hasMatchLevelEvents()) {
                const tickband = document.createElement('div');
                tickband.className = 'zsr-tickband';
                const watermark = document.createElement('div');
                watermark.className = 'zsr-tickband-watermark';
                watermark.textContent = 'Easter Eggs';
                tickband.appendChild(watermark);
                this.payload.matchLevelEvents.forEach(evt => {
                    tickband.appendChild(this._buildDot(evt, /*isMatchLevel=*/true, null));
                });
                track.appendChild(tickband);
            }

            // ── Lanes (one row per visible player) ──
            const lanesEl = document.createElement('div');
            lanesEl.className = 'zsr-lanes';
            visibleLanes.forEach((lane, idx) => {
                const laneEl = document.createElement('div');
                laneEl.className = 'zsr-lane';
                laneEl.style.setProperty('--lane-idx', String(idx));
                laneEl.dataset.client = String(lane.clientId);

                // Gaps render behind events — single absolute-positioned rect each.
                (lane.gaps || []).forEach(gap => {
                    const gx = this._pct(gap.start);
                    const gw = Math.max(this._pct(gap.end) - gx, 0.05);
                    const g = document.createElement('div');
                    g.className = 'zsr-gap' + (gap.compact ? ' zsr-gap--compact' : '');
                    if (gap.compact) {
                        // Compact gaps render as a thin vertical strip at the gap
                        // midpoint — visual hint, not a span. Width clamped tight.
                        const midPct = (gx + this._pct(gap.end)) / 2;
                        const compactW = Math.max(Math.min(gw * 0.15, 0.6), 0.18);
                        g.style.left = (midPct - compactW / 2) + '%';
                        g.style.width = compactW + '%';
                    } else {
                        g.style.left = gx + '%';
                        g.style.width = gw + '%';
                    }
                    g.dataset.tooltip = gap.tooltip || '';
                    laneEl.appendChild(g);
                });

                // Events. Round-completion markers (category 'round') are skipped
                // here — the R{n} round-band labels at the top of the track
                // convey the same information visually, so the per-lane round
                // ticks were redundant clutter. Round events still ship in the
                // payload because they drive RoundBand derivation server-side
                // and the first-event-as-initial-scrub-time defaulting.
                (lane.events || []).forEach(evt => {
                    if ((evt.category || 'default') === 'round') return;
                    laneEl.appendChild(this._buildDot(evt, /*isMatchLevel=*/false, lane));
                });

                lanesEl.appendChild(laneEl);
            });
            track.appendChild(lanesEl);

            // ── Scrub cursor (vertical line + draggable handle) ──
            // Positioned via --scrub-pct CSS var so cursor moves are a single
            // var write — no layout, no JS per pixel.
            const scrub = document.createElement('div');
            scrub.className = 'zsr-scrub';
            scrub.style.setProperty('--scrub-pct', this._pct(this.scrubSeconds) + '%');
            const scrubLine = document.createElement('div');
            scrubLine.className = 'zsr-scrub-line';
            const scrubHandle = document.createElement('button');
            scrubHandle.className = 'zsr-scrub-handle';
            scrubHandle.type = 'button';
            scrubHandle.setAttribute('aria-label', 'Scrub');
            scrub.appendChild(scrubLine);
            scrub.appendChild(scrubHandle);
            track.appendChild(scrub);
            this._scrubEl = scrub;
            this._scrubHandleEl = scrubHandle;

            scrollArea.appendChild(track);
            root.appendChild(scrollArea);
            host.appendChild(root);

            this._wireHandlers();
            this._applyFilter();
            this._applyFocus();
        }

        _trackHeight(laneCount) {
            // Constants mirrored in ZombieMatchScrubber.razor.cs _minHeight — keep in sync.
            // No bottom pad: lane rows already include vertical breathing room, and
            // trailing padding bleeds into an empty-looking strip below the last lane.
            const TOP_PAD = 24;
            const TICKBAND_HEIGHT = this._hasMatchLevelEvents() ? 32 : 0;
            const LANE_ROW = 44; // matches CSS lane-row height
            return Math.max(120, TOP_PAD + TICKBAND_HEIGHT + laneCount * LANE_ROW);
        }

        _buildDot(evt, isMatchLevel, lane) {
            const cat = evt.category || 'default';
            const v = CATEGORY_VISUALS[cat] || CATEGORY_VISUALS.default;
            const left = this._pct(evt.seconds);

            if (v.tick) {
                // Round markers + unknown categories render as a thin vertical
                // tick — no glyph, no shadow. Cheaper to paint at scale.
                const el = document.createElement('div');
                el.className = 'zsr-tick';
                el.style.left = left + '%';
                el.style.setProperty('--tick-fill', v.fill);
                el.dataset.cat = cat;
                el.dataset.tooltip = evt.time + ' • ' + evt.label;
                if (lane) el.dataset.client = String(lane.clientId);
                if (isMatchLevel) el.dataset.matchLevel = 'true';
                return el;
            }

            // Event dot — button so it's keyboard-focusable + accessible by default.
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'zsr-dot' + (isMatchLevel ? ' zsr-dot--match' : '');
            btn.style.left = left + '%';
            btn.style.setProperty('--dot-fill', v.fill);
            btn.style.setProperty('--dot-z', String(v.z));
            btn.dataset.cat = cat;
            btn.dataset.seconds = String(evt.seconds);
            btn.dataset.tooltip = evt.time + ' • ' + evt.label;
            if (lane) btn.dataset.client = String(lane.clientId);
            if (isMatchLevel) btn.dataset.matchLevel = 'true';
            btn.setAttribute('aria-label', evt.label);

            if (v.icon) {
                const ico = document.createElement('i');
                ico.className = 'ph ' + v.icon + ' zsr-dot-icon';
                btn.appendChild(ico);
            }
            return btn;
        }

        // ── Interaction wiring ────────────────────────────────────────────────
        _wireHandlers() {
            const track = this._trackEl;
            const scroll = this._scrollEl;
            const handle = this._scrubHandleEl;

            // Wheel-zoom (cursor-anchored). Updates `--zoom` CSS variable on the
            // host element; browser compositor handles the layer transform. Same
            // anchor math as the prior canvas implementation.
            this._wheelHandler = (e) => {
                e.preventDefault();
                const factor = e.deltaY < 0 ? 1.15 : 1 / 1.15;
                const newZoom = Math.min(Math.max(this.zoom * factor, ZOOM_MIN), ZOOM_MAX);
                if (Math.abs(newZoom - this.zoom) < 0.01) return;

                const trackRect = track.getBoundingClientRect();
                // Position of cursor in current track-coordinate space.
                const stageX = e.clientX - trackRect.left + scroll.scrollLeft;
                const ratio = newZoom / this.zoom;

                this.zoom = newZoom;
                this._container().style.setProperty('--zoom', String(this.zoom));

                // Reposition scroll so the time at the cursor stays under the cursor.
                const newStageX = stageX * ratio;
                const viewportX = e.clientX - trackRect.left;
                scroll.scrollLeft = Math.max(0, newStageX - viewportX);

                this._notifyZoom();
            };
            scroll.addEventListener('wheel', this._wheelHandler, { passive: false });

            // Scrub-cursor drag — pointer events for unified mouse/touch.
            this._scrubPointerDown = (e) => {
                e.preventDefault();
                this._scrubDragging = true;
                handle.setPointerCapture && handle.setPointerCapture(e.pointerId);
                this._updateScrubFromClientX(e.clientX);
            };
            this._scrubPointerMove = (e) => {
                if (!this._scrubDragging) return;
                this._updateScrubFromClientX(e.clientX);
            };
            this._scrubPointerUp = (e) => {
                if (!this._scrubDragging) return;
                this._scrubDragging = false;
                handle.releasePointerCapture && handle.releasePointerCapture(e.pointerId);
            };
            handle.addEventListener('pointerdown', this._scrubPointerDown);
            handle.addEventListener('pointermove', this._scrubPointerMove);
            handle.addEventListener('pointerup', this._scrubPointerUp);
            handle.addEventListener('pointercancel', this._scrubPointerUp);

            // Click-on-track to jump scrub cursor (excluding clicks on event dots —
            // those have their own handler that ALSO sets the scrub time, but
            // emits a clientId so the side panel can pin to that player's event).
            this._trackClick = (e) => {
                const dot = e.target.closest('.zsr-dot');
                if (dot) {
                    const seconds = Number(dot.dataset.seconds);
                    const clientId = dot.dataset.client ? Number(dot.dataset.client) : 0;
                    if (this.dotnetRef) {
                        this.dotnetRef.invokeMethodAsync('OnEventClicked', clientId, seconds);
                    }
                    this.setScrubTime(seconds);
                    return;
                }
                // Click on bare track moves the cursor.
                if (e.target.closest('.zsr-scrub') || e.target.closest('.zsr-side')) return;
                this._updateScrubFromClientX(e.clientX);
            };
            track.addEventListener('click', this._trackClick);

            // Tooltip — single delegated handler reusing the existing
            // window.tooltipFixed shared element. Replaces the prior
            // _showTooltip/_hideTooltip/_removeTooltipMouseMove machinery.
            // _lastHoverEl gates re-shows: mouseover fires on every parent->
            // child transition (e.g. dot -> inner icon), and re-calling show()
            // each time causes a perceptible tooltip flicker. We only fire on
            // a genuine target change.
            this._lastHoverEl = null;
            this._mouseOver = (e) => {
                const el = e.target.closest('[data-tooltip]');
                if (!el || !el.dataset.tooltip) return;
                if (el === this._lastHoverEl) return;
                this._lastHoverEl = el;
                if (window.tooltipFixed && window.tooltipFixed.show) {
                    window.tooltipFixed.show(el, el.dataset.tooltip, 'up');
                }
            };
            this._mouseOut = (e) => {
                // mouseout bubbles; only act when leaving the element entirely
                // (relatedTarget outside the same tooltip-bearing ancestor).
                const fromEl = e.target.closest('[data-tooltip]');
                const toEl = e.relatedTarget && e.relatedTarget.closest
                    ? e.relatedTarget.closest('[data-tooltip]')
                    : null;
                if (fromEl && fromEl !== toEl) {
                    this._lastHoverEl = null;
                    if (window.tooltipFixed && window.tooltipFixed.hide) {
                        window.tooltipFixed.hide();
                    }
                }
            };
            track.addEventListener('mouseover', this._mouseOver);
            track.addEventListener('mouseout', this._mouseOut);
        }

        _updateScrubFromClientX(clientX) {
            const trackRect = this._trackEl.getBoundingClientRect();
            const x = clientX - trackRect.left;
            const pct = Math.min(Math.max(x / trackRect.width, 0), 1);
            const seconds = this.payload.minSeconds + pct * this.span;
            this.setScrubTime(seconds);
        }

        _notifyScrub() {
            if (this._scrubDebounce) clearTimeout(this._scrubDebounce);
            this._scrubDebounce = setTimeout(() => {
                if (this.dotnetRef) {
                    this.dotnetRef.invokeMethodAsync('OnScrubChanged', this.scrubSeconds, this._computeHalfWindowSeconds());
                }
            }, SCRUB_DEBOUNCE_MS);
        }

        // Half-width (in seconds) of the side-panel hit window around the scrub
        // cursor. Sized so the visual dot footprint matches the hit window: at
        // any zoom level, brushing the cursor across a dot's pixel width should
        // surface that dot in the panel. Without this, the fixed ±5s window
        // demanded near-perfect cursor-on-centre alignment at 1× zoom (where
        // 5 seconds was ~2 px while a dot is 16 px wide) and was overly lenient
        // at high zoom.
        _computeHalfWindowSeconds() {
            if (!this._scrollEl) return 5;
            const trackPx = this._scrollEl.clientWidth * this.zoom;
            if (trackPx <= 0) return 5;
            const pxPerSec = trackPx / this.span;
            // 8 px = half a regular dot, +4 px slack so edge-of-dot hovers
            // still register cleanly. Floor at 0.5s so very-high-zoom doesn't
            // collapse the window to nothing on tickband (smaller) dots.
            const slopPx = 12;
            return Math.max(0.5, slopPx / pxPerSec);
        }

        // Debounce zoom-changed Blazor roundtrips — wheel-zoom fires many times
        // during a fast spin, but the consumer only cares about the settled
        // level (toolbar +/- display).
        _notifyZoom() {
            if (this._zoomDebounce) clearTimeout(this._zoomDebounce);
            this._zoomDebounce = setTimeout(() => {
                if (this.dotnetRef) {
                    this.dotnetRef.invokeMethodAsync('OnZoomChanged', this.zoom);
                }
            }, ZOOM_DEBOUNCE_MS);
        }

        _applyFilter() {
            const rule = FILTER_RULES[this.filter] || FILTER_RULES.all;
            // Walk dots once; toggle .zsr-dim. Cheap (~6000 class flips, runs
            // only on filter change, not per-frame).
            const dots = this._trackEl.querySelectorAll('.zsr-dot, .zsr-tick');
            dots.forEach(d => {
                if (d.dataset.matchLevel === 'true') {
                    // Match-level events stay full opacity regardless of filter
                    // unless the filter explicitly excludes their category.
                }
                const cat = d.dataset.cat || 'default';
                if (rule(cat)) d.classList.remove('zsr-dim');
                else           d.classList.add('zsr-dim');
            });
        }

        _applyFocus() {
            // CSS handles the dim via [data-focus-client] selector on host, but
            // can't compare the host's data-focus-client attr value to each
            // lane's data-client attr value (no attr-comparison in CSS). So we
            // mirror the focused-lane state into a `.zsr-focus-self` class which
            // the CSS can target directly. ~6 class flips per focus change —
            // negligible.
            const host = this._container();
            const lanes = this._trackEl ? this._trackEl.querySelectorAll('.zsr-lane') : [];
            if (this.focusClientId == null) {
                delete host.dataset.focusClient;
                lanes.forEach(l => l.classList.remove('zsr-focus-self'));
            } else {
                host.dataset.focusClient = String(this.focusClientId);
                const target = String(this.focusClientId);
                lanes.forEach(l => {
                    if (l.dataset.client === target) l.classList.add('zsr-focus-self');
                    else l.classList.remove('zsr-focus-self');
                });
            }
        }

        // ── public API ─────────────────────────────────────────────────────────

        setFilter(filter) {
            if (this.filter === filter) return;
            this.filter = filter;
            this._container().dataset.filter = filter;
            this._applyFilter();
        }

        setZoom(level) {
            const clamped = Math.min(Math.max(level, ZOOM_MIN), ZOOM_MAX);
            if (Math.abs(clamped - this.zoom) < 0.01) return;

            // Anchor at viewport-centre so toolbar +/- doesn't lose the user's
            // position. Same math as wheel handler but uses centre as anchor.
            const trackRect = this._trackEl.getBoundingClientRect();
            const scroll = this._scrollEl;
            const containerRect = scroll.getBoundingClientRect();
            const viewportX = containerRect.width / 2;
            const stageX = viewportX - trackRect.left + containerRect.left + scroll.scrollLeft;

            const ratio = clamped / this.zoom;
            this.zoom = clamped;
            this._container().style.setProperty('--zoom', String(this.zoom));
            const newStageX = stageX * ratio;
            scroll.scrollLeft = Math.max(0, newStageX - viewportX);
        }

        setScrubTime(seconds) {
            this.scrubSeconds = Math.min(Math.max(seconds, this.payload.minSeconds), this.payload.maxSeconds);
            const pct = this._pct(this.scrubSeconds);
            if (this._scrubEl) this._scrubEl.style.setProperty('--scrub-pct', pct + '%');
            this._notifyScrub();
        }

        focusClient(clientId) {
            this.focusClientId = clientId;
            this._applyFocus();
        }

        setLaneMode(mode) {
            if (mode !== 'qualified' && mode !== 'all') return;
            if (this.laneMode === mode) return;
            this.laneMode = mode;
            // Lane-mode change = different lane set = full subtree rebuild. Still
            // cheap (single docFragment swap, no canvas raster). Preserves zoom +
            // filter + focus + scrub time.
            const prevZoom = this.zoom;
            const prevFilter = this.filter;
            const prevScrub = this.scrubSeconds;
            this._build();
            this.zoom = prevZoom;
            this._container().style.setProperty('--zoom', String(this.zoom));
            this.filter = prevFilter;
            this._container().dataset.filter = prevFilter;
            this._applyFilter();
            this._applyFocus();
            this.setScrubTime(prevScrub);
        }

        dispose() {
            if (this._scrollEl && this._wheelHandler) {
                this._scrollEl.removeEventListener('wheel', this._wheelHandler);
            }
            if (this._scrubHandleEl) {
                this._scrubHandleEl.removeEventListener('pointerdown', this._scrubPointerDown);
                this._scrubHandleEl.removeEventListener('pointermove', this._scrubPointerMove);
                this._scrubHandleEl.removeEventListener('pointerup', this._scrubPointerUp);
                this._scrubHandleEl.removeEventListener('pointercancel', this._scrubPointerUp);
            }
            if (this._trackEl) {
                this._trackEl.removeEventListener('click', this._trackClick);
                this._trackEl.removeEventListener('mouseover', this._mouseOver);
                this._trackEl.removeEventListener('mouseout', this._mouseOut);
            }
            if (this._scrubDebounce) clearTimeout(this._scrubDebounce);
            if (this._zoomDebounce) clearTimeout(this._zoomDebounce);

            const host = this._container();
            if (host) {
                host.innerHTML = '';
                host.classList.remove('zsr-host');
                host.removeAttribute('data-filter');
                host.removeAttribute('data-focus-client');
                host.style.removeProperty('--zoom');
            }
            // Caller-owned dotnetRef; do not dispose here.
            this.dotnetRef = null;
        }
    }

    window.zombieScrubber = {
        init(elementId, payload, dotnetRef, focusClientId, initialLaneMode) {
            if (instances.has(elementId)) {
                instances.get(elementId).dispose();
                instances.delete(elementId);
            }
            instances.set(elementId, new ScrubberInstance(elementId, payload, dotnetRef, focusClientId, initialLaneMode));
        },
        dispose(elementId) {
            const inst = instances.get(elementId);
            if (inst) {
                inst.dispose();
                instances.delete(elementId);
            }
        },
        setFilter(elementId, filter) { const i = instances.get(elementId); if (i) i.setFilter(filter); },
        setZoom(elementId, level)    { const i = instances.get(elementId); if (i) i.setZoom(level); },
        setLaneMode(elementId, mode) { const i = instances.get(elementId); if (i) i.setLaneMode(mode); },
        setScrubTime(elementId, sec) { const i = instances.get(elementId); if (i) i.setScrubTime(sec); },
        focusClient(elementId, cid)  { const i = instances.get(elementId); if (i) i.focusClient(cid); }
    };
})();
