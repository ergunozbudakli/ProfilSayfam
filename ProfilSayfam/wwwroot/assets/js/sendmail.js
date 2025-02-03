function Send() {
    // Form validasyonu
    const name = $('#NameInput').val();
    const email = $('#MailInput').val();
    const subject = $('#SubjectInput').val();
    const message = $('#MessageText').val();

    // Boş alan kontrolü
    if (!name || !email || !subject || !message) {
        alert('Lütfen tüm alanları doldurunuz.');
        return;
    }

    // Email kontrolü
    if (!checkEmailFormat(email)) {
        alert('Lütfen geçerli bir email adresi giriniz.');
        return;
    }

    // Loading durumu
    const sendButton = $('.send a');
    const originalText = sendButton.text();
    sendButton.text('Gönderiliyor...');
    sendButton.css('pointer-events', 'none');

    var url = "@Url.Action("SendMail", "Home")";
    var mail = {
        Name: name,
        Mail: email,
        Subject: subject,
        Message: message
    }

    axios.post(url, mail)
        .then(function (response) {
            if (response.data === "BAŞARILI") {
                alert("Mesajınız başarıyla gönderildi!");
                resetForm();
            } else {
                alert("Hata: " + response.data);
            }
        })
        .catch(function (error) {
            console.error('Hata:', error);
            alert("Mesaj gönderilirken bir hata oluştu: " + error.message);
        })
        .finally(function () {
            sendButton.text(originalText);
            sendButton.css('pointer-events', 'auto');
        });
}

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