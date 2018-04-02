// keeps track of how many events have been displayed
let count = 1;

$(document).ready(function () {
	/*
	Expand alias tab if they have any
	*/
    $('#profile_aliases_btn').click(function (e) {
        const aliases = $('#profile_aliases').text().trim();
        if (aliases && aliases.length !== 0) {
            $('#profile_aliases').slideToggle(150);
            $(this).toggleClass('oi-caret-top');
        }
    });

    /* 
    load the initial 40 events
    */
    $.each(clientInfo.Meta, function (index, meta) {
        if (meta.key.includes("Event")) {
            loadMeta(meta);
            if (count % 40 === 0) {
                count++;
                return false;
            }
            count++;
        }
    });

    /*
    load additional events on scroll
    */
    $(window).scroll(function () {
        if ($(window).scrollTop() === $(document).height() - $(window).height() || $(document).height() === $(window).height()) {
            while (count % 40 !== 0 && count < clientInfo.Meta.length) {
                loadMeta(clientInfo.Meta[count - 1]);
                count++;
            }
            count++;
        }
    });

    /*
    load meta thats not an event
    */
    $.each(clientInfo.Meta, function (index, meta) {
        if (!meta.key.includes("Event")) {
            let metaString = `<div class="profile-meta-entry"><span class="profile-meta-value text-primary">${meta.value}</span><span class="profile-meta-title text-muted"> ${meta.key}</span></div>`;
            $("#profile_meta").append(metaString);
        }
    });

    /*
     get ip geolocation info into modal
     */
    $('.ip-locate-link').click(function (e) {
        e.preventDefault();
        const ip = $(this).data("ip");
        $.getJSON("http://ip-api.com/json/" + ip)
            .done(function (response) {
                $('#mainModal .modal-title').text(ip);
                $('#mainModal .modal-body').text("");
                $('#mainModal .modal-body').append("ASN &mdash; " + response["as"] + "<br/>");
                $('#mainModal .modal-body').append("ISP &mdash; " + response["isp"] + "<br/>");
                $('#mainModal .modal-body').append("Organization &mdash; " + response["org"] + "<br/>");
                $('#mainModal .modal-body').append("Location &mdash; " + response["city"] + ", " + response["regionName"] + ", " + response["country"] + "<br/>");
                $('#mainModal').modal();
            })
            .fail(function (jqxhr, textStatus, error) {
                $('#mainModal .modal-title').text("Error");
                $('#mainModal .modal-body').html('<span class="text-danger">&mdash;'+ error + '</span>');
                $('#mainModal').modal();
            });
    });

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
                $('#actionModal .modal-body').html(response);
                $('#actionModal').modal();
            })
            .fail(function (jqxhr, textStatus, error) {
                $('#actionModal .modal-body').html('<span class="text-danger">Error' + error + '</span>');
            });
    });
});

function penaltyToName(penaltyName) {
    switch (penaltyName) {
        case "Flag":
            return "Flagged";
        case "Warning":
            return "Warned";
        case "Report":
            return "Reported";
        case "Ban":
            return "Banned";
        case "Kick":
            return "Kicked";
        case "TempBan":
            return "Temp Banned";
        case "Unban":
            return "Unbanned";
    }
}

function shouldIncludePlural(num) {
    return num > 1 ? 's' : '';
}

let mostRecentDate = 0;
let currentStepAmount = 0;
let lastStep = '';
function timeStep(stepDifference) {
    let hours = stepDifference / (1000 * 60 * 60);
    let days = stepDifference / (1000 * 60 * 60 * 24);
    let weeks = stepDifference / (1000 * 60 * 60 * 24 * 7);

    if (Math.round(weeks) > Math.round(currentStepAmount / 24 * 7)) {
        currentStepAmount = Math.round(weeks);
        return `${currentStepAmount} week${shouldIncludePlural(currentStepAmount)} ago`;
    }

    if (Math.round(days) > Math.round(currentStepAmount / 24)) {
        currentStepAmount = Math.round(days);
        return `${currentStepAmount} day${shouldIncludePlural(currentStepAmount)} ago`;
    }

    if (Math.round(hours) > currentStepAmount) {
        currentStepAmount = Math.round(hours);
        return `${currentStepAmount} hour${shouldIncludePlural(currentStepAmount)} ago`;
    }
}

function loadMeta(meta) {
    let eventString = '';
    const metaDate = moment.utc(meta.when).valueOf();

    if (mostRecentDate === 0) {
        mostRecentDate = metaDate;
    }

    const step = timeStep(moment.utc().valueOf() - metaDate);

    if (step !== lastStep && step !== undefined && metaDate > 0) {
        $('#profile_events').append('<span class="p2 text-white profile-event-timestep"><span class="text-primary">&mdash;</span> ' + step + '</span>');
        lastStep = step;
    }

    // it's a penalty
    if (meta.class.includes("Penalty")) {
        if (meta.value.punisherId !== clientInfo.clientId) {
            eventString = `<div><span class="penalties-color-${meta.value.type.toLowerCase()}">${penaltyToName(meta.value.type)}</span> by <span class="text-highlight"> <a class="link-inverse"  href="${meta.value.punisherId}">${meta.value.punisherName}</a></span > for <span style="color: white; ">${meta.value.offense}</span></div>`;
        }
        else {
            eventString = `<div><span class="penalties-color-${meta.value.type.toLowerCase()}">${penaltyToName(meta.value.type)} </span> <span class="text-highlight"><a class="link-inverse" href="${meta.value.offenderId}"> ${meta.value.offenderName}</a></span > for <span style="color: white; ">${meta.value.offense}</span></div>`;
        }
    }
    else if (meta.key.includes("Alias")) {
        eventString = `<div><span class="text-primary">${meta.value}</span></div>`;
    }
    // it's a message
    else if (meta.key.includes("Event")) {
        eventString = `<div><span style="color:white;">></span><span class="text-muted"> ${meta.value}</span></div>`;
    }
    $('#profile_events').append(eventString);
}
