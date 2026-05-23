// ============================================
// UTC -> Local Time Renderer
// ============================================
// Server-rendered Blazor cannot know the browser's timezone, so dates served
// from C# always go out as UTC. This helper post-processes elements bearing
// `data-utc-time="<ISO 8601 UTC>"` and replaces their text with the user's
// local-formatted equivalent, moving the original UTC string into the title
// attribute so a hover always reveals the canonical UTC value.
//
// Attributes:
//   data-utc-time   ISO 8601 UTC timestamp (required)
//   data-utc-fmt    'time' (HH:mm:ss) | 'datetime' (full) — default 'datetime'
//
// Helper only swaps the inner text — UTC-on-hover is the caller's
// responsibility (use the Tooltip component, don't set title here, since the
// app's standard hover affordance is the custom Tooltip and we shouldn't have
// a title shadowing it).
//
// A MutationObserver picks up elements added by future Blazor renders, so
// per-render hand-wiring isn't required.
window.utcLocalTime = (function () {
    function format(date, fmt) {
        if (fmt === 'time') return date.toLocaleTimeString();
        return date.toLocaleString();
    }

    function convert(el) {
        if (!el || !el.getAttribute) return;
        const iso = el.getAttribute('data-utc-time');
        if (!iso) return;
        if (el.getAttribute('data-utc-converted') === '1') return;
        const date = new Date(iso);
        if (isNaN(date.getTime())) return;
        const fmt = el.getAttribute('data-utc-fmt') || 'datetime';
        el.textContent = format(date, fmt);
        el.setAttribute('data-utc-converted', '1');
    }

    function applyAll(root) {
        const scope = root || document;
        const els = scope.querySelectorAll('[data-utc-time]:not([data-utc-converted="1"])');
        for (let i = 0; i < els.length; i++) convert(els[i]);
    }

    function start() {
        applyAll();
        if (typeof MutationObserver === 'undefined') return;
        const obs = new MutationObserver(function (muts) {
            for (let i = 0; i < muts.length; i++) {
                const m = muts[i];
                if (m.type === 'attributes') {
                    if (m.target && m.target.getAttribute('data-utc-time')) {
                        // Re-convert if the timestamp changed under us (Blazor
                        // re-render with a new value).
                        m.target.removeAttribute('data-utc-converted');
                        convert(m.target);
                    }
                    continue;
                }
                if (m.addedNodes) {
                    for (let j = 0; j < m.addedNodes.length; j++) {
                        const n = m.addedNodes[j];
                        if (n.nodeType !== 1) continue;
                        if (n.matches && n.matches('[data-utc-time]')) convert(n);
                        if (n.querySelectorAll) {
                            const inner = n.querySelectorAll('[data-utc-time]:not([data-utc-converted="1"])');
                            for (let k = 0; k < inner.length; k++) convert(inner[k]);
                        }
                    }
                }
            }
        });
        obs.observe(document.body, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['data-utc-time']
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }

    return { applyAll: applyAll, convert: convert };
})();

// ============================================
// Visibility Observer for Component Virtualization
// ============================================
window.visibilityObserver = {
    observe: function (element, dotNetRef) {
        if (!element) return;
        const observer = new IntersectionObserver((entries) => {
            const isVisible = entries[0].isIntersecting;
            dotNetRef.invokeMethodAsync('OnVisibilityChanged', isVisible);
        }, { threshold: 0 });
        observer.observe(element);
        element._visibilityObserver = observer;
        element._dotNetRef = dotNetRef;
    },
    unobserve: function (element) {
        if (!element) return;
        if (element._visibilityObserver) {
            element._visibilityObserver.disconnect();
            element._visibilityObserver = null;
        }
        if (element._dotNetRef) {
            element._dotNetRef = null;
        }
    }
};

// ============================================
// Fixed Tooltip Positioning
// ============================================
// ============================================
// Zombie EE Badge Strip — overflow-aware collapse
// ============================================
// Per-quest EE chips render expanded by default. When the strip can't fit them
// without wrapping (titlebar narrowed by viewport, by long player names, by
// extra trophy chips, etc.), we collapse to a single "EE X/Y" aggregate chip
// that opens the modal on click. CSS-only can't detect "would wrap"; we measure
// after layout and toggle a class. ResizeObserver watches both the strip and
// its parent (parent width changes don't always re-fire on the strip itself).
window.zombieBadgeStrip = {
    _observers: new Map(),

    setup(elementId) {
        const el = document.getElementById(elementId);
        if (!el) return;
        // Avoid duplicate setup if Blazor re-invokes after a soft re-render.
        if (this._observers.has(elementId)) return;

        const measure = () => {
            // Optimistically un-collapse so we can measure the natural width of
            // the expanded chips. Without this, a strip that previously collapsed
            // would stay collapsed forever (the aggregate chip is narrower than
            // the expanded set, so scrollWidth never exceeds clientWidth).
            el.classList.remove('zm-badge-collapsed');
            // rAF so layout settles before we measure.
            requestAnimationFrame(() => {
                if (!el.isConnected) return;
                // Two overflow tests because scrollWidth alone misses cases where
                // flex-wrap kicked in and laid items out on a second row (no
                // horizontal overflow but vertical wrap occurred).
                const horizontalOverflow = el.scrollWidth > el.clientWidth + 1;
                let wrapped = false;
                if (!horizontalOverflow) {
                    // Compare offsetTop across direct children of the expanded
                    // group. If any sits below the first, flex wrapped them.
                    const expanded = el.querySelector('[data-ee-expanded]');
                    if (expanded) {
                        const kids = Array.from(expanded.children);
                        if (kids.length > 1) {
                            const baseTop = kids[0].offsetTop;
                            wrapped = kids.some(k => k.offsetTop > baseTop + 1);
                        }
                    }
                }
                if (horizontalOverflow || wrapped) {
                    el.classList.add('zm-badge-collapsed');
                }
            });
        };

        measure();
        const obs = new ResizeObserver(measure);
        obs.observe(el);
        if (el.parentElement) obs.observe(el.parentElement);
        this._observers.set(elementId, obs);
    },

    teardown(elementId) {
        const obs = this._observers.get(elementId);
        if (obs) {
            obs.disconnect();
            this._observers.delete(elementId);
        }
    }
};

window.tooltipFixed = {
    _el: null,
    _currentTrigger: null,
    _watchdog: null,
    _globalsBound: false,
    // Coordinates of the trigger's bounding rect at show time. Used by the global
    // mousemove watchdog to detect when the cursor leaves the trigger area without
    // a mouseleave event having fired (which happens when Blazor re-renders the
    // trigger element out from under the cursor — the new DOM node never receives
    // the in-flight mouseleave so without this safety net the tooltip orbits
    // forever, snapping to whatever tooltip-wrapper the cursor next enters).
    _triggerRect: null,

    _getEl: function () {
        if (!this._el) {
            this._el = document.getElementById('fixed-tooltip');
            if (!this._el) {
                this._el = document.createElement('div');
                this._el.id = 'fixed-tooltip';
                this._el.className = 'fixed z-[9999] pointer-events-none opacity-0 transition-opacity duration-150';
                document.body.appendChild(this._el);
            }
        }
        return this._el;
    },

    // Lazily wire the global safety nets — only once per page load. We attach to
    // window scroll (capture phase, so nested scroll containers also fire) plus a
    // throttled mousemove. Both call `_evictIfStale` which hides the tooltip when:
    //   1. the trigger element is no longer in the DOM (Blazor swap), OR
    //   2. the cursor has left the trigger's last-known bounding box.
    // Either condition means the tooltip is "orphaned" and should disappear.
    _bindGlobals: function () {
        if (this._globalsBound) return;
        this._globalsBound = true;

        // Capture-phase scroll listener catches any scrolling ancestor — nested
        // scrollable cards, the document, modal backdrops, anything. The trigger
        // moves on the page during scroll so its rect is stale; just hide.
        window.addEventListener('scroll', () => {
            if (this._currentTrigger) this.hide();
        }, true);

        // Cheap mousemove guard — only does work if a tooltip is currently shown.
        // We check the trigger's rect against cursor position, NOT element-from-
        // point, because the trigger may be obscured by inner content (an icon
        // child swallowing the hit) and elementFromPoint would lie about it.
        window.addEventListener('mousemove', (e) => {
            if (!this._currentTrigger || !this._triggerRect) return;
            const r = this._triggerRect;
            // 4px slack handles sub-pixel rendering and tiny mouse tracking gaps
            // that would otherwise flicker the tooltip on the trigger boundary.
            if (e.clientX < r.left - 4 || e.clientX > r.right + 4 ||
                e.clientY < r.top  - 4 || e.clientY > r.bottom + 4) {
                this.hide();
            }
        }, { passive: true });
    },

    // Periodic isConnected check — catches the case where the trigger element is
    // removed from the DOM while the cursor is stationary (no mousemove to fire
    // the bounds check). Fires every ~200ms while a tooltip is visible.
    _startWatchdog: function () {
        this._stopWatchdog();
        this._watchdog = setInterval(() => {
            if (!this._currentTrigger) { this._stopWatchdog(); return; }
            if (!this._currentTrigger.isConnected) this.hide();
        }, 200);
    },

    _stopWatchdog: function () {
        if (this._watchdog) {
            clearInterval(this._watchdog);
            this._watchdog = null;
        }
    },

    show: function (triggerElement, text, direction) {
        // Plain-text tooltip. Escape HTML so user-supplied strings can't
        // inject markup, then convert literal \n to <br> so multi-line
        // tooltip strings render as proper breaks (without this, newlines
        // collapse to whitespace and the body reads as one runon line).
        const safe = this._escapeHtml(text).replace(/\r?\n/g, '<br>');
        this._render(triggerElement, safe, direction, 'text-center');
    },

    showRich: function (triggerElement, direction) {
        // Rich-content tooltip — source HTML lives in a hidden div inside
        // the trigger wrapper (rendered server-side by the Tooltip
        // component's BodyContent fragment). Reading its innerHTML lets
        // callers use full Blazor-rendered markup (lists, icons, structured
        // rows) without us hand-encoding it through DotNet→JS strings.
        const bodyEl = triggerElement.querySelector(':scope > [data-tooltip-body]');
        if (!bodyEl) return;
        // left-align by default — structured bodies (lists of items) read
        // better flush-left than the plain-text center alignment.
        this._render(triggerElement, bodyEl.innerHTML, direction, 'text-left');
    },

    _render: function (triggerElement, innerHtml, direction, alignClass) {
        this._bindGlobals();
        const el = this._getEl();
        const rect = triggerElement.getBoundingClientRect();

        el.innerHTML =
            '<div class="bg-surface-alt text-foreground text-xs px-3 py-2 rounded-lg shadow-xl border border-line w-max max-w-[200px] md:max-w-[320px] ' + (alignClass || 'text-center') + ' whitespace-normal break-words">' +
            innerHtml +
            '</div>' +
            '<div class="' + this._arrowClass(direction) + '"></div>';

        el.style.opacity = '0';
        el.style.display = 'block';

        // Measure tooltip size after rendering content
        const tipRect = el.getBoundingClientRect();
        let top, left;

        switch (direction || 'up') {
            case 'down':
                top = rect.bottom + 8;
                left = rect.left + rect.width / 2 - tipRect.width / 2;
                break;
            case 'left':
                top = rect.top + rect.height / 2 - tipRect.height / 2;
                left = rect.left - tipRect.width - 8;
                break;
            case 'right':
                top = rect.top + rect.height / 2 - tipRect.height / 2;
                left = rect.right + 8;
                break;
            default: // up
                top = rect.top - tipRect.height - 8;
                left = rect.left + rect.width / 2 - tipRect.width / 2;
                break;
        }

        // Clamp to viewport
        left = Math.max(4, Math.min(left, window.innerWidth - tipRect.width - 4));
        top = Math.max(4, top);

        el.style.left = left + 'px';
        el.style.top = top + 'px';
        el.style.opacity = '1';

        // Track the active trigger + its rect for the global guards.
        this._currentTrigger = triggerElement;
        this._triggerRect = rect;
        this._startWatchdog();
    },

    hide: function () {
        const el = this._getEl();
        el.style.opacity = '0';
        this._currentTrigger = null;
        this._triggerRect = null;
        this._stopWatchdog();
    },

    _escapeHtml: function (text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    },

    _arrowClass: function (direction) {
        const base = 'absolute w-2 h-2 bg-surface-alt border-line rotate-45 ';
        switch (direction || 'up') {
            case 'down':
                return base + 'left-1/2 -translate-x-1/2 -top-1 border-l border-t';
            case 'left':
                return base + 'top-1/2 -translate-y-1/2 -right-1 border-t border-r';
            case 'right':
                return base + 'top-1/2 -translate-y-1/2 -left-1 border-b border-l';
            default:
                return base + 'left-1/2 -translate-x-1/2 -bottom-1 border-r border-b';
        }
    }
};

// ============================================
// Global Navigation Loading Bar
// ============================================
window.loadingBar = {
    _element: null,
    _isVisible: false,

    _getElement: function () {
        if (!this._element) {
            this._element = document.getElementById('mainLoadingBar');
        }
        return this._element;
    },

    show: function () {
        const bar = this._getElement();
        if (!bar || this._isVisible) return;

        this._isVisible = true;
        bar.classList.remove('hidden', 'loading-bar-complete');
        bar.classList.add('loading-bar-active');
    },

    hide: function () {
        const bar = this._getElement();
        if (!bar || !this._isVisible) return;

        this._isVisible = false;
        bar.classList.remove('loading-bar-active');
        bar.classList.add('loading-bar-complete');

        // Remove complete class after animation
        setTimeout(() => {
            bar.classList.add('hidden');
            bar.classList.remove('loading-bar-complete');
        }, 300);
    },

    // Auto-initialize: Hook into Blazor enhanced navigation
    init: function () {
        if (this._initialized) return;
        this._initialized = true;

        // Show loading bar when enhanced navigation starts
        document.addEventListener('click', (e) => {
            const link = e.target.closest('a[href]');
            if (!link) return;

            const href = link.getAttribute('href');
            // Only trigger for internal links (not external, not hash-only, not download, not actions)
            if (href &&
                !href.startsWith('http') &&
                !href.startsWith('#') &&
                !href.startsWith('javascript:') &&
                !link.hasAttribute('download') &&
                !link.hasAttribute('target') &&
                !link.hasAttribute('data-no-loading-bar') &&
                !link.closest('[data-no-loading-bar]')) {
                this.show();
            }
        });

        // Hide loading bar when enhanced navigation completes
        if (typeof Blazor !== 'undefined') {
            Blazor.addEventListener('enhancedload', () => {
                this.hide();
            });
        }
    }
};

// Initialize loading bar when Blazor is ready
if (typeof Blazor !== 'undefined') {
    window.loadingBar.init();
} else {
    document.addEventListener('DOMContentLoaded', () => {
        // Wait a bit for Blazor to initialize
        setTimeout(() => window.loadingBar.init(), 100);
    });
}

// ============================================
// Chart Theme Utility
// ============================================
window.chartTheme = (function () {
    // Helper to convert CSS color to rgba (handles oklch, hsl, etc.)
    const colorToRgba = (color, alpha) => {
        // Use canvas 2D context - it always outputs rgb() format regardless of input
        const canvas = document.createElement('canvas');
        canvas.width = canvas.height = 1;
        const ctx = canvas.getContext('2d');
        ctx.fillStyle = color;
        ctx.fillRect(0, 0, 1, 1);
        const [r, g, b] = ctx.getImageData(0, 0, 1, 1).data;
        return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    };

    // Get current theme colors from CSS variables
    const getColors = () => {
        const styles = getComputedStyle(document.documentElement);
        return {
            primary: styles.getPropertyValue('--color-primary').trim() || 'hsl(217 91% 60%)',
            secondary: styles.getPropertyValue('--color-secondary').trim() || 'hsl(271 91% 65%)',
            muted: styles.getPropertyValue('--color-muted').trim() || 'hsl(0 0% 55%)',
            surface: styles.getPropertyValue('--color-surface').trim() || 'hsl(0 0% 13%)',
            line: styles.getPropertyValue('--color-line').trim() || 'hsl(0 0% 25%)',
            foreground: styles.getPropertyValue('--color-foreground').trim() || 'hsl(0 0% 98%)',
            subtle: styles.getPropertyValue('--color-subtle').trim() || 'hsl(0 0% 75%)'
        };
    };

    // Get chart-ready colors with alpha values
    const getChartColors = () => {
        const colors = getColors();
        return {
            lineColor: colorToRgba(colors.primary, 0.85),
            fillColor: colorToRgba(colors.primary, 0.1),
            tickColor: colorToRgba(colors.muted, 0.35),
            surfaceColor: colorToRgba(colors.surface, 1),
            borderColor: colorToRgba(colors.line, 1),
            foregroundColor: colorToRgba(colors.foreground, 1),
            subtleColor: colorToRgba(colors.subtle, 1)
        };
    };

    // Get standard tooltip config for Chart.js
    const getTooltipConfig = () => {
        const chartColors = getChartColors();
        return {
            mode: 'nearest',
            intersect: false,
            animationDuration: 0,
            cornerRadius: 6,
            displayColors: false,
            backgroundColor: chartColors.surfaceColor,
            borderColor: chartColors.borderColor,
            borderWidth: 1,
            titleFontColor: chartColors.foregroundColor,
            bodyFontColor: chartColors.subtleColor,
            xPadding: 12,
            yPadding: 8
        };
    };

    return {
        colorToRgba,
        getColors,
        getChartColors,
        getTooltipConfig
    };
})();

function createDiagonalPattern(color = 'black') {
    let shape = document.createElement('canvas');
    shape.width = 10;
    shape.height = 10;
    let c = shape.getContext('2d');
    c.strokeStyle = color;
    c.beginPath();
    c.moveTo(2, 0);
    c.lineTo(10, 8);
    c.stroke();
    c.beginPath();
    c.moveTo(0, 8);
    c.lineTo(2, 10);
    c.stroke();
    return c.createPattern(shape, 'repeat');
}

window.initServerChart = function (elementId, playerHistory, maxClients, strings) {
    const canvas = document.getElementById(elementId);
    if (!canvas) return;

    const card = canvas.closest('.card');
    const width = card ? card.clientWidth : canvas.parentElement.clientWidth;
    canvas.setAttribute('width', width);

    // Get theme colors from shared utility
    const colors = window.chartTheme.getColors();
    const colorToRgba = window.chartTheme.colorToRgba;

    const onlineBorderColor = colorToRgba(colors.primary, 1);
    const onlineFillColor = colorToRgba(colors.primary, 0.1);
    const offlineBorderColor = colorToRgba(colors.secondary, 0.5);
    const offlinePatternColor = colorToRgba(colors.secondary, 0.2);

    const onlineTime = [];
    const offlineTime = [];
    const mapChange = [];
    let lastMap = '';

    playerHistory.forEach((elem, i) => {
        if (elem.ma !== lastMap) {
            mapChange.push(i);
            lastMap = elem;
        }

        if (elem.ci) {
            offlineTime.push({
                cc: maxClients,
                ts: elem.ts
            });

            onlineTime.push({
                cc: 0,
                ts: elem.ts
            })
        } else {
            offlineTime.push({
                cc: 0,
                ts: elem.ts
            });

            onlineTime.push(elem)
        }
    });

    let animationProgress = 0;
    let initialAnimationComplete = false;

    return new Chart(canvas, {
        type: 'line',
        data: {
            labels: playerHistory.map(history => history.ts),
            datasets: [{
                data: onlineTime.map(history => history.cc),
                backgroundColor: onlineFillColor,
                borderColor: onlineBorderColor,
                borderWidth: 1.5,
                hoverBorderColor: 'white',
                hoverBorderWidth: 2,
                pointRadius: 0,
                pointHoverRadius: 4,
                pointBackgroundColor: onlineBorderColor
            },
            {
                data: offlineTime.map(history => history.cc),
                backgroundColor: createDiagonalPattern(offlinePatternColor),
                borderColor: offlineBorderColor,
                borderWidth: 1.5,
                hoverBorderColor: 'white',
                hoverBorderWidth: 2,
                pointRadius: 0,
                pointHoverRadius: 0
            }],
            lineAtIndexes: mapChange,
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            legend: false,
            layout: {
                padding: {
                    top: 5
                }
            },
            tooltips: {
                enabled: false, // Disable default canvas tooltip
                custom: function (tooltipModel) {
                    let tooltipEl = document.getElementById('chartjs-tooltip');

                    // Get theme colors
                    const tooltipStyles = getComputedStyle(document.documentElement);
                    const surfaceColor = tooltipStyles.getPropertyValue('--color-surface').trim() || 'hsl(0 0% 13%)';
                    const lineColor = tooltipStyles.getPropertyValue('--color-line').trim() || 'hsl(0 0% 25%)';
                    const foregroundColor = tooltipStyles.getPropertyValue('--color-foreground').trim() || 'hsl(0 0% 98%)';
                    const subtleColor = tooltipStyles.getPropertyValue('--color-subtle').trim() || 'hsl(0 0% 75%)';

                    // Create element on first render
                    if (!tooltipEl) {
                        tooltipEl = document.createElement('div');
                        tooltipEl.id = 'chartjs-tooltip';
                        tooltipEl.style.position = 'absolute';
                        tooltipEl.style.borderRadius = '6px';
                        tooltipEl.style.pointerEvents = 'none';
                        tooltipEl.style.zIndex = '9999';
                        tooltipEl.style.transition = 'all .1s ease';
                        tooltipEl.style.transform = 'translate(-50%, 0)';
                        tooltipEl.style.boxShadow = '0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)';
                        document.body.appendChild(tooltipEl);
                    }

                    // Update colors dynamically (in case theme changed)
                    tooltipEl.style.background = surfaceColor;
                    tooltipEl.style.border = `1px solid ${lineColor}`;

                    // Hide if no tooltip
                    if (tooltipModel.opacity === 0) {
                        tooltipEl.style.opacity = '0';
                        return;
                    }

                    // Set caret Position
                    tooltipEl.classList.remove('above', 'below', 'no-transform');
                    if (tooltipModel.yAlign) {
                        tooltipEl.classList.add(tooltipModel.yAlign);
                    } else {
                        tooltipEl.classList.add('no-transform');
                    }

                    function getBody(bodyItem) {
                        return bodyItem.lines;
                    }

                    // Set Text
                    if (tooltipModel.body) {
                        const titleLines = tooltipModel.title || [];
                        const bodyLines = tooltipModel.body.map(getBody);

                        let innerHtml = '<div style="padding: 8px 12px;">';

                        titleLines.forEach(function (title) {
                            // Format Title (Date)
                            const formattedTitle = moment(title).local().calendar();
                            innerHtml += `<div style="color: ${foregroundColor}; font-size: 11px; font-weight: 600; margin-bottom: 4px; font-family: ui-sans-serif, system-ui, sans-serif;">` + formattedTitle + '</div>';
                        });

                        bodyLines.forEach(function (body, i) {
                            // Custom Label Logic
                            const dataIndex = tooltipModel.dataPoints[i].index;
                            const datasetIndex = tooltipModel.dataPoints[i].datasetIndex;
                            const value = tooltipModel.dataPoints[i].yLabel;
                            let label;

                            if (datasetIndex !== 1) {
                                label = `${value} ${strings.players} • ${playerHistory[dataIndex].ma}`;
                            } else {
                                label = value === 0 ? '' : strings.unreachable;
                            }

                            if (label) {
                                innerHtml += `<div style="color: ${subtleColor}; font-size: 11px; font-family: ui-sans-serif, system-ui, sans-serif;">` + label + '</div>';
                            }
                        });
                        innerHtml += '</div>';

                        tooltipEl.innerHTML = innerHtml;
                    }

                    // `this._chart.canvas`
                    const position = this._chart.canvas.getBoundingClientRect();

                    // Display, position, and set styles for font
                    tooltipEl.style.opacity = '1';
                    tooltipEl.style.left = position.left + window.scrollX + tooltipModel.caretX + 'px';
                    tooltipEl.style.top = position.top + window.scrollY + tooltipModel.caretY - tooltipEl.clientHeight - 10 + 'px'; // Shift up by height + padding
                    tooltipEl.style.fontFamily = tooltipModel._bodyFontFamily;
                    tooltipEl.style.fontSize = tooltipModel.bodyFontSize + 'px';
                    tooltipEl.style.fontStyle = tooltipModel._bodyFontStyle;
                }
            },
            scales: {
                xAxes: [{
                    display: false,
                }],
                yAxes: [{
                    display: false,
                    gridLines: {
                        display: false
                    },
                    ticks: {
                        beginAtZero: true,
                        suggestedMax: maxClients
                    }
                }]
            },
            hover: {
                mode: 'nearest',
                intersect: false
            },
            elements: {
                point: {
                    radius: 0,
                    hitRadius: 10,
                    hoverRadius: 4
                },
                line: {
                    tension: 0.3 // Smooth curves slightly
                }
            },
            animation: {
                duration: 1000,
                onProgress: function (context) {
                    animationProgress = context.currentStep / context.numSteps;
                    if (animationProgress >= 1) {
                        initialAnimationComplete = true;
                    }
                }
            }
        },
    });
}

// Store chart instances for updates
window.serverCharts = window.serverCharts || {};

// Destroy a chart instance and remove from cache
window.destroyServerChart = function (elementId) {
    const chart = window.serverCharts[elementId];
    if (chart) {
        chart.destroy();
        delete window.serverCharts[elementId];
    }
};

// Update existing chart with new data
window.updateServerChart = function (elementId, playerHistory, maxClients, strings) {
    const canvas = document.getElementById(elementId);
    if (!canvas) return; // Canvas not in DOM

    const chart = window.serverCharts[elementId];

    // Check if the cached chart's canvas is still the same element in the DOM
    // Blazor navigation can create new canvas elements with the same ID
    if (!chart || chart.canvas !== canvas) {
        // Chart doesn't exist or canvas was replaced, initialize new chart
        if (chart) {
            // Destroy old chart to prevent memory leaks
            chart.destroy();
        }
        window.serverCharts[elementId] = window.initServerChart(elementId, playerHistory, maxClients, strings);
        return;
    }

    // Prepare data same way as init
    const onlineTime = [];
    const offlineTime = [];
    const mapChange = [];
    let lastMap = '';

    playerHistory.forEach((elem, i) => {
        if (elem.ma !== lastMap) {
            mapChange.push(i);
            lastMap = elem;
        }

        if (elem.ci) {
            offlineTime.push({
                cc: maxClients,
                ts: elem.ts
            });

            onlineTime.push({
                cc: 0,
                ts: elem.ts
            })
        } else {
            offlineTime.push({
                cc: 0,
                ts: elem.ts
            });

            onlineTime.push(elem)
        }
    });

    // Update chart data
    chart.data.labels = playerHistory.map(history => history.ts);
    chart.data.datasets[0].data = onlineTime.map(history => history.cc);
    chart.data.datasets[1].data = offlineTime.map(history => history.cc);
    chart.data.lineAtIndexes = mapChange;

    // Update chart (with minimal animation for smooth transitions)
    chart.update({
        duration: 200,
        easing: 'easeInOutQuad'
    });
}

window.processLogin = function (url) {
    return fetch(url)
        .then(response => {
            if (response.ok) {
                location.reload();
                return "OK";
            } else {
                return response.text();
            }
        })
        .catch(error => {
            return "Error: " + error;
        });
}

window.processLoginPost = function (url, body) {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const formData = new URLSearchParams();

    // Convert object to FormData
    for (const key in body) {
        if (body.hasOwnProperty(key)) {
            formData.append(key, body[key]);
        }
    }

    const headers = {
        'Content-Type': 'application/x-www-form-urlencoded'
    };

    if (token) {
        headers['RequestVerificationToken'] = token;
    }

    return fetch(url, {
        method: 'POST',
        headers: headers,
        body: formData.toString()
    })
        .then(async response => {
            if (response.ok) {
                const text = await response.text();
                if (text === "2FA_ENROLLMENT_REQUIRED") {
                    return text;
                }
                location.reload();
                return "OK";
            } else {
                return response.text();
            }
        })
        .catch(error => {
            return "Error: " + error;
        });
}


window.themeManager = {
    // HSL values for standard Tailwind palettes (500 shade base)
    paletteHSL: {
        'slate': [215, 16, 47], 'gray': [220, 9, 46], 'zinc': [240, 4, 46],
        'neutral': [0, 0, 45], 'stone': [28, 6, 44], 'red': [0, 84, 60],
        'orange': [25, 95, 53], 'amber': [38, 92, 50], 'yellow': [48, 96, 53],
        'lime': [84, 81, 44], 'green': [142, 76, 36], 'emerald': [160, 84, 39],
        'teal': [173, 58, 39], 'cyan': [189, 94, 43], 'sky': [199, 89, 48],
        'blue': [217, 91, 60], 'indigo': [239, 84, 67], 'violet': [258, 57, 66],
        'purple': [271, 91, 65], 'fuchsia': [292, 84, 61], 'pink': [335, 78, 60],
        'rose': [343, 89, 56]
    },

    // Helper: HEX to HSL
    hexToHSL: function (hex) {
        let result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
        if (!result) return null;
        let r = parseInt(result[1], 16);
        let g = parseInt(result[2], 16);
        let b = parseInt(result[3], 16);
        r /= 255;
        g /= 255;
        b /= 255;
        let max = Math.max(r, g, b), min = Math.min(r, g, b);
        let h, s, l = (max + min) / 2;
        if (max === min) {
            h = s = 0;
        } else {
            let d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            switch (max) {
                case r:
                    h = (g - b) / d + (g < b ? 6 : 0);
                    break;
                case g:
                    h = (b - r) / d + 2;
                    break;
                case b:
                    h = (r - g) / d + 4;
                    break;
            }
            h /= 6;
        }
        return [Math.round(h * 360), Math.round(s * 100), Math.round(l * 100)];
    },

    load: function () {
        try {
            const saved = localStorage.getItem('themeSettings');
            return saved ? JSON.parse(saved) : null;
        } catch (e) {
            return null;
        }
    },

    save: function (settings) {
        try {
            localStorage.setItem('themeSettings', JSON.stringify(settings));
        } catch (e) {
        }
    },

    apply: function (settings) {
        if (!settings) return;
        const doc = document.documentElement;

        // Apply preset
        if (settings.preset) {
            doc.setAttribute('data-theme', settings.preset);
        }

        // Apply primary color
        if (settings.primaryColorMode === 1) { // Palette
            const palette = settings.primaryPalette || 'blue';
            doc.setAttribute('data-primary-palette', palette);
            const hsl = this.paletteHSL[palette] || this.paletteHSL['blue'];
            doc.style.setProperty('--color-primary-h', hsl[0]);
            doc.style.setProperty('--color-primary-s', hsl[1] + '%');
            doc.style.setProperty('--color-primary-l', hsl[2] + '%');
        } else { // Custom
            doc.removeAttribute('data-primary-palette');
            doc.style.setProperty('--color-primary-h', settings.primaryHue);
            doc.style.setProperty('--color-primary-s', settings.primarySaturation + '%');
            doc.style.setProperty('--color-primary-l', settings.primaryLightness + '%');
        }

        // Apply secondary color
        // For minimal preset, force grey (no saturation) to maintain monochrome look
        if (settings.preset === 'minimal') {
            doc.removeAttribute('data-secondary-palette');
            doc.style.setProperty('--color-secondary-h', '0');
            doc.style.setProperty('--color-secondary-s', '0%');
            doc.style.setProperty('--color-secondary-l', '60%');
        } else if (settings.secondaryColorMode === 1) { // Palette
            const palette = settings.secondaryPalette || 'purple';
            doc.setAttribute('data-secondary-palette', palette);
            const hsl = this.paletteHSL[palette] || this.paletteHSL['purple'];
            doc.style.setProperty('--color-secondary-h', hsl[0]);
            doc.style.setProperty('--color-secondary-s', hsl[1] + '%');
            doc.style.setProperty('--color-secondary-l', hsl[2] + '%');
        } else { // Custom
            doc.removeAttribute('data-secondary-palette');
            // Ensure custom values have fallbacks
            const hue = settings.secondaryHue ?? 271;
            const sat = settings.secondarySaturation ?? 91;
            const lit = settings.secondaryLightness ?? 65;
            doc.style.setProperty('--color-secondary-h', hue);
            doc.style.setProperty('--color-secondary-s', sat + '%');
            doc.style.setProperty('--color-secondary-l', lit + '%');
        }
    },

    // Initialize and listen for navigation
    init: function (serverSettings) {
        const self = this;
        let settings = self.load();

        const parseServerColor = (colorName) => {
            if (!colorName) return { mode: 1, palette: 'blue' };
            // Check if it's a known palette key
            if (self.paletteHSL[colorName.toLowerCase()]) {
                return { mode: 1, palette: colorName.toLowerCase() };
            }
            // Check if it's a HEX code
            if (colorName.startsWith('#')) {
                const hsl = self.hexToHSL(colorName);
                if (hsl) {
                    return {
                        mode: 0,
                        hue: hsl[0],
                        sat: hsl[1],
                        lit: hsl[2]
                    };
                }
            }
            // Fallback for unparsed or standard "blue"
            return { mode: 1, palette: 'blue' };
        };

        const createSettingsFromServer = (srv) => {
            const primary = parseServerColor(srv.primaryColor);
            const secondary = parseServerColor(srv.secondaryColor);

            return {
                preset: srv.preset || 'minimal',
                primaryColorMode: primary.mode,
                primaryPalette: primary.mode === 1 ? primary.palette : undefined,
                primaryHue: primary.mode === 0 ? primary.hue : undefined,
                primarySaturation: primary.mode === 0 ? primary.sat : undefined,
                primaryLightness: primary.mode === 0 ? primary.lit : undefined,

                secondaryColorMode: secondary.mode,
                secondaryPalette: secondary.mode === 1 ? secondary.palette : undefined,
                secondaryHue: secondary.mode === 0 ? secondary.hue : undefined,
                secondarySaturation: secondary.mode === 0 ? secondary.sat : undefined,
                secondaryLightness: secondary.mode === 0 ? secondary.lit : undefined
            };
        };

        if (serverSettings) {
            if (serverSettings.preventUserCustomization) {
                // Locked: Force server settings
                settings = createSettingsFromServer(serverSettings);
            } else if (!settings) {
                // No local customization: Use server defaults as starting point
                settings = createSettingsFromServer(serverSettings);
            }
        }

        // Apply
        if (settings) self.apply(settings);

        if (this._initialized) return;
        this._initialized = true;

        // Use Blazor's enhancedload event (fires AFTER DOM patching completes)
        if (typeof Blazor !== 'undefined') {
            Blazor.addEventListener('enhancedload', () => {
                const srv = window.serverThemeSettings;
                let s = self.load();

                if (srv && srv.preventUserCustomization) {
                    s = createSettingsFromServer(srv);
                }

                if (s) self.apply(s);
            });
        }

        // Fallback: MutationObserver to detect when data-theme is removed
        const observer = new MutationObserver((mutations) => {
            for (const mutation of mutations) {
                if (mutation.type === 'attributes' && mutation.attributeName === 'data-theme') {
                    const current = document.documentElement.getAttribute('data-theme');
                    let saved = self.load();

                    // Respect lock in observer too
                    const srv = window.serverThemeSettings;
                    if (srv && srv.preventUserCustomization) {
                        saved = createSettingsFromServer(srv);
                    }

                    if (saved && saved.preset && current !== saved.preset) {
                        self.apply(saved);
                    }
                }
            }
        });

        observer.observe(document.documentElement, {
            attributes: true,
            attributeFilter: ['data-theme']
        });
    }
};

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    // Note: serverThemeSettings might not be ready if script is deferred, 
    // but in App.razor it's immediate. 
    // We rely on the inline script in App.razor calling init() explicitly.
    // We KEEP this listener just in case but usually the inline script runs first? 
    // Actually inline runs after parsing. 
    // We will remove the auto-init here to avoid double-init race conditions 
    // since App.razor now calls it explicitly with args.
    // document.addEventListener('DOMContentLoaded', () => window.themeManager.init());
} else {
    // window.themeManager.init();
}

