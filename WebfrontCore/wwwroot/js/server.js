function getPlayerHistoryChart(playerHistory, i, width, color, maxClients) {
    ///////////////////////////////////////
    // thanks to canvasjs :(
    playerHistory.forEach(function (item, i) {
        playerHistory[i].x = new Date(playerHistory[i].x);
    });

    return new CanvasJS.Chart(`server_history_${i}`, {
        backgroundColor: '#191919',
        height: 100,
        width: width,
        animationEnabled: true,
        toolTip: {
            contentFormatter: function (e) {
                const date = moment.utc(e.entries[0].dataPoint.x);
                return date.local().format('h:mm A') + " - " + e.entries[0].dataPoint.y + " players";
            }
        },
        axisX: {
            interval: 1,
            gridThickness: 0,
            lineThickness: 0,
            tickThickness: 0,
            margin: 0,
            valueFormatString: " "
        },
        axisY: {
            gridThickness: 0,
            lineThickness: 0,
            tickThickness: 0,
            minimum: 0,
            maximum: maxClients + 1,
            margin: 0,
            valueFormatString: " ",
            labelMaxWidth: 0
        },
        legend: {
            maxWidth: 0,
            maxHeight: 0,
            dockInsidePlotArea: true
        },
        data: [{
            showInLegend: false,
            type: "splineArea",
            color: color,
            markerSize: 0,
            dataPoints: playerHistory
        }]
    });
    //////////////////////////////////////
}
var charts = {};

$(window).resize(function () {
    $('.server-history-row').each(function (index) {
        let serverId = $(this).data('serverid');
        charts[serverId].options.width = $('.server-header').first().width();
        charts[serverId].render();
    });
});

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
        let clientHistory = $(this).data('clienthistory');
        let serverId = $(this).data('serverid');
        let maxClients = parseInt($('#server_header_' + serverId + ' .server-maxclients').text());
        let primaryColor = $('title').css('background-color');
        let color = $(this).data('online') === 'True' ? primaryColor : '#ff6060';
        let width = $('.server-header').first().width();
        let historyChart = getPlayerHistoryChart(clientHistory, serverId, width, color, maxClients);
        historyChart.render();
        charts[serverId] = historyChart;
    });
});

setInterval(refreshClientActivity, 2000);
