// Ctrl+K komut paleti: sayfalar, komutlar ve canlı kayıt araması
(function () {
    const katman = document.getElementById('komutKatman');
    const girdi = document.getElementById('komutGirdi');
    const liste = document.getElementById('komutListe');
    if (!katman || !girdi || !liste) return;

    const turIkon = { 'Sayfa': '▤', 'Komut': '⌘', 'Parça': '◉', 'Lot': '▦', 'Hammadde': '◆', 'NCR': '⚠', 'Makine': '⚙' };
    let ogeler = [];
    let secili = 0;
    let istek = 0;
    let zamanlayici = null;

    function kucuk(s) { return (s || '').toLocaleLowerCase('tr-TR'); }

    function sabitOgeler() {
        const sonuc = [];
        document.querySelectorAll('.menu a[href]').forEach(function (a) {
            sonuc.push({ tur: 'Sayfa', baslik: a.textContent.trim(), alt: a.getAttribute('href'), link: a.getAttribute('href') });
        });
        sonuc.push({ tur: 'Komut', baslik: 'Tema değiştir (açık / karanlık)', alt: 'Görünüm', calistir: function () { const b = document.querySelector('[data-tema-degistir]'); if (b) b.click(); } });
        sonuc.push({ tur: 'Komut', baslik: 'Bildirimler', alt: '/bildirim', link: '/bildirim' });
        if (document.querySelector('.menu a[href*="/rapor/vardiya"]'))
            sonuc.push({ tur: 'Komut', baslik: 'Vardiya raporunu PDF indir', alt: 'Rapor', link: '/rapor/vardiyapdf' });
        sonuc.push({ tur: 'Komut', baslik: 'Duruşları Excel indir', alt: 'Rapor', link: '/rapor/durusexcel' });
        if (document.querySelector('[data-panel-duzenle]'))
            sonuc.push({ tur: 'Komut', baslik: 'Kontrol merkezini düzenle', alt: 'Panel', calistir: function () { document.querySelector('[data-panel-duzenle]').click(); } });
        sonuc.push({
            tur: 'Komut', baslik: 'Çıkış yap', alt: 'Oturum', calistir: function () {
                const f = document.querySelector('form[action*="cikis"]');
                if (f) f.submit();
            }
        });
        return sonuc;
    }

    function ciz(gruplar) {
        liste.innerHTML = '';
        ogeler = [];
        gruplar.forEach(function (g) {
            if (!g.ogeler.length) return;
            const b = document.createElement('div');
            b.className = 'komut-grup';
            b.textContent = g.ad;
            liste.appendChild(b);
            g.ogeler.forEach(function (o) {
                const i = ogeler.length;
                ogeler.push(o);
                const satir = document.createElement('div');
                satir.className = 'komut-oge';
                satir.setAttribute('role', 'option');
                satir.dataset.indeks = i;
                const ikon = document.createElement('span');
                ikon.className = 'komut-ikon';
                ikon.textContent = turIkon[o.tur] || '•';
                const metin = document.createElement('span');
                metin.className = 'komut-metin';
                const bas = document.createElement('strong');
                bas.textContent = o.baslik;
                const alt = document.createElement('small');
                alt.textContent = o.alt || '';
                metin.appendChild(bas);
                metin.appendChild(alt);
                const tur = document.createElement('span');
                tur.className = 'komut-tur';
                tur.textContent = o.tur;
                satir.appendChild(ikon);
                satir.appendChild(metin);
                satir.appendChild(tur);
                satir.addEventListener('mousemove', function () { sec(i); });
                satir.addEventListener('click', function () { calistir(i); });
                liste.appendChild(satir);
            });
        });
        if (!ogeler.length) {
            const bos = document.createElement('div');
            bos.className = 'komut-bos';
            bos.textContent = girdi.value.trim().length < 2 ? 'Aramak için yazmaya başlayın.' : 'Sonuç yok. Tam seri no veya lot numarasını deneyin.';
            liste.appendChild(bos);
        }
        sec(0);
    }

    function sec(i) {
        if (!ogeler.length) return;
        secili = (i + ogeler.length) % ogeler.length;
        liste.querySelectorAll('.komut-oge').forEach(function (s) {
            const aktif = parseInt(s.dataset.indeks, 10) === secili;
            s.classList.toggle('secili', aktif);
            s.setAttribute('aria-selected', aktif ? 'true' : 'false');
            if (aktif) s.scrollIntoView({ block: 'nearest' });
        });
    }

    function calistir(i) {
        const o = ogeler[i];
        if (!o) return;
        kapat();
        if (o.calistir) o.calistir();
        else if (o.link) location.href = o.link;
    }

    function guncelle() {
        const q = girdi.value.trim();
        const k = kucuk(q);
        const sabit = sabitOgeler().filter(function (o) { return !k || kucuk(o.baslik).indexOf(k) >= 0 || kucuk(o.alt).indexOf(k) >= 0; });
        const sayfalar = sabit.filter(function (o) { return o.tur === 'Sayfa'; });
        const komutlar = sabit.filter(function (o) { return o.tur === 'Komut'; });
        ciz([{ ad: 'Sayfalar', ogeler: sayfalar.slice(0, 8) }, { ad: 'Komutlar', ogeler: komutlar.slice(0, 6) }]);

        clearTimeout(zamanlayici);
        if (q.length < 2) return;
        const no = ++istek;
        zamanlayici = setTimeout(function () {
            fetch('/ara/oneri?q=' + encodeURIComponent(q), { headers: { 'Accept': 'application/json' } })
                .then(function (r) { return r.ok ? r.json() : []; })
                .then(function (kayitlar) {
                    if (no !== istek) return;
                    ciz([
                        { ad: 'Kayıtlar', ogeler: (kayitlar || []).map(function (x) { return { tur: x.tur, baslik: x.baslik, alt: x.alt, link: x.link }; }) },
                        { ad: 'Sayfalar', ogeler: sayfalar.slice(0, 5) },
                        { ad: 'Komutlar', ogeler: komutlar.slice(0, 4) }
                    ]);
                })
                .catch(function () { });
        }, 160);
    }

    function ac() {
        katman.hidden = false;
        document.body.classList.add('komut-acik');
        girdi.value = '';
        guncelle();
        setTimeout(function () { girdi.focus(); }, 10);
        if (window.Efekt) Efekt.cal('tik');
    }

    function kapat() {
        katman.hidden = true;
        document.body.classList.remove('komut-acik');
    }

    document.addEventListener('keydown', function (e) {
        if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
            e.preventDefault();
            if (katman.hidden) ac(); else kapat();
            return;
        }
        if (katman.hidden) return;
        if (e.key === 'Escape') { e.preventDefault(); kapat(); }
        else if (e.key === 'ArrowDown') { e.preventDefault(); sec(secili + 1); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); sec(secili - 1); }
        else if (e.key === 'Enter') { e.preventDefault(); calistir(secili); }
    });

    girdi.addEventListener('input', guncelle);
    katman.addEventListener('mousedown', function (e) { if (e.target === katman) kapat(); });
    document.addEventListener('click', function (e) {
        if (e.target.closest('[data-komut-ac]')) { e.preventDefault(); ac(); }
    });
})();
