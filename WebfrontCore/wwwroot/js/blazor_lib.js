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
