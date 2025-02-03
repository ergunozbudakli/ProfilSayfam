
function show_menu() {
  $('#show_menuitems').addClass('active');
}

function hide_menu() {
  $('#show_menuitems').removeClass('active');
}
// --------------------------------------


function checkEmailFormat(str) {
    var atIndex = str.indexOf("@");
    var dotIndex = str.indexOf(".");
    return atIndex > 0 && dotIndex > atIndex;
}

function resetForm() {
    $('#NameInput').val('');
    $('#MailInput').val('');
    $('#SubjectInput').val('');
    $('#MessageText').val('');
}
$(document).ready(function() {
  var owl = $('.owl-carousel');
  owl.owlCarousel({
  items: 1,
  loop: true,
  margin: 10,
  autoplay: true,
  autoplayTimeout: 10000,
  autoplayHoverPause: true
  });
});


// --------------------------------------
