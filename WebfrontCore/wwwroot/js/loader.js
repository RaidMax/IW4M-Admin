let offset = 15;
let loadCount = 15;
let isLoading = false;
let loadUri = '';
let loaderResponseId = '';

function initLoader(location, loaderId) {
    loadUri = location;
    loaderResponseId = loaderId;
    setupListeners();
}

function loadMoreItems() {
    if (isLoading) {
        return false;
    }

    showLoader();
    isLoading = true;
    $.get(loadUri, { offset: offset, count : loadCount })
        .done(function (response) {
            $(loaderResponseId).append(response);
            if (response.trim().length === 0) {
                staleLoader();
            }
            hideLoader();
            isLoading = false;
        })
        .fail(function (jqxhr, statis, error) {
            errorLoader();
            isLoading = false;
        });
    offset += loadCount;
}

function setupListeners() {
if ($(loaderResponseId).length === 1) {
/*
    https://stackoverflow.com/questions/19731730/jquery-js-detect-users-scroll-attempt-without-any-window-overflow-to-scroll
    */

    $('html').bind('mousewheel DOMMouseScroll', function (e) {
        var delta = (e.originalEvent.wheelDelta || -e.originalEvent.detail);

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

        /*$('#load_penalties_button').click(function () {
            loadMorePenalties();
        });*/
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