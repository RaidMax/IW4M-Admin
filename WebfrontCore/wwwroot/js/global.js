$(document).ready(function () {
    /*
     * handle action modal
     */
    $('.profile-action').click(function (e) {
        const actionType = $(this).data('action');
        $.get('/Action/' + actionType + 'Form')
            .done(function (response) {
                $('#actionModal .modal-body').html(response);
                $('#actionModal').modal();
            })
            .fail(function (jqxhr, textStatus, error) {
                $('#actionModal .modal-body').html('<span class="text-danger">' + error + '</span>');
                $('#actionModal').modal();
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
                    $('#actionModal .modal-body').html(response);
                    $('#actionModal').modal();
                }
            })
            .fail(function (jqxhr, textStatus, error) {
                if (jqxhr.status == 401) {
                    $('#actionModal .modal-body').removeClass('text-danger');
                    $('#actionModal .modal-body').prepend('<div class="text-danger mb-3">Invalid login credentials</div>');
                }
                else {
                    $('#actionModal .modal-body').html('<span class="text-danger">Error &mdash; ' + error + '</span>');
                }
            });
    });
});