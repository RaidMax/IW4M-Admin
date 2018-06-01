function getStatsChart(id, width, height) {
    const data = $('#' + id).data('history');
    let fixedData = [];
    data.forEach(function (item, i) {
        fixedData[i] = { x: i, y: item };
    });

    return new CanvasJS.Chart(id, {
        backgroundColor: 'transparent',
        height: height,
        width: width,
        animationEnabled: false,
        toolTip: {
            contentFormatter: function (e) {
                return e.entries[0].dataPoint.y;
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
            minimum: Math.min(...data) - Math.min(...data) * 0.075,
            maximum: Math.max(...data) + Math.max(...data) * 0.075,
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
            color: 'rgba(0, 122, 204, 0.25)',
            markerSize: 0,
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
});

$(document).on("loaderFinished", function (event, response) {
    const ids = $.map($(response).find('.client-rating-graph'), function (elem) { return $(elem).attr('id'); });
    ids.forEach(function (item, index) {
        getStatsChart(item, $(item).width(), $(item).height()).render();
    });
});
