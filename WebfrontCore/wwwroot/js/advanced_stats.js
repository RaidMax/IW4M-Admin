window.onresize = function () {
    drawPlayerModel();
}

$(document).ready(function () {
    $('.table-slide').click(function () {
        if ($(window).width() < 993) {
            $(this).prev().find('.hidden-row').toggleClass('d-none d-flex');
        } else {
            $(this).prev().find('.hidden-row-lg').toggleClass('d-none');
        }
        
        $(this).attr('data-title', '');
        $(this).attr('data-toggle', '');
 
        $(this).children('span').toggleClass('oi-chevron-top oi-chevron-bottom');
    });
    setupPerformanceGraph();
    drawPlayerModel();
})

function setupPerformanceGraph() {
    const summary = $('#client_stats_summary');
    if (summary === undefined) {
        return;
    }
    const chart = $('#client_performance_history');
    const container = $('#client_performance_history_container');
    chart.attr('height', summary.height());
    chart.attr('width', container.width());
    renderPerformanceChart();
}

function drawPlayerModel() {
    const canvas = document.getElementById('hitlocation_model');
    if (canvas === null) {
        return;
    }
    const context = canvas.getContext('2d');
    const container = $('#hitlocation_container');
    const background = new Image();
    background.onload = () => {
        const backgroundRatioX = background.width / background.height;

        canvas.height = container.height() - 28;
        canvas.width = (canvas.height * backgroundRatioX);

        const scalar = canvas.height / background.height;

        drawHitLocationChart(context, background, scalar, canvas.width, canvas.height);
    }
    background.src = '/images/stats/hit_location_model.png';
}

function buildHitLocationPosition() {
    let hitLocations = {}
    hitLocations['head'] = {
        x: 454.5,
        y: 108.5,
        width: 157,
        height: 217
    }

    hitLocations['torso_upper'] = {
        x: 457,
        y: 318,
        width: 254,
        height: 202
    }

    hitLocations['torso_lower'] = {
        x: 456.50,
        y: 581,
        width: 315,
        height: 324
    }

    hitLocations['right_leg_upper'] = {
        x: 527.5,
        y: 856.7,
        width: 149,
        height: 228
    }

    hitLocations['right_leg_lower'] = {
        x: 542,
        y: 1077.6,
        width: 120,
        height: 214
    }

    hitLocations['right_foot'] = {
        x: 558.5,
        y: 1253.5,
        width: 93,
        height: 138
    }

    hitLocations['left_leg_upper'] = {
        x: 382.5,
        y: 857,
        width: 141,
        height: 228
    }

    hitLocations['left_leg_lower'] = {
        x: 371.5,
        y: 1078,
        width: 119,
        height: 214
    }

    hitLocations['left_foot'] = {
        x: 353,
        y: 1254,
        width: 90,
        height: 138
    }

    hitLocations['left_arm_upper'] = {
        p1: {
            x: 330,
            y: 218
        },
        p2: {
            x: 330,
            y: 400
        },
        p3: {
            x: 255,
            y: 475
        },
        p4: {
            x: 165,
            y: 375
        },
        type: 'polygon'
    }

    hitLocations['right_arm_upper'] = {
        p1: {
            x: 584,
            y: 218
        },
        p2: {
            x: 584,
            y: 400
        },
        p3: {
            x: 659,
            y: 475
        },
        p4: {
            x: 749,
            y: 375
        },
        type: 'polygon'
    }

    hitLocations['left_arm_lower'] = {
        p1: {
            x: 165,
            y: 375
        },
        p2: {
            x: 255,
            y: 475
        },
        p3: {
            x: 121,
            y: 584
        },
        p4: {
            x: 30,
            y: 512
        },
        type: 'polygon'
    }

    hitLocations['right_arm_lower'] = {
        p1: {
            x: 749,
            y: 375
        },
        p2: {
            x: 659,
            y: 475
        },
        p3: {
            x: 789,
            y: 587
        },
        p4: {
            x: 876,
            y: 497
        },
        type: 'polygon'
    }

    hitLocations['left_hand'] = {
        p1: {
            x: 30,
            y: 512
        },
        p2: {
            x: 121,
            y: 584
        },
        p3: {
            x: 0,
            y: 669
        },
        p4: {
            x: 0,
            y: 582
        },
        type: 'polygon'
    }

    hitLocations['right_hand'] = {
        p1: {
            x: 789,
            y: 587
        },
        p2: {
            x: 876,
            y: 497
        },
        p3: {
            x: 905,
            y: 534
        },
        p4: {
            x: 905,
            y: 666
        },
        type: 'polygon'
    }
    return hitLocations;
}

