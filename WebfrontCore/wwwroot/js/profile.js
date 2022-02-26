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

    const ipAddresses = $('.ip-lookup-profile');
    $.each(ipAddresses, function (index, address) {
        let ip = $(address).data('ip');
        if (ip.length === 0) {
            return;
        }
        $.get('https://ip2c.org/' + ip, function (result) {
            const countryCode = result.split(';')[1].toLowerCase();
            const country = result.split(';')[3];

            if (country === 'Unknown') {
                return;
            }
            
            $('#ip_lookup_country').text(country);
            if (countryCode !== 'zz' && countryCode !== '') {
                $(address).css('background-image', `url('https://flagcdn.com/w80/${countryCode}.png')`);
            }
        });
    });

    /* set the end time for initial event query */
    startAt = $('.loader-data-time').last().data('time');

    $('#filter_meta_container_button').click(function () {
        $('#filter_meta_container').removeClass('d-none').addClass('flex-md-column');
        $('#additional_meta_filter').removeClass('d-md-none');
    });

    /*
     * load context of chat 
     */
    $(document).off('click', '.client-message');
    $(document).on('click', '.client-message', function (e) {
        showLoader();
        const location = $(this);
        $('.client-message-prefix').removeClass('oi-chevron-bottom');
        $('.client-message-prefix').removeClass('oi-chevron-right');

        $('.client-message-prefix').addClass('oi-chevron-right');

        $(this).children().filter('.client-message-prefix').removeClass('oi-chevron-right');
        $(this).children().filter('.client-message-prefix').addClass('oi-chevron-bottom');

        $.get('/Stats/GetMessageAsync', {
            'serverId': $(this).data('serverid'),
            'when': $(this).data('when')
        })
            .done(function (response) {
                $('.client-message-context').remove();
                location.after(response);
                hideLoader();
            })
            .fail(function (jqxhr, textStatus, error) {
                errorLoader();
            });
    });

    /*
    * load info on ban/flag
    */
    $(document).off('click', '.automated-penalty-info-detailed');
    $(document).on('click', '.automated-penalty-info-detailed', function (e) {
        showLoader();
        const location = $(this).parent();
        $.get('/Stats/GetAutomatedPenaltyInfoAsync', {
            'penaltyId': $(this).data('penalty-id')
        })
            .done(function (response) {
                $('.penalty-info-context').remove();
                location.after(response);
                hideLoader();
            })
            .fail(function (jqxhr, textStatus, error) {
                errorLoader();
            });
    });

    /*
     get ip geolocation info into modal
     */
    $('.ip-locate-link').click(function (e) {
        e.preventDefault();
        const ip = $(this).data("ip");
        $.getJSON(`https://ipwhois.app/json/${ip}`)
            .done(function (response) {
                $('#mainModal .modal-title').text(ip);
                $('#mainModal .modal-body').text('');
                if (response.isp.length > 0) {
                    $('#mainModal .modal-body').append(`${_localization['WEBFRONT_PROFILE_LOOKUP_ISP']} &mdash; ${response.isp}<br/>`);
                }
                if (response.org.length > 0) {
                    $('#mainModal .modal-body').append(`${_localization['WEBFRONT_PROFILE_LOOKUP_ORG']} &mdash; ${response.org}<br/>`);
                }
                if (response.region.length > 0 || response.city.length > 0 || response.country.length > 0 || response.timezone_gmt.length > 0) {
                    $('#mainModal .modal-body').append(`${_localization['WEBFRONT_PROFILE_LOOKUP_LOCATION']} &mdash; `);
                }
                if (response.city.length > 0) {
                    $('#mainModal .modal-body').append(response.city);
                }
                if (response.region.length > 0) {
                    $('#mainModal .modal-body').append((response.region.length > 0 ? ', ' : '') + response.region);
                }
                if (response.country.length > 0) {
                    $('#mainModal .modal-body').append((response.country.length > 0 ? ', ' : '') + response.country);
                }
                if (response.timezone_gmt.length > 0) {
                    $('#mainModal .modal-body').append((response.timezone_gmt.length > 0 ? ', ' : '') + response.timezone_gmt);
                }

                $('#mainModal').modal();
            })
            .fail(function (jqxhr, textStatus, error) {
                $('#mainModal .modal-title').text("Error");
                $('#mainModal .modal-body').html('<span class="text-danger">&mdash;' + error + '</span>');
                $('#mainModal').modal();
            });
    });
});
