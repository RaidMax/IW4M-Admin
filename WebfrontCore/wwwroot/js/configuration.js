$(document).ready(function() {
    $.each($('.has-related-content'), function (key, value) {
        value = $(value);
        if (value.attr('checked').length > 0) {
            $(value.data('related-content')).slideDown();
        }
    });

    $('input:checkbox').change(function () {
        var isChecked = $(this).is(':checked');
        isChecked ? $($(this).data('related-content')).slideDown() : $($(this).data('related-content')).slideUp();
    });
});