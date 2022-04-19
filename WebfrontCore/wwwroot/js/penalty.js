function updateFilters() {
    location = `${location.href.split('?')[0]}?showOnly=${$('#penalty_filter_selection').val()}&hideAutomatedPenalties=${document.getElementById('hide_automated_penalties_checkbox').checked}`;
}

$(document).ready(function () {
    const filterSelection = $('#penalty_filter_selection');
    
    if (filterSelection) {
        filterSelection.change(function () {
            updateFilters();
        });

        $('#hide_automated_penalties_checkbox').click(function () {
            updateFilters();
        });
    }
});
