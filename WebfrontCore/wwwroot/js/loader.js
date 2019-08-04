let loaderOffset = 10;
let loadCount = 10;
let loaderReachedEnd = false;
let startAt = null;
let isLoaderLoading = false;
let loadUri = '';
let loaderResponseId = '';

function initLoader(location, loaderId, count = 10, start = count) {
    loadUri = location;
    loaderResponseId = loaderId;
    loadCount = count;
    loaderOffset = start;
    setupListeners();
}

function loadMoreItems() {
    if (isLoaderLoading || loaderReachedEnd) {
        return false;
    }

    showLoader();
    isLoaderLoading = true;
    $.get(loadUri, { offset: loaderOffset, count: loadCount, startAt: startAt })
        .done(function (response) {
            $(loaderResponseId).append(response);
            if (response.trim().length === 0) {
                staleLoader();
                loaderReachedEnd = true;
                $('.loader-load-more').addClass('disabled');
            }
            $(document).trigger("loaderFinished", response);
            startAt = $(response).filter('.loader-data-time').last().data('time');
            hideLoader();
            isLoaderLoading = false;
        })
        .fail(function (jqxhr, statis, error) {
            errorLoader();
            isLoaderLoading = false;
        });
    loaderOffset += loadCount;
}

var hasScrollBar = false;

function _ScrollHandler(e) {
    //throttle event:
    /*
    https://stackoverflow.com/questions/3898130/check-if-a-user-has-scrolled-to-the-bottom
    */
    var $window = $(window);
    var $document = $(document);
    hasScrollBar = true;
    let _throttleTimer = null;
    let _throttleDelay = 100;

    clearTimeout(_throttleTimer);
    _throttleTimer = setTimeout(function () {

        //do work
        if ($window.scrollTop() + $window.height() > $document.height() - 100) {
            loadMoreItems();
        }

    }, _throttleDelay);
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



        $(document).ready(function () {
            $(window)
                .off('scroll', _ScrollHandler)
                .on('scroll', _ScrollHandler);
            $('.loader-load-more:not(.disabled)').click(function (e) {
                if (!isLoaderLoading) {
                    loadMoreItems();
                }
            });
        });
    }
}