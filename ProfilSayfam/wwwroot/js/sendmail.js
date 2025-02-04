function Send() {
    // Form validasyonu
    const name = $('#NameInput').val();
    const email = $('#MailInput').val();
    const subject = $('#SubjectInput').val();
    const message = $('#MessageText').val();

    // Dil kontrolü
    const currentLang = document.documentElement.lang || 'tr';
    const messages = {
        tr: {
            warning: 'Uyarı',
            error: 'Hata',
            success: 'Başarılı',
            ok: 'Tamam',
            allFieldsRequired: 'Lütfen tüm alanları doldurunuz.',
            invalidEmail: 'Lütfen geçerli bir email adresi giriniz.',
            nameLength: 'İsim en fazla 100 karakter olabilir.',
            subjectLength: 'Konu en fazla 200 karakter olabilir.',
            messageLength: 'Mesaj en fazla 2000 karakter olabilir.',
            sendingMessage: 'Gönderiliyor...',
            messageSent: 'Mesajınız başarıyla gönderildi!',
            checkFields: 'Lütfen form alanlarını kontrol ediniz.',
            sendError: 'Mesaj gönderilirken bir hata oluştu.'
        },
        en: {
            warning: 'Warning',
            error: 'Error',
            success: 'Success',
            ok: 'OK',
            allFieldsRequired: 'Please fill in all fields.',
            invalidEmail: 'Please enter a valid email address.',
            nameLength: 'Name cannot exceed 100 characters.',
            subjectLength: 'Subject cannot exceed 200 characters.',
            messageLength: 'Message cannot exceed 2000 characters.',
            sendingMessage: 'Sending...',
            messageSent: 'Your message has been sent successfully!',
            checkFields: 'Please check the form fields.',
            sendError: 'An error occurred while sending the message.'
        }
    };

    const msg = messages[currentLang] || messages.en;

    // Boş alan kontrolü
    if (!name || !email || !subject || !message) {
        Swal.fire({
            icon: 'warning',
            title: msg.warning,
            text: msg.allFieldsRequired,
            confirmButtonText: msg.ok
        });
        return;
    }

    // Email kontrolü
    if (!checkEmailFormat(email)) {
        Swal.fire({
            icon: 'error',
            title: msg.error,
            text: msg.invalidEmail,
            confirmButtonText: msg.ok
        });
        return;
    }

    // Karakter uzunluğu kontrolü
    if (name.length > 100) {
        Swal.fire({
            icon: 'error',
            title: msg.error,
            text: msg.nameLength,
            confirmButtonText: msg.ok
        });
        return;
    }

    if (subject.length > 200) {
        Swal.fire({
            icon: 'error',
            title: msg.error,
            text: msg.subjectLength,
            confirmButtonText: msg.ok
        });
        return;
    }

    if (message.length > 2000) {
        Swal.fire({
            icon: 'error',
            title: msg.error,
            text: msg.messageLength,
            confirmButtonText: msg.ok
        });
        return;
    }

    // Loading durumu
    const sendButton = $('.btn-send');
    const originalText = sendButton.find('span').text();
    const originalIcon = sendButton.find('i').clone();
    
    sendButton.find('span').text(msg.sendingMessage);
    sendButton.find('i').removeClass().addClass('bi bi-hourglass-split');
    sendButton.prop('disabled', true);

    const url = '/Home/SendMail';
    const mail = {
        Name: name,
        Mail: email,
        Subject: subject,
        Message: message
    }

    axios.post(url, mail)
        .then(function (response) {
            if (response.data === "BAŞARILI") {
                Swal.fire({
                    icon: 'success',
                    title: msg.success,
                    text: msg.messageSent,
                    confirmButtonText: msg.ok
                });
                resetForm();
            } else {
                Swal.fire({
                    icon: 'error',
                    title: msg.error,
                    text: response.data,
                    confirmButtonText: msg.ok
                });
            }
        })
        .catch(function (error) {
            console.error('Hata:', error);
            let errorMessage = msg.sendError;
            
            if (error.response) {
                if (error.response.status === 400) {
                    errorMessage = msg.checkFields;
                    if (error.response.data) {
                        errorMessage = error.response.data;
                    }
                }
            }
            
            Swal.fire({
                icon: 'error',
                title: msg.error,
                text: errorMessage,
                confirmButtonText: msg.ok
            });
        })
        .finally(function () {
            sendButton.find('span').text(originalText);
            sendButton.find('i').replaceWith(originalIcon);
            sendButton.prop('disabled', false);
        });
}

function checkEmailFormat(email) {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
}

function resetForm() {
    $('#NameInput').val('');
    $('#MailInput').val('');
    $('#SubjectInput').val('');
    $('#MessageText').val('');
}