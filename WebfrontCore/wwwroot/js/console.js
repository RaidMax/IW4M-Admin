function executeCommand() {
    const serverId = $('#console_server_select').val();
    const command = $('#console_command_value').val();

    if (command.length === 0) {
        return false;
    }

    showLoader();
    $.get('/Console/Execute', { serverId: serverId, command: command })
        .done(function (response) {
            $('#console_command_response pre').html('');
            
            hideLoader();
            response.map(r => r.response).forEach(item => {
                $('#console_command_response').append(`<div>${escapeHtml(item)}</div>`);
            })
            
            $('#console_command_response').append('<hr/>')
            $('#console_command_value').val("");
        })
        .fail(function (response) {
            $('#console_command_response pre').html('');
            errorLoader();
            hideLoader();
            
            if (response.status < 500) {
                response.responseJSON.map(r => r.response).forEach(item => {
                    $('#console_command_response').append(`<div class="text-danger">${escapeHtml(item)}</div>`);
                })
            } else {
                $('#console_command_response').append(`<div class="text-danger">Could not execute command...</div>`);
            }
        });
}

$(document).ready(function () {

    if ($('#console_command_button').length === 0) {
        return false;
    }

    $('#console_command_button').click(function (e) {
        executeCommand();
    });

    $(document).keydown(function (event) {
        const keyCode = event.keyCode ? event.keyCode : event.which;
        if (keyCode === 13) {
            executeCommand();
        }
    });
});
