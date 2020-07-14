function hideLoader() {
    $('.layout-loading-icon').fadeOut();
}

function showLoader() {
    $('.layout-loading-icon').attr('style', 'visibility:visible');
    $('.layout-loading-icon').removeClass('text-danger');
    $('.layout-loading-icon').removeClass('text-muted');
    $('.layout-loading-icon').fadeIn();
}

function errorLoader() {
    $('.layout-loading-icon').addClass('text-danger');
}

function staleLoader() {
    $('.layout-loading-icon').addClass('text-muted');
}

$(document).ready(function () {
    hideLoader();

    /*
     * hide loader when clicking
     */
    $(document).click(function (e) {
        //hideLoader()
    });

    /*
     * handle action modal
     */
    $(document).off('click', '.profile-action');
    $(document).on('click', '.profile-action', function (e) {
        const actionType = $(this).data('action');
        const actionId = $(this).data('action-id');
        const actionIdKey = actionId === undefined ? '' : '?id=' + actionId;
        $.get('/Action/' + actionType + 'Form' + actionIdKey)
            .done(function (response) {
                $('#actionModal .modal-message').fadeOut('fast');
                $('#actionModal .modal-body-content').html(response);
                $('#actionModal').modal();
                $('#actionModal').trigger('action_form_received', actionType);
            })
            .fail(function (jqxhr, textStatus, error) {
                $('#actionModal .modal-body-content').html('');
                $('#actionModal .modal-message').text(_localization['GLOBAL_ERROR'] + ' — ' + jqxhr.responseText);
                $('#actionModal').modal();
                $('#actionModal .modal-message').fadeIn('fast');
            });
    });

    /*
     * handle action submit
     */
    $(document).on('submit', '.action-form', function (e) {
        e.preventDefault();
        $(this).append($('#target_id input'));
        $('#actionModal').data('should-refresh', $('#actionModal').find('.refreshable').length !== 0);
        const data = $(this).serialize();
        showLoader();
        $.get($(this).attr('action') + '/?' + data)
            .done(function (response) {
                hideLoader();
                // success without content
                if (response.length === 0) {
                    location.reload();
                }
                else {
                    $('#actionModal .modal-message').fadeOut('fast');
                    $('#actionModal .modal-body-content').html(response);
                    $('#actionModal').modal();
                }
            })
            .fail(function (jqxhr, textStatus, error) {
                errorLoader();
                hideLoader();
                if ($('#actionModal .modal-message').text.length > 0) {
                    $('#actionModal .modal-message').fadeOut('fast');
                }
                if (jqxhr.status === 401) {
                    $('#actionModal .modal-message').text(_localization['WEBFRONT_ACTION_CREDENTIALS']);
                }
                else {
                    $('#actionModal .modal-message').text(_localization['GLOBAL_ERROR'] + ' — ' + jqxhr.responseText);
                }
                $('#actionModal .modal-message').fadeIn('fast');
            });
    });

    /*
     * handle loading of recent clients
     */
    $('#actionModal').off('action_form_received');
    $('#actionModal').on('action_form_received', function (e, actionType) {
        if (actionType === 'RecentClients') {
            const ipAddresses = $('.client-location-flag');
            $.each(ipAddresses, function (index, address) {
                $.get('https://ip2c.org/' + $(address).data('ip'), function (result) {
                    const countryCode = result.split(';')[1].toLowerCase();
                    if (countryCode !== 'zz') {
                        $(address).css('background-image', `url(https://www.countryflags.io/${countryCode}/flat/64.png)`);
                    }
                });
            });
        }
    });

    /* 
     * handle close event to refresh if need be
     */
    $("#actionModal").on("hidden.bs.modal", function () {
        let shouldRefresh = $(this).data('should-refresh');

        if (shouldRefresh !== undefined && shouldRefresh) {
            location.reload();
        }
    });
});