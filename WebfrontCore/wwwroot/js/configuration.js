$(document).ready(function() {
    $.each($('.has-related-content'), function (key, value) {
        value = $(value);
        if (value.attr('checked') !== undefined && value.attr('checked').length > 0) {
            $(value.data('related-content')).slideDown();
        }
    });

    $('input:checkbox').change(function () {
        var isChecked = $(this).is(':checked');
        isChecked ? $($(this).data('related-content')).slideDown() : $($(this).data('related-content')).slideUp();
    });

    $('.configuration-add-new').click(function (e) {
        e.preventDefault();
 
        let parentElement = $(this).parent();

        $.get($(this).attr('href') + '&itemCount=' + $(this).siblings().length, function (response) {
            $(response).insertBefore(parentElement.children().last());
        });
    });
});