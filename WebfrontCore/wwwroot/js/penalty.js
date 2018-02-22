let offset = 15;

function loadMorePenalties() {
    $.get('/Penalty/ListAsync', { offset: offset })
        .done(function (response) {
            $('#penalty_table').append(response);
        });
    offset += 15;
}

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