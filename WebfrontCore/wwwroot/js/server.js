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

function getPlayerHistoryChart(playerHistory, i, width, maxClients) {
    const primaryColor = $('title').css('background-color');
    const rgb = primaryColor.match(/\d+/g);
    const fillColor = `rgba(${rgb[0]}, ${rgb[1]}, ${rgb[2]}, 0.66)`;
    const offlineFillColor = 'rgba(255, 96, 96, 0.55)';

    const onlineTime = [];
    const offlineTime = [];
    const mapChange = [];
    let lastMap = '';

    playerHistory.forEach((elem, i) => {
        if (elem.map !== lastMap) {
            mapChange.push(i);
            lastMap = elem.map;
        }

        if (elem.connectionInterrupted) {
            offlineTime.push({
                clientCount: maxClients,
                timeString: elem.timeString
            });

            onlineTime.push({
                clientCount: 0,
                timeString: elem.timeString
            })
        } else {
            offlineTime.push({
                clientCount: 0,
                timeString: elem.timeString
            });

            onlineTime.push(elem)
        }
    });

    let animationProgress = 0;
    let initialAnimationComplete = false;
    /*const originalLineDraw = Chart.controllers.line.prototype.draw;
    Chart.helpers.extend(Chart.controllers.line.prototype, {
        draw: function () {
            originalLineDraw.apply(this, arguments);

            const chart = this.chart;
            const ctx = chart.chart.ctx;

            chart.config.data.lineAtIndexes.forEach((elem, index) => {
                const xScale = chart.scales['x-axis-0'];
                const yScale = chart.scales['y-axis-0'];

                ctx.save();
                ctx.beginPath();
                ctx.moveTo(xScale.getPixelForValue(undefined, elem), yScale.getPixelForValue(playerHistory[elem].clientCount) / (initialAnimationComplete ? 1 : animationProgress));
                ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
                ctx.lineTo(xScale.getPixelForValue(undefined, elem), yScale.bottom);
                ctx.stroke();
                ctx.restore();
            });
        }
    });*/

    const canvas = document.getElementById(`server_history_canvas_${i}`);
    canvas.setAttribute('width', width);

    return new Chart(document.getElementById(`server_history_canvas_${i}`), {
        type: 'line',
        data: {
            labels: playerHistory.map(history => history.timeString),
            datasets: [{
                data: onlineTime.map(history => history.clientCount),
                backgroundColor: fillColor,
                borderColor: primaryColor,
                borderWidth: 2,
                hoverBorderColor: 'white',
                hoverBorderWidth: 2
            },
                {
                    data: offlineTime.map(history => history.clientCount),
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
            defaultFontFamily: '-apple-system, BlinkMacSystemFont, "Open Sans", "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif, "Apple Color Emoji", "Segoe UI Emoji", "Segoe UI Symbol"',
            tooltips: {
                callbacks: {
                    // todo: localization at some point
                    title: context => moment(context[0].label).local().calendar(),
                    label: context => context.datasetIndex !== 1 ? `${context.value} players on ${playerHistory[context.index].mapAlias}` : context.value === '0' ? '' : 'Server Unreachable!',
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
                        max: 1,
                        min: maxClients + 2
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

function refreshClientActivity() {
    $('.server-history-row').each(function (index) {
        let serverId = $(this).data("serverid");

        $.get({
            url: "/server/clientactivity/" + serverId,
            cache: false
        })
            .done(function (response) {
                const clientCount = $(response).find('a').length;
                $('#server_header_' + serverId + ' .server-clientcount').text(clientCount);
                $('#server_clientactivity_' + serverId).html(response);
            })
            .fail(function (jqxhr, textStatus, error) {
                $('#server_clientactivity_' + serverId).html('');
            });
    });
}

$(document).ready(function () {
    $('.server-join-button').click(function (e) {
        $(this).children('.server-header-ip-address').show();
    });

    $('.server-history-row').each(function (index, element) {
        let clientHistory = $(this).data('clienthistory-ex');
        let serverId = $(this).data('serverid');
        let maxClients = parseInt($('#server_header_' + serverId + ' .server-maxclients').text());
        let width = $('.server-header').first().width();
        getPlayerHistoryChart(clientHistory, serverId, width, maxClients);
    });

    $('.moment-date').each((index, element) => {
        const title = $(element).attr('title');

        if (title !== undefined) {
            const date = new Date(title);
            $(element).attr('title', moment.utc(date).calendar());
        }
    });
});

setInterval(refreshClientActivity, 2000);
