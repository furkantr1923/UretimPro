// İstasyon ekranı: barkod/QR okuyucu klavye gibi seri no yazar ve Enter'a basar
(function () {
    const kap = document.getElementById('istasyon');
    if (!kap) return;

    const makineId = parseInt(kap.dataset.makineId, 10);
    const girdi = document.getElementById('seriGirdi');
    const form = document.getElementById('okutForm');
    const sonuc = document.getElementById('sonuc');
    const sayac = document.getElementById('bugunSayac');
    let aktifSeri = null;

    function yaz(ad, oz, ic) {
        const e = document.createElement(ad);
        Object.keys(oz || {}).forEach(function (k) {
            if (k === 'class') e.className = oz[k];
            else if (k === 'text') e.textContent = oz[k];
            else e.setAttribute(k, oz[k]);
        });
        (ic || []).forEach(function (c) { if (c) e.appendChild(typeof c === 'string' ? document.createTextNode(c) : c); });
        return e;
    }

    async function gonder(adres, govde) {
        const yanit = await fetch(adres, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': window.CsrfToken },
            body: JSON.stringify(govde)
        });
        if (yanit.status === 401 || yanit.status === 403) return { durum: 'hata', baslik: 'Oturum', mesaj: 'Yetkiniz yok ya da oturum sona erdi.' };
        if (!yanit.ok) return { durum: 'hata', baslik: 'Sunucu hatası', mesaj: 'İşlem tamamlanamadı (' + yanit.status + ').' };
        return yanit.json();
    }

    function bilgiSatiri(c) {
        const parcalar = [];
        if (c.seriNo) parcalar.push(yaz('span', {}, [yaz('strong', { class: 'mono', text: c.seriNo })]));
        if (c.urun) parcalar.push(yaz('span', { text: c.urun }));
        if (c.emirNo) parcalar.push(yaz('span', { class: 'mono', text: c.emirNo }));
        if (c.adim) parcalar.push(yaz('span', { text: 'Adım ' + c.adimSira + '/' + c.adimSayisi + ' · ' + c.adim }));
        return yaz('div', { class: 'sonuc-bilgi' }, parcalar);
    }

    function goster(c) {
        sonuc.innerHTML = '';
        const ikonlar = { tamam: '✓', olcum: '⌖', hata: '!', red: '✕' };
        const kart = yaz('div', { class: 'sonuc-kart ' + c.durum, role: 'status' }, [
            yaz('h2', {}, [yaz('span', { 'aria-hidden': 'true', text: ikonlar[c.durum] || '•' }), c.baslik]),
            yaz('p', { text: c.mesaj, style: 'margin:8px 0 0' }),
            bilgiSatiri(c)
        ]);

        if (c.durum === 'tamam' && c.emirToplam) {
            const yuzde = Math.round(100 * c.emirTamamlanan / c.emirToplam);
            const cubuk = yaz('div', { class: 'ilerleme mt' }, [
                yaz('div', { class: 'cubuk' }, [yaz('div', { class: 'dolu yesil', style: '--w:' + yuzde + '%' })]),
                yaz('span', { class: 'metin', text: c.emirTamamlanan + '/' + c.emirToplam + ' tamamlandı' })
            ]);
            kart.appendChild(cubuk);
        }

        if (c.durum === 'olcum') kart.appendChild(olcumFormu(c));
        sonuc.appendChild(kart);

        if (c.durum === 'tamam') {
            Efekt.cal('bip');
            Efekt.flas();
            if (sayac) sayac.textContent = String((parseInt(sayac.textContent, 10) || 0) + 1);
            if (c.emirTamamlandi) {
                setTimeout(function () { Efekt.konfeti(); Efekt.cal('basari'); }, 300);
                Tost({ baslik: c.emirNo + ' tamamlandı', mesaj: 'Tüm parçalar üretildi. Tebrikler!', seviye: 'bilgi' });
            }
            girdi.value = '';
            girdi.focus();
        } else if (c.durum === 'red') {
            Efekt.alarm();
            Efekt.cal('alarm');
            girdi.value = '';
        } else if (c.durum === 'hata') {
            Efekt.cal('hata');
            girdi.select();
        } else if (c.durum === 'olcum') {
            Efekt.cal('tik');
            const ilk = sonuc.querySelector('input[data-kar]');
            if (ilk) ilk.focus();
        }
    }

    function olcumFormu(c) {
        const f = yaz('form', { class: 'mt', novalidate: 'novalidate' });
        c.olcumler.forEach(function (o) {
            const aletSec = yaz('select', { class: 'girdi', 'data-alet': o.id, 'aria-label': o.ad + ' ölçüm aleti' });
            const uygunlar = c.aletler.filter(function (a) { return a.tur === o.aletTuru; });
            const digerleri = c.aletler.filter(function (a) { return a.tur !== o.aletTuru; });
            uygunlar.concat(digerleri).forEach(function (a) {
                const sec = yaz('option', { value: a.id, text: a.kod + ' · ' + a.ad + (a.gecerli ? '' : ' (kalibrasyon geçti)') });
                if (!a.gecerli) sec.style.color = 'gray';
                aletSec.appendChild(sec);
            });
            const ilkGecerli = uygunlar.find(function (a) { return a.gecerli; });
            if (ilkGecerli) aletSec.value = ilkGecerli.id;

            const deger = yaz('input', {
                class: 'girdi mono', type: 'text', inputmode: 'decimal', autocomplete: 'off',
                'data-kar': o.id, 'data-alt': o.alt, 'data-ust': o.ust, placeholder: String(o.nominal).replace('.', ','),
                'aria-label': o.ad + ' değeri'
            });
            deger.addEventListener('input', function () {
                const v = parseFloat(deger.value.replace(',', '.'));
                deger.classList.toggle('disarda', !isNaN(v) && (v < o.alt || v > o.ust));
                deger.classList.toggle('icerde', !isNaN(v) && v >= o.alt && v <= o.ust);
            });

            const fmt = function (n) { return Number(n).toLocaleString('tr-TR', { maximumFractionDigits: 3 }); };
            f.appendChild(yaz('div', { class: 'olcum-satir' }, [
                yaz('div', {}, [yaz('strong', { text: o.ad }), yaz('div', { class: 'tolerans', text: fmt(o.alt) + ' – ' + fmt(o.ust) + ' ' + o.birim + ' · nominal ' + fmt(o.nominal) })]),
                yaz('label', { class: 'alan' }, ['Değer (' + o.birim + ')', deger]),
                yaz('label', { class: 'alan' }, ['Ölçüm aleti', aletSec])
            ]));
        });
        const dugme = yaz('button', { type: 'submit', class: 'buton bakir mt' }, ['Ölçümü kaydet ve adımı tamamla']);
        f.appendChild(dugme);

        f.addEventListener('submit', async function (e) {
            e.preventDefault();
            dugme.disabled = true;
            const olcumler = Array.from(f.querySelectorAll('input[data-kar]')).map(function (i) {
                const kar = parseInt(i.dataset.kar, 10);
                return {
                    karakteristikId: kar,
                    deger: i.value,
                    aletId: parseInt(f.querySelector('select[data-alet="' + kar + '"]').value, 10)
                };
            });
            try {
                goster(await gonder('/istasyon/olcum', { makineId: makineId, seriNo: aktifSeri, olcumler: olcumler }));
            } catch (hata) {
                goster({ durum: 'hata', baslik: 'Bağlantı hatası', mesaj: 'Sunucuya ulaşılamadı.' });
            } finally {
                dugme.disabled = false;
            }
        });
        return f;
    }

    form.addEventListener('submit', async function (e) {
        e.preventDefault();
        const seri = girdi.value.trim().toUpperCase();
        if (!seri) return;
        aktifSeri = seri;
        try {
            goster(await gonder('/istasyon/okut', { makineId: makineId, seriNo: seri }));
        } catch (hata) {
            goster({ durum: 'hata', baslik: 'Bağlantı hatası', mesaj: 'Sunucuya ulaşılamadı.' });
        }
    });

    document.querySelectorAll('[data-ornek-seri]').forEach(function (b) {
        b.addEventListener('click', function () {
            girdi.value = b.dataset.ornekSeri;
            form.requestSubmit();
        });
    });

    girdi.focus();
})();
