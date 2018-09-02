function executeCommand() {
    const serverId = $('#console_server_select').val();
    const command = $('#console_command_value').val();

    if (command.length === 0) {
        return false;
    }

    if (command[0] !== '!') {
        $('#console_command_response').text('All commands must start with !').addClass('text-danger');
        return false;
    }
    showLoader();
    $.get('/Console/ExecuteAsync', { serverId: serverId, command: command })
        .done(function (response) {
            hideLoader();
            $('#console_command_response').append(response);
            $('#console_command_value').val("");
        })
        .fail(function (jqxhr, textStatus, error) {
            errorLoader();
            hideLoader();
            $('#console_command_response').text('Could not execute command: ' + error).addClass('text-danger');
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
        const keyCode = (event.keyCode ? event.keyCode : event.which);
        if (keyCode === 13) {
            executeCommand();
        }
    });
});