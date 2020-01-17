$(document).ready(function() {
    $.each($('.has-related-content'), function(key, value) {
        value = $(value);
        if (value.attr('checked') !== undefined && value.attr('checked').length > 0) {
            $(value.data('related-content')).slideDown();
        }
    });

    $('input:checkbox').change(function() {
        var isChecked = $(this).is(':checked');
        isChecked ? $($(this).data('related-content')).slideDown() : $($(this).data('related-content')).slideUp();
    });

    // this is used for regular simple form adds
    $(document).on('click', '.configuration-add-new', function(e) {
        e.preventDefault();

        let parentElement = $(this).parent();
        let label = $(this).siblings('label');
        let forAttr = $(label).attr('for');
        let match = /Servers_+([0-9+])_+.*/g.exec(forAttr);
        let additionalData = '';
        if (match !== null && match.length === 2) {
            additionalData = '&serverIndex=' + match[1].toString();
        }

        $.get($(this).attr('href') + '&itemCount=' + $(this).siblings('input').length.toString() + additionalData, function (response) {
            $(response).insertBefore(parentElement.children().last());
        });
    });

    // this is used for server adds which are little more complex
    $(document).on('click', '.configuration-server-add-new', function (e) {
        e.preventDefault();

        let parentElement = $(this).parent();

        $.get($(this).attr('href') + '&itemCount=' + $('.server-configuration-header').length.toString(), function (response) {
            $(response).insertBefore(parentElement.children().last());
        });
    });

    // removes the server when clicking the delete button
    $(document).on('click', '.delete-server-button', function (e) {
        $(this).parents('.server-configuration-header').remove();
    });

    $('#configurationForm').submit(function (e) {
        $.ajax({
            data: $(this).serialize(),
            type: $(this).attr('method'),
            url: $(this).attr('action'),
            complete: function(response) {
                if (response.status === 200) {
                    $('#actionModal .modal-message').removeClass('text-danger');
                    $('#actionModal').data('should-refresh', true);
                }
                else {
                    $('#actionModal .modal-message').addClass('text-danger');
                }
                $('#actionModal .modal-body-content').html('');
                let errors = '';

                if (response.responseJSON.errors !== undefined) {
                    errors = response.responseJSON.errors[0].join('<br/>');
                }
                message = response.responseJSON.message;
                $('#actionModal .modal-message').html(message + '<br/>' + errors);
                $('#actionModal').modal();
                $('#actionModal .modal-message').fadeIn('fast');
            }
        });

        return false;
    });
});