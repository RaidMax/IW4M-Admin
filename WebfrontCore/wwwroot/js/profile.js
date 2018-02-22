$(document).ready(function () {
	/*
	Expand alias tab if they have any
	*/
    $('#profile_aliases_btn').click(function (e) {
        const aliases = $('#profile_aliases').text().trim();
        if (aliases && aliases.length !== 0) {
            $('#profile_aliases').slideToggle(150);
        }
    });

    $.each(clientInfo.Meta, function (index, meta) {
        loadMeta(meta);
        if (count % 40 === 0) {
            count++;
            return false;
        }
        count++
    });
});

function penaltyToName(penaltyName) {
    switch (penaltyName) {
        case "Flag":
            return "Flagged"
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

function loadMeta(meta) {
    let eventString = '';
    // it's a penalty
    if (meta.class.includes("Penalty")) {
        if (meta.value.punisherId !== clientInfo.clientId) {
            eventString = `<div><span class="penalties-color-${meta.value.type.toLowerCase()}">${penaltyToName(meta.value.type)}</span> by <span class="text-highlight"> <a class="link-inverse"  href="${meta.value.punisherId}">${meta.value.punisherName}</a></span > for <span style="color: white; ">${meta.value.offense}</span> ${meta.whenString} ago </div>`;
        }
        else {
            eventString = `<div><span class="penalties-color-${meta.value.type.toLowerCase()}">${penaltyToName(meta.value.type)} </span> <span class="text-highlight"><a class="link-inverse" href="${meta.value.offenderId}"> ${meta.value.offenderName}</a></span > for <span style="color: white; ">${meta.value.offense}</span> ${meta.whenString} ago </div>`;
        }
    }
    // it's a message
    else if (meta.key.includes("Event")) {
        eventString = `<div><span style="color:white;">></span><span class="text-muted"> ${meta.value}</span></div>`;
    }
    $('#profile_events').append(eventString);
}

let count = 1;

$(document).ready(function () {
    $(window).scroll(function () {
        if ($(window).scrollTop() === $(document).height() - $(window).height() || $(document).height() === $(window).height()) {
            while (count % 40 !== 0 && count < clientInfo.Meta.length) {
                loadMeta(clientInfo.Meta[count - 1]);
                count++;
            }
            count++;
        }
    });

    $.each(clientInfo.Meta, function (index, meta) {
        if (!meta.key.includes("Event")) {
            let metaString = `<div class="profile-meta-entry"><span class="profile-meta-value text-primary">${meta.value}</span><span class="profile-meta-title text-muted"> ${meta.key}</span></div>`;
            $("#profile_meta").append(metaString);
        }
    });

    $.each(clientInfo.Meta, function (index, meta) {
        loadMeta(meta);

        if (count % 40 === 0) {
            count++;
            return false;
        }
        count++;
    });
});