// Dynamic action handler for ScriptPlugin interactions
// This intercepts clicks on .profile-action elements that have data-action="DynamicAction"
// and routes them to Blazor's ActionService instead of the legacy jQuery/HalfMoon modal
window.setupDynamicActionHandlers = function (dotNetRef) {
    console.log('[DynamicAction] Setting up handlers, dotNetRef:', !!dotNetRef);

    // Use event delegation on document to catch dynamically added elements
    const handler = function (e) {
        const target = e.target.closest('.profile-action');
        if (!target) return;

        console.log('[DynamicAction] Click detected on:', target);

        e.preventDefault();
        e.stopPropagation();
        e.stopImmediatePropagation();

        const action = target.dataset.action;
        const actionId = target.dataset.actionId ? parseInt(target.dataset.actionId) : null;
        let actionMeta = target.dataset.actionMeta;

        console.log('[DynamicAction] action:', action, 'actionId:', actionId, 'meta:', actionMeta);

        // Decode the meta if it was URI encoded
        if (actionMeta) {
            try {
                actionMeta = decodeURIComponent(actionMeta);
            } catch (ex) {
                // Already decoded or invalid
            }
        }

        // Call Blazor to handle the action
        console.log('[DynamicAction] Invoking Blazor HandleDynamicAction');
        dotNetRef.invokeMethodAsync('HandleDynamicAction', action, actionId, actionMeta)
            .then(() => console.log('[DynamicAction] Blazor invocation succeeded'))
            .catch(err => console.error('[DynamicAction] Blazor invocation failed:', err));

        return false;
    };

    // Remove any existing handler to prevent duplicates
    if (window._dynamicActionHandler) {
        document.removeEventListener('click', window._dynamicActionHandler, true);
    }
    window._dynamicActionHandler = handler;
    document.addEventListener('click', handler, true);
    console.log('[DynamicAction] Handler attached');
};

// ============================================
// Modifier-key state tracking (Ctrl / Cmd)
// ============================================
// Toggles `mod-key-down` on <body> while either modifier is held. Used to
// swap the play-icon for a copy-icon on server-card connect links so the
// "Ctrl+click to copy connect command" affordance is discoverable.
// Window blur clears the class so a tab-out doesn't leave it stuck.
(function () {
    const setModState = (down) => {
        document.body.classList.toggle('mod-key-down', down);
    };
    document.addEventListener('keydown', e => {
        if (e.key === 'Control' || e.key === 'Meta') setModState(true);
    });
    document.addEventListener('keyup', e => {
        if (e.key === 'Control' || e.key === 'Meta') setModState(false);
    });
    window.addEventListener('blur', () => setModState(false));
})();

window.copyToClipboard = async function (text) {
    if (!text) return false;
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (err) {
        console.error('[copyToClipboard] Clipboard write failed:', err);
        return false;
    }
};

window.openProtocolUrl = function (url) {
    if (!url) return;
    window.location.href = url;
};
