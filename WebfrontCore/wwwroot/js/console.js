function executeCommand() {
    const serverId = $('#console_server_select').val();
    const command = $('#console_command_value').val();
    $.get('/Console/ExecuteAsync', { serverId: serverId, command: command })
        .done(function (response) {
            $('#console_command_response').html(response);
            $('#console_command_value').val("");
        })
        .fail(function (jqxhr, textStatus, error) {
            $('#console_command_response').text('Could not execute command: ' + error).addClass('text-danger');
        });
}

$(document).ready(function () {
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