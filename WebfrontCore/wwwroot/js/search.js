$(document).ready(function () {
    $('.form-inline').submit(function (e) {
        const id = $(e.currentTarget).find('input');
        if ($(id).val().length < 3) {
            e.preventDefault();
            $(id)
                .addClass('input-text-danger')
                .delay(25)
                .queue(function () {
                    $(this).addClass('input-border-transition').dequeue();
                })
                .delay(1000)
                .queue(function () {
                    $(this).removeClass('input-text-danger').dequeue();
                })
                .delay(500)
                .queue(function () {
                    $(this).removeClass('input-border-transition').dequeue();
                });
        } else if ($(id).val().startsWith("chat|")) {
            e.preventDefault();
            window.location = "/Message/Find?query=" + $(id).val();
        }
    });

    $('.date-picker-input').each((index, selector) => {
        new Datepicker(selector, {
            buttonClass: 'btn',
            format: 'yyyy-mm-dd',
            nextArrow: '>',
            prevArrow: '<',
            orientation: 'auto top'
        });
    });


    const clientSearchWrapper = $('*[id^="clientSearchWrapper"]');
    const chatSearchWrapper = $('*[id^="chatSearchWrapper"]');
    const searchTypeSelector = $('#searchTypeSelectorParent select');
    let isClients = false;

    searchTypeSelector.on('change', function () {
        if (isClients) {
            clientSearchWrapper.removeClass('d-none');
            chatSearchWrapper.addClass('d-none');
        } else {
            chatSearchWrapper.removeClass('d-none');
            clientSearchWrapper.addClass('d-none');
        }
        isClients = !isClients;
    });

    const isDefault = clientSearchWrapper.data('has-data') !== 'True' && chatSearchWrapper.data('has-data') !== 'True';

    if (isDefault) {
        isClients = false;
        searchTypeSelector.val('client').change();
    } else {
        if (clientSearchWrapper.data('has-data') === 'True') {
            isClients = false;
            searchTypeSelector.val('client').change();
        }
        if (chatSearchWrapper.data('has-data') === 'True') {
            isClients = true;
            searchTypeSelector.val('chat').change();
        }
    }
});
