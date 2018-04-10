$(document).ready(function() {
    $('.form-inline').submit(function(e) {
        if ($('#client_search').val().length < 3) {
            e.preventDefault();
            $('#client_search')
                .addClass('input-text-danger')
                .delay(25)
                .queue(function(){
                    $(this).addClass('input-border-transition').dequeue();
                })
                .delay(1000)
                .queue(function() {
                    $(this).removeClass('input-text-danger').dequeue();
                })
                .delay(500)
                .queue(function() {
                    $(this).removeClass('input-border-transition').dequeue();
                });
      
        }
    });
});