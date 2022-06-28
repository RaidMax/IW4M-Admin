function hideLoader() {
    $('#mainLoadingBar').fadeOut();
    $('.modal-loading-bar').fadeOut();
}

function showLoader() {
    $('#mainLoadingBar').fadeIn();
    $('.modal-loading-bar').fadeIn();
}

function errorLoader() {
    $('#mainLoadingBar').addClass('bg-danger').delay(2000).fadeOut();
}

function staleLoader() {
    $('#mainLoadingBar').addClass('bg-grey');
}

function getUrlParameter(sParam) {
    let sPageURL = window.location.search.substring(1),
        sURLVariables = sPageURL.split('&'),
        sParameterName,
        i;

    for (i = 0; i < sURLVariables.length; i++) {
        sParameterName = sURLVariables[i].split('=');

        if (sParameterName[0] === sParam) {
            return sParameterName[1] === undefined ? true : decodeURIComponent(sParameterName[1]);
        }
    }
    return false;
}

function clearQueryString() {
    const uri = window.location.href.toString();
    if (uri.indexOf("?") > 0) {
        const cleanUri = uri.substring(0, uri.indexOf("?"));
        window.history.replaceState({}, document.title, cleanUri);
    }
}

const entityMap = {
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#39;',
    '/': '&#x2F;',
    '`': '&#x60;',
    '=': '&#x3D;'
};

function escapeHtml (string) {
    return String(string).replace(/[&<>"'`=\/]/g, function (s) {
        return entityMap[s];
    });
}

function buildToastUri(message, duration) {
    let uri = '&';
    if (window.location.href.toString().indexOf('?') <= 0) {
        uri = '?';
    }
    uri += `toastMessage=${escape(message)}${duration ? `&duration=${duration}` : ''}`;
    return uri;
}

$(document).ready(function () {
    
    let toastMessage = getUrlParameter('toastMessage');
    const duration = parseInt(getUrlParameter('duration'));
     
    if (toastMessage) {
        toastMessage = unescape(toastMessage);
    }
    
    if (toastMessage) {
        clearQueryString();
        halfmoon.initStickyAlert({
            content: toastMessage,
            title: 'Success',
            alertType: 'alert-success',
            fillType: 'filled',
            timeShown: duration
        });
    }
    
    hideLoader();

    $(document).off('click', '.profile-action');
    $(document).on('click', '.profile-action', function (e) {
        e.preventDefault();
        const actionType = $(this).data('action');
        const actionId = $(this).data('action-id');
        const responseDuration = $(this).data('response-duration') || 5000;
        const actionIdKey = actionId === undefined ? '' : `?id=${actionId}`;
        showLoader();
        $.get(`/Action/${actionType}Form/${actionIdKey}`)
            .done(function (response) {
                $('#actionModal .modal-message').fadeOut('fast')
                $('#actionModal').attr('data-response-duration', responseDuration);
                $('#actionModalContent').html(response);
                hideLoader();
                halfmoon.toggleModal('actionModal');
            })
            .fail(function (jqxhr, textStatus, error) {
                halfmoon.initStickyAlert({
                    content: jqxhr.responseText,
                    title: 'Error',
                    alertType: 'alert-danger',
                    fillType: 'filled'
                });
            });
    });

    /*
     * handle action submit
     */
    $(document).on('submit', '.action-form', function (e) {
        e.preventDefault();
        $(this).append($('#target_id input'));
        const modal = $('#actionModal');
        const shouldRefresh = modal.data('should-refresh', modal.find('.refreshable').length !== 0);
        const data = $(this).serialize();
        const duration = modal.data('response-duration');
        showLoader();

        $.get($(this).attr('action') + '/?' + data)
            .done(function (response) {
                hideLoader();
                // success without content
                if (response.length === 0) {
                    location.reload();
                } else {
                    let message = response; 
                    try {
                        message = response.map(r => escapeHtml(r.response));
                    }
                    catch{}
                    if (shouldRefresh) {
                        window.location = `${window.location.href.replace('#', '')}${buildToastUri(message, duration)}`;
                    }
                    else {
                        modal.modal();
                        halfmoon.initStickyAlert({
                            content: escapeHtml(message),
                            title: 'Executed',
                            alertType: 'alert-primary',
                            fillType: 'filled'
                        });
                    }
                }
            })
            .fail(function (jqxhr) {
                hideLoader();

                let message = jqxhr.responseText;
                
                try {
                    const jsonMessage = $.parseJSON(message);

                    if (jsonMessage) {
                        message = jsonMessage.map(r => escapeHtml(r.response));
                    }
                }
                
                catch{}
                
                if (message instanceof Array)
                {
                    message = message.join("<br/>");
                }
                
                halfmoon.initStickyAlert({
                    content: message,
                    title: 'Error',
                    alertType: 'alert-danger',
                    fillType: 'filled'
                });
            });
    });
});
