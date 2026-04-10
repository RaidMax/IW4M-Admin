// Track chart instances by canvas ID for proper cleanup
const chartInstances = {};

// Destroy a chart instance by ID (called from Blazor dispose)
function destroyStatsChart(id) {
    if (chartInstances[id]) {
        chartInstances[id].destroy();
        delete chartInstances[id];
    }
}

function getClosestMultiple(baseValue, value) {
    return Math.round(value / baseValue) * baseValue;
}

function getStatsChart(id, rankingText, data) {
    if (!data || data.length <= 1) {
        // only 0 perf or no data
        return;
    }

    // Destroy existing chart if it exists on this canvas
    if (chartInstances[id]) {
        chartInstances[id].destroy();
        delete chartInstances[id];
    }

    // Get theme colors from shared utility
    const theme = window.chartTheme.getChartColors();

    const labels = [];
    const values = [];

    data.forEach(function (item, i) {
        // Handle both PascalCase (from MVC) and camelCase (from Blazor JS interop)
        labels.push(item.OccurredAt || item.occurredAt);
        values.push(item.Performance || item.performance);
    });

    const padding = 4;
    let dataMin = Math.min(...values);
    const dataMax = Math.max(...values);

    if (dataMax - dataMin === 0) {
        dataMin = 0;
    }

    dataMin = Math.max(0, dataMin);

    const min = getClosestMultiple(padding, dataMin - padding);
    const max = getClosestMultiple(padding, dataMax + padding);

    const chartData = {
        labels: labels,
        datasets: [{
            data: values,
            pointBackgroundColor: 'rgba(255, 255, 255, 0)',
            pointBorderColor: 'rgba(255, 255, 255, 0)',
            pointHoverRadius: 5,
            pointHoverBackgroundColor: theme.lineColor,
        }]
    };

    const options = {
        defaultFontFamily: "-apple-system, BlinkMacSystemFont, 'Open Sans', 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif, 'Apple Color Emoji', 'Segoe UI Emoji', 'Segoe UI Symbol'",
        responsive: true,
        maintainAspectRatio: false,
        legend: false,
        tooltips: {
            enabled: false,
            custom: function (tooltipModel) {
                let tooltipEl = document.getElementById('chartjs-stats-tooltip');

                const styles = getComputedStyle(document.documentElement);
                const surfaceColor = styles.getPropertyValue('--color-surface-alt').trim() || styles.getPropertyValue('--color-surface').trim() || 'hsl(0 0% 13%)';
                const lineColor = styles.getPropertyValue('--color-line').trim() || 'hsl(0 0% 25%)';
                const foregroundColor = styles.getPropertyValue('--color-foreground').trim() || 'hsl(0 0% 98%)';
                const subtleColor = styles.getPropertyValue('--color-subtle').trim() || 'hsl(0 0% 75%)';

                if (!tooltipEl) {
                    tooltipEl = document.createElement('div');
                    tooltipEl.id = 'chartjs-stats-tooltip';
                    tooltipEl.style.position = 'absolute';
                    tooltipEl.style.borderRadius = '6px';
                    tooltipEl.style.pointerEvents = 'none';
                    tooltipEl.style.zIndex = '9999';
                    tooltipEl.style.transition = 'all .1s ease';
                    tooltipEl.style.transform = 'translate(-50%, 0)';
                    tooltipEl.style.boxShadow = '0 4px 6px -1px rgba(0, 0, 0, 0.1)';
                    document.body.appendChild(tooltipEl);
                }

                tooltipEl.style.background = surfaceColor;
                tooltipEl.style.border = '1px solid ' + lineColor;

                if (tooltipModel.opacity === 0) {
                    tooltipEl.style.opacity = '0';
                    return;
                }

                if (tooltipModel.body) {
                    const value = Math.round(tooltipModel.dataPoints[0].yLabel);
                    const dateStr = moment.utc(tooltipModel.dataPoints[0].label).local().calendar();
                    tooltipEl.innerHTML =
                        '<div style="padding: 8px 12px;">' +
                        '<div style="color: ' + foregroundColor + '; font-size: 11px; font-weight: 600; margin-bottom: 4px; font-family: ui-sans-serif, system-ui, sans-serif;">' + value + ' ' + rankingText + '</div>' +
                        '<div style="color: ' + subtleColor + '; font-size: 11px; font-family: ui-sans-serif, system-ui, sans-serif;">' + dateStr + '</div>' +
                        '</div>';
                }

                const position = this._chart.canvas.getBoundingClientRect();
                tooltipEl.style.opacity = '1';
                tooltipEl.style.left = position.left + window.scrollX + tooltipModel.caretX + 'px';
                tooltipEl.style.top = position.top + window.scrollY + tooltipModel.caretY - tooltipEl.clientHeight - 10 + 'px';
            }
        },
        hover: {
            mode: 'nearest',
            intersect: false
        },
        elements: {
            line: {
                fill: false,
                borderColor: theme.lineColor,
                borderWidth: 2
            },
            point: {
                radius: 5
            }
        },
        scales: {
            xAxes: [{
                display: false,
            }],
            yAxes: [{
                gridLines: {
                    display: false
                },

                position: 'right',
                ticks: {
                    precision: 0,
                    stepSize: max - min / 2,
                    callback: function (value, index, values) {
                        if (index === values.length - 1) {
                            return min;
                        } else if (index === 0) {
                            return max;
                        } else {
                            return '';
                        }
                    },
                    fontColor: 'rgba(255, 255, 255, 0.25)'
                }
            }]
        },
        layout: {
            padding: {
                left: 15
            }
        },
    };

    // Store the new chart instance for potential future cleanup
    chartInstances[id] = new Chart(id, {
        type: 'line',
        data: chartData,
        options: options
    });
}

