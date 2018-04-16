let offset = 12;
let isLoading = false;

function loadMorePenalties() {
    if (isLoading) {
        return false;
    }

    showLoader();
    isLoading = true;
    $.get('/Penalty/ListAsync', { offset: offset })
        .done(function (response) {
            $('#penalty_table').append(response);
            hideLoader();
            isLoading = false;
        })
        .fail(function (jqxhr, statis, error) {
            errorLoader();
            isLoading = false;
        });
    offset += 12;
}

if ($('#penalty_table').length === 1) {
    /*
    https://stackoverflow.com/questions/19731730/jquery-js-detect-users-scroll-attempt-without-any-window-overflow-to-scroll
    */

    $('html').bind('mousewheel DOMMouseScroll', function (e) {
        var delta = (e.originalEvent.wheelDelta || -e.originalEvent.detail);

        if (delta < 0 && !hasScrollBar) {
            loadMorePenalties();
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

        $('#load_penalties_button').hover(function () {
            loadMorePenalties();
        });
    });

    function ScrollHandler(e) {
        //throttle event:
        hasScrollBar = true;
        clearTimeout(_throttleTimer);
        _throttleTimer = setTimeout(function () {

            //do work
            if ($window.scrollTop() + $window.height() > $document.height() - 100) {
                loadMorePenalties();
            }

        }, _throttleDelay);
    }
}