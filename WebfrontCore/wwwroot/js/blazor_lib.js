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

    const primaryColor = getComputedStyle(document.documentElement).getPropertyValue('--primary') || '#007bff';
    const rgb = [0, 123, 255]; // fallback

    const fillColor = `rgba(${rgb[0]}, ${rgb[1]}, ${rgb[2]}, 0.66)`;
    const offlineFillColor = 'rgba(255, 96, 96, 0.55)';

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
                backgroundColor: 'rgba(56, 189, 248, 0.1)', // Sky-400/10
                borderColor: 'rgba(56, 189, 248, 1)',       // Sky-400
                borderWidth: 1.5,
                hoverBorderColor: 'white',
                hoverBorderWidth: 2,
                pointRadius: 0,
                pointHoverRadius: 4,
                pointBackgroundColor: 'rgba(56, 189, 248, 1)'
            },
            {
                data: offlineTime.map(history => history.cc),
                backgroundColor: createDiagonalPattern('rgba(148, 163, 184, 0.2)'), // Slate-400/20
                borderColor: 'rgba(148, 163, 184, 0.5)',                            // Slate-400/50
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
                    top: 25
                }
            },
            tooltips: {
                enabled: false, // Disable default canvas tooltip
                custom: function (tooltipModel) {
                    // Tooltip Element
                    var tooltipEl = document.getElementById('chartjs-tooltip');

                    // Create element on first render
                    if (!tooltipEl) {
                        tooltipEl = document.createElement('div');
                        tooltipEl.id = 'chartjs-tooltip';
                        tooltipEl.style.position = 'absolute';
                        tooltipEl.style.background = 'rgba(15, 23, 42, 0.95)'; // Slate-900
                        tooltipEl.style.border = '1px solid rgba(51, 65, 85, 0.5)'; // Slate-700
                        tooltipEl.style.borderRadius = '4px';
                        tooltipEl.style.pointerEvents = 'none';
                        tooltipEl.style.zIndex = '9999';
                        tooltipEl.style.transition = 'all .1s ease';
                        tooltipEl.style.transform = 'translate(-50%, 0)';
                        tooltipEl.style.boxShadow = '0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)';
                        document.body.appendChild(tooltipEl);
                    }

                    // Hide if no tooltip
                    if (tooltipModel.opacity === 0) {
                        tooltipEl.style.opacity = 0;
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
                        var titleLines = tooltipModel.title || [];
                        var bodyLines = tooltipModel.body.map(getBody);

                        var innerHtml = '<div style="padding: 8px 12px;">';

                        titleLines.forEach(function (title) {
                            // Format Title (Date)
                            var formattedTitle = moment(title).local().calendar();
                            innerHtml += '<div style="color: #f1f5f9; font-size: 11px; font-weight: 600; margin-bottom: 4px; font-family: ui-sans-serif, system-ui, sans-serif;">' + formattedTitle + '</div>';
                        });

                        bodyLines.forEach(function (body, i) {
                            // Custom Label Logic
                            var dataIndex = tooltipModel.dataPoints[i].index;
                            var datasetIndex = tooltipModel.dataPoints[i].datasetIndex;
                            var value = tooltipModel.dataPoints[i].yLabel;
                            var label = "";

                            if (datasetIndex !== 1) {
                                label = `${value} ${strings.players} | ${playerHistory[dataIndex].ma}`;
                            } else {
                                label = value === 0 ? '' : strings.unreachable;
                            }

                            if (label) {
                                innerHtml += '<div style="color: #cbd5e1; font-size: 11px; font-family: ui-sans-serif, system-ui, sans-serif;">' + label + '</div>';
                            }
                        });
                        innerHtml += '</div>';

                        tooltipEl.innerHTML = innerHtml;
                    }

                    // `this._chart.canvas`
                    var position = this._chart.canvas.getBoundingClientRect();

                    // Display, position, and set styles for font
                    tooltipEl.style.opacity = 1;
                    tooltipEl.style.left = position.left + window.pageXOffset + tooltipModel.caretX + 'px';
                    tooltipEl.style.top = position.top + window.pageYOffset + tooltipModel.caretY - tooltipEl.clientHeight - 10 + 'px'; // Shift up by height + padding
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

// Infinite scroll support for Blazor components using IntersectionObserver
window.blazorInfiniteScroll = {
    setup: function (element, dotNetRef) {
        if (!element) return;

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    dotNetRef.invokeMethodAsync('LoadMore');
                }
            });
        }, {
            root: null,
            rootMargin: '100px',
            threshold: 0.1
        });

        observer.observe(element);

        // Store observer reference for cleanup
        element._infiniteScrollObserver = observer;
    },
    remove: function (element) {
        if (element && element._infiniteScrollObserver) {
            element._infiniteScrollObserver.disconnect();
            delete element._infiniteScrollObserver;
        }
    }
};

