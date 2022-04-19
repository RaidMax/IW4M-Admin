function refreshScoreboard() {
    const serverPanel = $('.scoreboard-container');
    const serverId = $(serverPanel).data('server-id');

    const scoreboardTable = $(serverPanel).children('.table-sort');

    $.get(`/Server/${serverId}/Scoreboard?order=${scoreboardTable.data('sort-column')}&down=${scoreboardTable.data('sort-down')}`, (response) => {
        $(serverPanel).html(response);
        setupDataSorting();
    });
}

$(document).ready(() => {
    if ($('.scoreboard-container').length === 0) {
        return 0;
    }
    
    setInterval(refreshScoreboard, 5000);
    setupDataSorting();
})

function setupDataSorting() {
    const tableColumn = $('.table-sort-column');
    $(tableColumn).off('click');
    $(tableColumn).on('click', function() {
        const columnName = $(this).data('column-name');
        const table = $('.table-sort');
        $(table).data('sort-column', columnName);
        $(table).data('sort-down', $(table).data('sort-down') !== true);
        refreshScoreboard();
    })
}
