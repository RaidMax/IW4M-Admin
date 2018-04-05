$(document).ready(function () {
    /*
     * handle action modal
     */
    $('.profile-action').click(function (e) {
        const actionType = $(this).data('action');
        $.get('/Action/' + actionType + 'Form')
            .done(function (response) {
                $('#actionModal .modal-message').fadeOut('fast');
                $('#actionModal .modal-body-content').html(response);
                $('#actionModal').modal();
            })
            .fail(function (jqxhr, textStatus, error) {
                $('#actionModal .modal-message').text('Error &mdash ' + error);
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
        const data = $(this).serialize();
        $.get($(this).attr('action') + '/?' + data)
            .done(function (response) {
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
                if ($('#actionModal .modal-message').text.length > 0) {
                    $('#actionModal .modal-message').fadeOut('fast');
                }
                if (jqxhr.status === 401) {
                    $('#actionModal .modal-message').text('Invalid login credentials');
                }
                else {
                    $('#actionModal .modal-message').text('Error &mdash; ' + error);
                }
                $('#actionModal .modal-message').fadeIn('fast');
            });
    });
});