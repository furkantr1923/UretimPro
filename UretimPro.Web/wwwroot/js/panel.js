// Kişiselleştirilebilir kontrol merkezi: kartları sürükle-bırak, gizle, kullanıcıya kaydet
(function () {
    const izgara = document.getElementById('panelIzgara');
    if (!izgara) return;
    const kolonlar = Array.prototype.slice.call(izgara.querySelectorAll('[data-kolon]'));
    const cubuk = document.getElementById('panelCubuk');
    const gizliKutu = document.getElementById('gizliKartlar');

    function kartlar() { return Array.prototype.slice.call(izgara.querySelectorAll('.widget')); }
    function kart(id) { return izgara.querySelector('.widget[data-widget="' + id + '"]'); }
    function kolon(ad) { return izgara.querySelector('[data-kolon="' + ad + '"]'); }

    // ---------- Kayıtlı düzeni uygula (grafikler çizilmeden önce) ----------
    try {
        const duzen = izgara.dataset.duzen ? JSON.parse(izgara.dataset.duzen) : null;
        if (duzen) {
            ['genis', 'dar'].forEach(function (ad) {
                const k = kolon(ad);
                (duzen[ad] || []).forEach(function (id) { const w = kart(id); if (w && k) k.appendChild(w); });
            });
            (duzen.gizli || []).forEach(function (id) { const w = kart(id); if (w) w.classList.add('widget-gizli'); });
        }
    } catch (e) { console.warn('Panel düzeni okunamadı', e); }
    izgara.classList.remove('duzen-bekliyor');

    // ---------- Kaydet ----------
    let kayitZamanlayici = null;
    function kaydet(sifirla) {
        clearTimeout(kayitZamanlayici);
        kayitZamanlayici = setTimeout(function () {
            const govde = sifirla ? 'null' : JSON.stringify({
                genis: kolon('genis') ? Array.prototype.map.call(kolon('genis').querySelectorAll('.widget'), function (w) { return w.dataset.widget; }) : [],
                dar: kolon('dar') ? Array.prototype.map.call(kolon('dar').querySelectorAll('.widget'), function (w) { return w.dataset.widget; }) : [],
                gizli: kartlar().filter(function (w) { return w.classList.contains('widget-gizli'); }).map(function (w) { return w.dataset.widget; })
            });
            fetch('/panel/duzen', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': window.CsrfToken || '' },
                body: govde
            }).then(function (r) {
                if (!r.ok) throw new Error(r.status);
                if (sifirla) location.reload();
            }).catch(function () {
                if (window.Tost) Tost({ baslik: 'Düzen kaydedilemedi', mesaj: 'Bağlantıyı kontrol edip tekrar deneyin.', seviye: 'uyari' });
            });
        }, sifirla ? 0 : 400);
    }

    // ---------- Gizli kartlar ----------
    function gizliListesi() {
        if (!gizliKutu) return;
        gizliKutu.innerHTML = '';
        kartlar().filter(function (w) { return w.classList.contains('widget-gizli'); }).forEach(function (w) {
            const b = document.createElement('button');
            b.type = 'button';
            b.className = 'cip';
            b.textContent = '+ ' + (w.dataset.ad || w.dataset.widget);
            b.addEventListener('click', function () {
                w.classList.remove('widget-gizli');
                gizliListesi();
                kaydet();
                if (window.Grafik) Grafik.yenile();
            });
            gizliKutu.appendChild(b);
        });
    }

    // ---------- Düzenleme araç çubuğu ----------
    function dugme(metin, baslik, is) {
        const b = document.createElement('button');
        b.type = 'button';
        b.className = 'widget-dugme';
        b.textContent = metin;
        b.title = baslik;
        b.setAttribute('aria-label', baslik);
        b.addEventListener('click', function (e) { e.stopPropagation(); is(); });
        return b;
    }

    function aracCubugu(w) {
        if (w.querySelector('.widget-arac')) return;
        const a = document.createElement('div');
        a.className = 'widget-arac';
        const tut = document.createElement('span');
        tut.className = 'widget-tutamak';
        tut.textContent = '⠿ ' + (w.dataset.ad || '');
        a.appendChild(tut);
        const sag = document.createElement('span');
        sag.appendChild(dugme('↑', 'Yukarı taşı', function () {
            const once = w.previousElementSibling;
            if (once) { w.parentNode.insertBefore(w, once); kaydet(); }
        }));
        sag.appendChild(dugme('↓', 'Aşağı taşı', function () {
            const sonra = w.nextElementSibling;
            if (sonra) { w.parentNode.insertBefore(sonra, w); kaydet(); }
        }));
        sag.appendChild(dugme('⇄', 'Diğer sütuna taşı', function () {
            const hedef = w.parentNode.dataset.kolon === 'genis' ? kolon('dar') : kolon('genis');
            if (hedef) { hedef.appendChild(w); kaydet(); if (window.Grafik) Grafik.yenile(); }
        }));
        sag.appendChild(dugme('✕', 'Kartı gizle', function () {
            w.classList.add('widget-gizli');
            gizliListesi();
            kaydet();
        }));
        a.appendChild(sag);
        w.insertBefore(a, w.firstChild);
    }

    let suruklenen = null;
    function sonrakiKart(k, y) {
        const adaylar = Array.prototype.slice.call(k.querySelectorAll('.widget:not(.suruklenen):not(.widget-gizli)'));
        let enYakin = null, enYakinFark = -Infinity;
        adaylar.forEach(function (w) {
            const r = w.getBoundingClientRect();
            const fark = y - r.top - r.height / 2;
            if (fark < 0 && fark > enYakinFark) { enYakinFark = fark; enYakin = w; }
        });
        return enYakin;
    }

    kolonlar.forEach(function (k) {
        k.addEventListener('dragover', function (e) {
            if (!suruklenen) return;
            e.preventDefault();
            const sonra = sonrakiKart(k, e.clientY);
            if (sonra) k.insertBefore(suruklenen, sonra);
            else k.appendChild(suruklenen);
        });
    });

    function duzenlemeyiAc(ac) {
        document.body.classList.toggle('panel-duzenleme', ac);
        if (cubuk) cubuk.hidden = !ac;
        kartlar().forEach(function (w) {
            if (ac) aracCubugu(w);
            w.draggable = ac;
        });
        gizliListesi();
        if (!ac && window.Grafik) Grafik.yenile();
        if (window.Efekt) Efekt.cal('tik');
    }

    izgara.addEventListener('dragstart', function (e) {
        const w = e.target.closest && e.target.closest('.widget');
        if (!w || !document.body.classList.contains('panel-duzenleme')) return;
        suruklenen = w;
        w.classList.add('suruklenen');
        try { e.dataTransfer.setData('text/plain', w.dataset.widget); e.dataTransfer.effectAllowed = 'move'; } catch (x) { }
    });
    izgara.addEventListener('dragend', function () {
        if (!suruklenen) return;
        suruklenen.classList.remove('suruklenen');
        suruklenen = null;
        kaydet();
    });

    document.addEventListener('click', function (e) {
        if (e.target.closest('[data-panel-duzenle]')) duzenlemeyiAc(!document.body.classList.contains('panel-duzenleme'));
        if (e.target.closest('[data-panel-bitti]')) {
            duzenlemeyiAc(false);
            if (window.Tost) Tost({ baslik: 'Panel düzeni kaydedildi', mesaj: 'Bir sonraki girişinizde de bu düzen açılacak.', seviye: 'bilgi' });
        }
        if (e.target.closest('[data-panel-sifirla]')) kaydet(true);
    });
})();