function drawHitLocationChart(context, background, scalar, width, height) {
    context.drawImage(background, 0, 0, background.width, background.height, 0, 0, width, height);

    const hitLocations = buildHitLocationPosition();

    $.each(hitLocationData, (index, hit) => {
        let scaledPercentage = hit.percentage / maxPercentage;
        let red;
        let green = 255;

        if (scaledPercentage < 0.5) {
            red = Math.round(scaledPercentage * 255 * 2);
        } else {
            red = 255;
            green = Math.round((1 - scaledPercentage) * 255 * 2);
        }

        red = red.toString(16).padStart(2, '0');
        green = green.toString(16).padStart(2, '0');

        const color = '#' + red + green + '0077';
        const location = hitLocations[hit.name];
        
        if (location === undefined) {
            return true;
        }

        if (location.type === 'polygon') {
            drawPolygon(context, scalar, location.p1, location.p2, location.p3, location.p4, color);
        } else {
            drawRectangle(context, scalar, location.x, location.y, location.width, location.height, color);
        }
    });
}

function drawRectangle(context, scalar, x, y, width, height, color) {
    const scaledRectWidth = width * scalar;
    const scaledRectHeight = height * scalar;
    const rectX = x * scalar - (scaledRectWidth / 2);
    const rectY = y * scalar - (scaledRectHeight / 2);
    context.beginPath();
    context.fillStyle = color
    context.fillRect(rectX, rectY, scaledRectWidth, scaledRectHeight);
    context.closePath();
}

function drawPolygon(context, scalar, p1, p2, p3, p4, color) {

    const points = [p1, p2, p3, p4];

    $.each(points, (index, point) => {
        point.x = point.x * scalar;
        point.y = point.y * scalar;
    });

    context.beginPath();
    context.fillStyle = color;
    context.moveTo(p1.x, p1.y);
    context.lineTo(p2.x, p2.y);
    context.lineTo(p3.x, p3.y);
    context.lineTo(p4.x, p4.y);
    context.fill();
    context.closePath();
}

function getClosestMultiple(baseValue, value) {
    return Math.round(value / baseValue) * baseValue;
}

function renderPerformanceChart() {
    const id = 'client_performance_history';
    const data = $('#' + id).data('history');
    
    if (data === undefined) {
        return;
    }
    if (data.length <= 1) {
        // only 0 perf
        return;
    }

    const labels = [];
    const values = [];
    
    data.forEach(function (item, i) {
        labels.push(item.OccurredAt);
        values.push(item.Performance)
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
            pointHoverBackgroundColor: 'rgba(255, 255, 255, 1)',
        }]
    };

    const options = {
        defaultFontFamily: '-apple-system, BlinkMacSystemFont, "Open Sans", "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif, "Apple Color Emoji", "Segoe UI Emoji", "Segoe UI Symbol"',
        responsive: true,
        maintainAspectRatio: false,
        legend: false,
        tooltips: {
            callbacks: {
                label: context => moment.utc(context.label).local().calendar(),
                title: items => Math.round(items[0].yLabel) + ' ' + _localization["PLUGINS_STATS_COMMANDS_PERFORMANCE"],
            },
            mode: 'nearest',
            intersect: false,
            animationDuration: 0,
            cornerRadius: 0,
            displayColors: false
        },
        hover: {
            mode: 'nearest',
            intersect: false
        },
        elements: {
            line: {
                fill: false,
                borderColor: halfmoon.getPreferredMode() === "light-mode" ? 'rgba(0, 0, 0, 0.85)' : 'rgba(255, 255, 255, 0.75)',
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
                    stepSize: 3,
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
