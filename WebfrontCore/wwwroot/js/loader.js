let loaderOffset = 10;
let loadCount = 10;
let loaderReachedEnd = false;
let startAt = null;
let isLoaderLoading = false;
let loadUri = '';
let loaderResponseId = '';
let additionalParams = [];

function initLoader(location, loaderId, count = 10, start = count, additional = []) {
    loadUri = location;
    loaderResponseId = loaderId;
    loadCount = count;
    loaderOffset = start;
    additionalParams = additional;

    try {
        setupMonitor();
    }
    catch {
        // ignored (can happen when the action modal loader exists but no page level loader does)
    }

    $('#loaderLoad').click(function () {
        loadMoreItems();
    });
    
    $('.loader-load-more').click(function() {
        loadMoreItems();
    })
}

function setupMonitor() {
    const element = document.querySelector('#loaderLoad')
    const observer = new window.IntersectionObserver(([entry]) => {
        if (entry.isIntersecting && $('.content-wrapper').scrollTop() > 10) {
            loadMoreItems();
        }
    }, {
        root: null,
        threshold: 1,
    })

    observer.observe(element);
}

function loadMoreItems() {
    if (isLoaderLoading || loaderReachedEnd) {
        return false;
    }

    showLoader();
    isLoaderLoading = true;
    let params = {offset: loaderOffset, count: loadCount, startAt: startAt};
    for (let i = 0; i < additionalParams.length; i++) {
        let param = additionalParams[i];
        params[param.name] = param.value instanceof Function ? param.value() : param.value;
    }

    $.get(loadUri, params)
        .done(function (response) {
            $(loaderResponseId).append(response);
            if (response.trim().length === 0) {
                staleLoader();
                loaderReachedEnd = true;
                $('.loader-load-more').remove('text-primary').addClass('text-muted');
            }
            $(document).trigger("loaderFinished", response);
            startAt = $(response).filter('.loader-data-time').last().data('time');
            hideLoader();
            isLoaderLoading = false;
        })
        .fail(function () {
            errorLoader();
            halfmoon.initStickyAlert({
                content: _localization['WEBFRONT_SCRIPT_LOADER_ERROR'],
                title: 'Error',
                alertType: 'alert-danger',
                fillType: 'filled'
            });
            isLoaderLoading = false;
        });
    loaderOffset += loadCount;
}
