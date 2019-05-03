let offset = 15;
let isLoading = false;

function loadMorePenalties() {
    if (isLoading) {
        return false;
    }

    showLoader();
    isLoading = true;
    $.get('/Penalty/ListAsync', { offset: offset, showOnly : $('#penalty_filter_selection').val() })
        .done(function (response) {
            $('#penalty_table').append(response);
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
    offset += 15;
}

if ($('#penalty_table').length === 1) {
    
    $('#penalty_filter_selection').change(function() {
            location = location.href.split('?')[0] + "?showOnly=" + $('#penalty_filter_selection').val();
    });
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

        $('#load_penalties_button').click(function () {
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