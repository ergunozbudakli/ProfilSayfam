document.addEventListener('DOMContentLoaded', function() {
    const langSelector = document.querySelector('.language-selector');
    if (!langSelector) return;

    const currentLang = langSelector.querySelector('.current-lang');
    const dropdown = langSelector.querySelector('.lang-dropdown');

    if (!currentLang || !dropdown) return;

    // Dropdown'ı aç/kapa
    currentLang.addEventListener('click', function() {
        dropdown.classList.toggle('show');
    });

    // Dışarı tıklandığında dropdown'ı kapat
    document.addEventListener('click', function(e) {
        if (!langSelector.contains(e.target)) {
            dropdown.classList.remove('show');
        }
    });
}); 