// Toast notification wrapper for Blazor
window.blazorToast = {
    show: function (content, title, alertType, fillType, timeShown) {
        console.log('blazorToast.show called', { content, title, alertType, fillType, timeShown });

        if (typeof halfmoon === 'undefined') {
            console.error('halfmoon is not defined');
            return;
        }

        if (!halfmoon.initStickyAlert) {
            console.error('halfmoon.initStickyAlert is not available');
            return;
        }

        // Ensure stickyAlerts is initialized (required for Blazor)
        if (!halfmoon.stickyAlerts) {
            halfmoon.stickyAlerts = document.getElementsByClassName('sticky-alerts')[0];
            if (!halfmoon.stickyAlerts) {
                console.error('Could not find .sticky-alerts element');
                return;
            }
        }

        try {
            halfmoon.initStickyAlert({
                content: content,
                title: title,
                alertType: alertType || '',
                fillType: fillType || '',
                hasDismissButton: true,
                timeShown: timeShown || 5000
            });
            console.log('Toast shown successfully');
        } catch (error) {
            console.error('Error showing toast:', error);
        }
    }
};

window.initHalfmoon = function () {
    console.log('initHalfmoon called');
    if (typeof halfmoon === 'undefined') {
        console.error('halfmoon is undefined');
        return;
    }

    // Try standard initialization
    if (halfmoon.onDOMContentLoaded) {
        halfmoon.onDOMContentLoaded();
    }

    // Manual fallback: check if pageWrapper is set
    if (!halfmoon.pageWrapper) {
        console.warn('halfmoon.pageWrapper is missing after init. Attempting manual set.');
        halfmoon.pageWrapper = document.getElementsByClassName("page-wrapper")[0];

        if (halfmoon.pageWrapper) {
            console.log('halfmoon.pageWrapper set manually');
            // Re-initialize sidebar if needed
            if (halfmoon.sidebar) {
                // If sidebar object exists (should exist if halfmoon loaded), we might need to reset its state or it just uses the wrapper reference
            }
        } else {
            console.error('Could not find .page-wrapper');
        }
    } else {
        console.log('halfmoon.pageWrapper was found correctly');
    }
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
        } catch (e) { }
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
        if (settings.secondaryColorMode === 1) { // Palette
            const palette = settings.secondaryPalette || 'purple';
            doc.setAttribute('data-secondary-palette', palette);
            const hsl = this.paletteHSL[palette] || this.paletteHSL['purple'];
            doc.style.setProperty('--color-secondary-h', hsl[0]);
            doc.style.setProperty('--color-secondary-s', hsl[1] + '%');
            doc.style.setProperty('--color-secondary-l', hsl[2] + '%');
        } else { // Custom
            doc.removeAttribute('data-secondary-palette');
            doc.style.setProperty('--color-secondary-h', settings.secondaryHue);
            doc.style.setProperty('--color-secondary-s', settings.secondarySaturation + '%');
            doc.style.setProperty('--color-secondary-l', settings.secondaryLightness + '%');
        }
    },

    // Initialize and listen for navigation
    init: function () {
        const self = this;

        // Apply on load
        const settings = self.load();
        if (settings) self.apply(settings);

        // Use Blazor's enhancedload event (fires AFTER DOM patching completes)
        if (typeof Blazor !== 'undefined') {
            Blazor.addEventListener('enhancedload', () => {
                const s = self.load();
                if (s) self.apply(s);
            });
        }

        // Fallback: MutationObserver to detect when data-theme is removed
        const observer = new MutationObserver((mutations) => {
            for (const mutation of mutations) {
                if (mutation.type === 'attributes' && mutation.attributeName === 'data-theme') {
                    const current = document.documentElement.getAttribute('data-theme');
                    const saved = self.load();
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
    document.addEventListener('DOMContentLoaded', () => window.themeManager.init());
} else {
    window.themeManager.init();
}
