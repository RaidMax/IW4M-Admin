let loaderOffset = 10;
let loadCount = 10;
let isLoaderLoading = false;
let loadUri = '';
let loaderResponseId = '';

function initLoader(location, loaderId, count = 10) {
    loadUri = location;
    loaderResponseId = loaderId;
    loadCount = count;
    loaderOffset = count;
    setupListeners();
}

function loadMoreItems() {
    if (isLoaderLoading) {
        return false;
    }

    showLoader();
    isLoaderLoading = true;
    $.get(loadUri, { offset: loaderOffset, count : loadCount })
        .done(function (response) {
            $(loaderResponseId).append(response);
            if (response.trim().length === 0) {
                staleLoader();
            }
            $(document).trigger("loaderFinished", response);
            hideLoader();
            isLoaderLoading = false;
        })
        .fail(function (jqxhr, statis, error) {
            errorLoader();
            isLoaderLoading = false;
        });
    loaderOffset += loadCount;
}

function setupListeners() {
if ($(loaderResponseId).length === 1) {
/*
    https://stackoverflow.com/questions/19731730/jquery-js-detect-users-scroll-attempt-without-any-window-overflow-to-scroll
    */

    $('html').bind('mousewheel DOMMouseScroll', function (e) {
        var delta = e.originalEvent.wheelDelta || -e.originalEvent.detail;

        if (delta < 0 && !hasScrollBar) {
            loadMoreItems();
        }
    });

    /*
    https://stackoverflow.com/questions/3898130/check-if-a-user-has-scrolled-to-the-bottom
    */

    var _throttleTimer = null;
    var _throttleDelay = 100;
    var $window = $(window);
    var $document = $(document);
    var hasScrollBar = false;

    $document.ready(function () {
        $window
            .off('scroll', ScrollHandler)
            .on('scroll', ScrollHandler);
    });

    function ScrollHandler(e) {
        //throttle event:
        hasScrollBar = true;
        clearTimeout(_throttleTimer);
        _throttleTimer = setTimeout(function () {

            //do work
            if ($window.scrollTop() + $window.height() > $document.height() - 100) {
                 loadMoreItems();
            }

        }, _throttleDelay);
    }
}
}