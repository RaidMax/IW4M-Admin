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
    const primaryColor = $('.text-primary').css('color');
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
                    label: context => context.datasetIndex !== 1 ? `${context.value} ${_localization['WEBFRONT_SCRIPT_SERVER_PLAYERS']} | ${playerHistory[context.index].mapAlias}` : context.value === '0' ? '' : _localization['WEBFRONT_SCRIPT_SERVER_UNREACHABLE'],
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

function refreshClientActivity(serverId) {
    $.get({
        url: `/server/clientactivity/${serverId}`,
        cache: false
    })
        .done(function (response) {
            const clientCount = $(response).find('a.no-decoration').length;
            $('#server_header_' + serverId + ' .server-clientcount').text(clientCount);
            $('#server_clientactivity_' + serverId).html(response);
        })
        .fail(function (jqxhr, textStatus, error) {
            $('#server_clientactivity_' + serverId).html('');
        });
}

$(document).ready(function () {
    $('.server-join-button').click(function (e) {
        $(this).parent().parent().find('.server-header-ip-address').show();
    });

    $('.server-history-row').each(function (index, element) {
        let clientHistory = $(this).data('clienthistory-ex');
        const serverId = $(this).data('serverid');
        setInterval(() => refreshClientActivity(serverId), 2000 + (index * 100));
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
