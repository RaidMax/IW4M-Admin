function getStatsChart(id, width, height) {
    const data = $('#' + id).data('history');
    let fixedData = [];
    data.forEach(function (item, i) {
        fixedData[i] = { x: i, y: Math.floor(item) };
    });

    let dataMin = Math.min(...data);
    const dataMax = Math.max(...data);

    if (dataMax - dataMin === 0) {
        dataMin = 0;
    }

    const padding = (dataMax - dataMin) * 0.075;
    const min = Math.max(0, dataMin - padding);
    const max = dataMax + padding;
    let interval = Math.floor((max - min) / 2);

    if (interval < 1)
        interval = 1;

    let primaryColor = document.documentElement.style.getPropertyValue('--primary');

    return new CanvasJS.Chart(id, {
        backgroundColor: 'transparent',
        height: height,
        width: width,
        animationEnabled: false,
        toolTip: {
            contentFormatter: function (e) {
                return Math.round(e.entries[0].dataPoint.y, 1);
            }
        },
        title: {
            text: _localization['WEBFRONT_STATS_PERFORMANCE_HISTORY'],
            fontSize: 14
        },
        axisX: {
            gridThickness: 0,
            lineThickness: 0,
            tickThickness: 0,
            margin: 0,
            valueFormatString: ' '
        },
        axisY: {
            labelFontSize: 12,
            interval: interval,
            gridThickness: 0,
            lineThickness: 0.5,
            valueFormatString: '#,##0',
            minimum: min,
            maximum: max
        },
        legend: {
            dockInsidePlotArea: true
        },
        data: [{
            type: 'splineArea',
            color: primaryColor.endsWith('80') ? primaryColor : primaryColor + '40',
            markerSize: 3.5,
            dataPoints: fixedData
        }]
    });
}

$(document).ready(function () {
    $('.client-rating-graph').each(function (i, element) {
        getStatsChart($(element).attr('id'), $(element).width(), $(element).height()).render();
    });

    $(window).resize(function () {
        $('.client-rating-graph').each(function (index, element) {
            getStatsChart($(element).attr('id'), $(element).width(), $(element).height()).render();
        });
    });

    $('.top-players-link').click(function (event) {
        $($(this).attr('href')).html('');
        initLoader('/Stats/GetTopPlayersAsync?serverId=' + $(this).data('serverid'), $(this).attr('href'), 10, 0);
        loadMoreItems();
    });
});

$(document).on("loaderFinished", function (event, response) {
    const ids = $.map($(response).find('.client-rating-graph'), function (elem) { return $(elem).attr('id'); });
    ids.forEach(function (item, index) {
        getStatsChart(item, $(item).width(), $(item).height()).render();
    });
});
