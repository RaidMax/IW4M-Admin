function getClosestMultiple(baseValue, value) {
    return Math.round(value / baseValue) * baseValue;
}

function getStatsChart(id, rankingText, data) {
    if (!data || data.length <= 1) {
        // only 0 perf or no data
        return;
    }

    // Get theme colors from shared utility
    const theme = window.chartTheme.getChartColors();
    const tooltipConfig = window.chartTheme.getTooltipConfig();

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
            ...tooltipConfig,
            callbacks: {
                label: context => moment.utc(context.label).local().calendar(),
                title: items => Math.round(items[0].yLabel) + ' ' + rankingText
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

    new Chart(id, {
        type: 'line',
        data: chartData,
        options: options
    });
}
