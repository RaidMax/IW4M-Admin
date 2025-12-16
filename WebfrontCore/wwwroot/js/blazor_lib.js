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
                backgroundColor: fillColor,
                borderColor: primaryColor,
                borderWidth: 2,
                hoverBorderColor: 'white',
                hoverBorderWidth: 2
            },
            {
                data: offlineTime.map(history => history.cc),
                backgroundColor: createDiagonalPattern(offlineFillColor),
                borderColor: offlineFillColor,
                borderWidth: 2,
                hoverBorderColor: 'white',
                hoverBorderWidth: 2
            }],
            lineAtIndexes: mapChange,
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            legend: false,
            tooltips: {
                callbacks: {
                    title: context => moment(context[0].label).local().calendar(),
                    label: context => context.datasetIndex !== 1 ? `${context.value} ${strings.players} | ${playerHistory[context.index].ma}` : context.value === '0' ? '' : strings.unreachable,
                },
                mode: 'nearest',
                intersect: false,
                animationDuration: 0,
                cornerRadius: 0,
                displayColors: false
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
                        max: 0.5,
                        min: maxClients + 1
                    }
                }]
            },
            hover: {
                mode: 'nearest',
                intersect: false
            },
            elements: {
                point: {
                    radius: 0
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


