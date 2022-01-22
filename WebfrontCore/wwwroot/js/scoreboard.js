function refreshScoreboard() {
    const serverPanel = $('.scoreboard-container.active');
    const serverId = $(serverPanel).data('server-id');

    $.get(`../Server/${serverId}/Scoreboard`, (response) => {
        $(serverPanel).html(response);
    });
}

$(document).ready(() => {
    $(window.location.hash).tab('show');
    $(`${window.location.hash}_nav`).addClass('active');
})

setInterval(refreshScoreboard, 5000);
