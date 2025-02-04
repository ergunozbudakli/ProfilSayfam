$(document).ready(function () {
    // Sidebar toggle
    $('#sidebarCollapse').on('click', function () {
        $('#sidebar').toggleClass('active');
    });

    // Alt menüyü aktif olan sayfaya göre aç
    var currentUrl = window.location.pathname;
    if (currentUrl.includes('/Admin/Analytics')) {
        $('#analyticsSubmenu').addClass('show');
    }

    // Aktif menü öğesini vurgula
    $('a[href="' + currentUrl + '"]').addClass('active');
}); 