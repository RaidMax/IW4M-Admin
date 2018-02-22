function getPlayerHistoryChart(playerHistory, i, width) {
    ///////////////////////////////////////
    // thanks to canvasjs :(
    playerHistory.forEach(function (item, i) {
        playerHistory[i].x = new Date(playerHistory[i].x);
    });

    return new CanvasJS.Chart(`server_history_${i}`, {
        backgroundColor: "#191919",
        height: 100,
        width: width,
        animationEnabled: true,
        toolTip: {
            contentFormatter: function (e) {
                var date = new Date(e.entries[0].dataPoint.x);
                return date.toLocaleTimeString('en-US', { timeZone: 'America/New_York', hour12: true }) + " - " + e.entries[0].dataPoint.y + " players";
            }
        },
        axisX: {
            interval: 1,
            gridThickness: 0,
            lineThickness: 0,
            tickThickness: 0,
            margin: 0,
            valueFormatString: " ",
        },
        axisY: {
            gridThickness: 0,
            lineThickness: 0,
            tickThickness: 0,
            minimum: 0,
            margin: 0,
            valueFormatString: " ",
            labelMaxWidth: 0,
        },
        legend: {
            maxWidth: 0,
            maxHeight: 0,
            dockInsidePlotArea: true,
        },
        data: [{
            showInLegend: false,
            type: "splineArea",
            color: "rgba(0, 122, 204, 0.432)",
            markerSize: 0,
            dataPoints: playerHistory,
        }]
    });
    //////////////////////////////////////
}
var charts = {};

$('.server-history-row').each(function (index, element) {
    let clientHistory = $(this).data('clienthistory');
    let serverId = $(this).data('serverid');
    let width = $('.server-header').first().width();
    let historyChart = getPlayerHistoryChart(clientHistory, serverId, width);
    historyChart.render();
    charts[serverId] = historyChart;
});

$(window).resize(function () {
    $('.server-history-row').each(function (index) {
        let serverId = $(this).data('serverid');
        charts[serverId].options.width = $('.server-header').first().width();
        charts[serverId].render()
    });
})

function refreshClientActivity() {
    $('.server-history-row').each(function (index) {
        let serverId = $(this).data("serverid");

        $.get("/server/clientactivity/" + serverId)
            .done(function (response) {
                $('#server_clientactivity_' + serverId).html(response);
            })
            .fail(function (jqxhr, textStatus, error) {
                $('#server_clientactivity_' + serverId).html("Could not load client activity -  " + error);
            });
    });
}

setInterval(refreshClientActivity, 2000);