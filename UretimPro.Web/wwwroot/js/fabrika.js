// Canlı fabrika yerleşim planı: malzeme akışı, makine ayrıntısı, canlı işlem animasyonu
(function () {
    const svg = document.getElementById('fabrikaPlan');
    if (!svg) return;

    const NS = 'http://www.w3.org/2000/svg';
    const G = 150, Y = 96;
    const makineler = JSON.parse(svg.dataset.makineler || '[]');
    const akislar = JSON.parse(svg.dataset.akislar || '[]');
    const katman = document.getElementById('akisKatman');
    const detay = document.getElementById('makineDetay');
    const idIle = {};
    makineler.forEach(function (m) { idIle[m.id] = m; });
    const azHareket = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    // ---------- Akış yolları (makinelerin üzerinden geçmeyen dik açılı rota) ----------
    function kutuyaCarparMi(x1, y1, x2, y2) {
        const pay = 7;
        const solX = Math.min(x1, x2), sagX = Math.max(x1, x2), ustY = Math.min(y1, y2), altY = Math.max(y1, y2);
        return makineler.some(function (m) {
            return sagX > m.x - pay && solX < m.x + G + pay && altY > m.y - pay && ustY < m.y + Y + pay;
        });
    }

    function yol(kaynak, hedef) {
        const ileri = hedef.x >= kaynak.x;
        const sx = ileri ? kaynak.x + G : kaynak.x, sy = kaynak.y + Y / 2;
        const tx = ileri ? hedef.x : hedef.x + G, ty = hedef.y + Y / 2;
        const yon = ileri ? 1 : -1;
        if (Math.abs(tx - sx) <= 70) {
            const c = (sx + tx) / 2;
            return 'M' + sx + ' ' + sy + ' H' + c + ' V' + ty + ' H' + tx;
        }
        const cA = sx + yon * 25, cB = tx - yon * 25;
        let enIyi = null, enIyiMaliyet = Infinity;
        for (let a = 34; a <= 526; a += 4) {
            const maliyet = Math.abs(sy - a) + Math.abs(a - ty);
            if (maliyet >= enIyiMaliyet) continue;
            if (kutuyaCarparMi(cA, sy, cA, a) || kutuyaCarparMi(cA, a, cB, a) || kutuyaCarparMi(cB, a, cB, ty)) continue;
            enIyi = a;
            enIyiMaliyet = maliyet;
        }
        if (enIyi === null) return 'M' + sx + ' ' + sy + ' C' + (sx + yon * 80) + ' ' + sy + ' ' + (tx - yon * 80) + ' ' + ty + ' ' + tx + ' ' + ty;
        return 'M' + sx + ' ' + sy + ' H' + cA + ' V' + enIyi + ' H' + cB + ' V' + ty + ' H' + tx;
    }

    const kenarlar = [];
    function kenarEkle(d, adet, hedefId) {
        const p = document.createElementNS(NS, 'path');
        p.setAttribute('d', d);
        p.setAttribute('class', 'akis' + (adet > 0 ? ' aktif' : ''));
        p.setAttribute('marker-end', 'url(#okUcu)');
        if (adet > 0) p.style.setProperty('--hiz', Math.max(0.6, 2.6 - Math.log10(adet + 1)) + 's');
        katman.appendChild(p);
        kenarlar.push({ yol: p, adet: adet, hedef: hedefId });
    }

    akislar.forEach(function (a) {
        const k = idIle[a.kaynak], h = idIle[a.hedef];
        if (k && h) kenarEkle(yol(k, h), a.adet, h.id);
    });

    // Depodan ilk adımlara, son adımlardan sevkiyata
    const gelenVar = new Set(akislar.map(function (a) { return a.hedef; }));
    const gidenVar = new Set(akislar.map(function (a) { return a.kaynak; }));
    makineler.forEach(function (m) {
        if (gidenVar.has(m.id) && !gelenVar.has(m.id))
            kenarEkle('M160 ' + (m.y + Y / 2) + ' H' + m.x, Math.round(m.adet / 3), m.id);
        if (gelenVar.has(m.id) && !gidenVar.has(m.id))
            kenarEkle('M' + (m.x + G) + ' ' + (m.y + Y / 2) + ' H1188', Math.round(m.adet / 3), null);
    });

    // ---------- Hareketli parça noktaları ----------
    function noktaGonder(kenar, vurgu) {
        if (azHareket || document.hidden) return;
        const uzunluk = kenar.yol.getTotalLength();
        const n = document.createElementNS(NS, 'circle');
        n.setAttribute('r', vurgu ? 6 : 4.2);
        n.setAttribute('class', 'akis-nokta' + (vurgu ? ' vurgu' : ''));
        katman.appendChild(n);
        const sure = 1500 + uzunluk * 2.2;
        const bas = performance.now();
        (function adim(t) {
            const o = Math.min(1, (t - bas) / sure);
            const e = o < 0.5 ? 2 * o * o : 1 - Math.pow(-2 * o + 2, 2) / 2;
            const p = kenar.yol.getPointAtLength(e * uzunluk);
            n.setAttribute('cx', p.x);
            n.setAttribute('cy', p.y);
            if (o < 1) requestAnimationFrame(adim);
            else n.remove();
        })(bas);
    }

    const aktifler = kenarlar.filter(function (k) { return k.adet > 0; });
    const toplam = aktifler.reduce(function (t, k) { return t + k.adet; }, 0);
    if (toplam > 0) {
        setInterval(function () {
            let r = Math.random() * toplam;
            for (let i = 0; i < aktifler.length; i++) {
                if (r < aktifler[i].adet) { noktaGonder(aktifler[i]); return; }
                r -= aktifler[i].adet;
            }
        }, 650);
    }

    // ---------- Makine ayrıntısı ----------
    function el(etiket, sinif, metin) {
        const e = document.createElement(etiket);
        if (sinif) e.className = sinif;
        if (metin !== undefined && metin !== null) e.textContent = metin;
        return e;
    }
    function yuzde(v) { return '%' + (v * 100).toLocaleString('tr-TR', { maximumFractionDigits: 1 }); }
    function oeeSinif(v) { return v >= 0.85 ? 'yesil' : v >= 0.6 ? 'sari' : 'kirmizi'; }

    function cubuk(ad, deger, sinif) {
        const kutu = el('div', 'oee-satir');
        const ust = el('div', 'esnek arasi');
        ust.appendChild(el('span', 'renk-soluk', ad));
        ust.appendChild(el('strong', 'mono', yuzde(deger)));
        const il = el('div', 'ilerleme');
        const c = el('div', 'cubuk');
        const d = el('div', 'dolu ' + (sinif || ''));
        d.style.setProperty('--w', Math.min(100, deger * 100) + '%');
        c.appendChild(d);
        il.appendChild(c);
        kutu.appendChild(ust);
        kutu.appendChild(il);
        return kutu;
    }

    function bilgi(ad, deger) {
        const b = el('div', 'bilgi-kutu');
        b.appendChild(el('small', null, ad));
        b.appendChild(el('strong', null, deger));
        return b;
    }

    function link(href, metin, sinif) {
        const a = el('a', 'buton ' + (sinif || 'ikincil') + ' kucuk', metin);
        a.href = href;
        return a;
    }

    function goster(m) {
        if (!detay || !m) return;
        svg.querySelectorAll('.plan-makine.secili').forEach(function (g) { g.classList.remove('secili'); });
        const g = svg.querySelector('.plan-makine[data-id="' + m.id + '"]');
        if (g) g.classList.add('secili');

        detay.innerHTML = '';
        const bas = el('div', 'kart-baslik');
        const h2 = el('h2', 'esnek', m.kod + ' · ' + m.ad + ' ');
        h2.appendChild(el('span', 'rozet ' + m.sinif, m.durum));
        bas.appendChild(h2);
        bas.appendChild(el('small', 'renk-soluk', m.tur));
        detay.appendChild(bas);

        const izgara = el('div', 'makine-detay');
        const sol = el('div', 'oee-buyuk');
        sol.appendChild(el('small', 'renk-soluk', 'Bugünkü OEE'));
        sol.appendChild(el('strong', 'renk-' + oeeSinif(m.oee), m.planli ? yuzde(m.oee) : '–'));
        sol.appendChild(cubuk('Kullanılabilirlik', m.kul));
        sol.appendChild(cubuk('Performans', m.perf));
        sol.appendChild(cubuk('Kalite', m.kal, 'yesil'));
        izgara.appendChild(sol);

        const sag = el('div', 'dikey');
        const bilgiler = el('div', 'bilgi-izgara');
        bilgiler.appendChild(bilgi('Bugün işlem', String(m.adet)));
        bilgiler.appendChild(bilgi('Duruş (bugün)', m.durusDk + ' dk'));
        bilgiler.appendChild(bilgi('İdeal çevrim', m.ideal + ' sn'));
        bilgiler.appendChild(bilgi('Son operatör', m.op || '—'));
        sag.appendChild(bilgiler);

        if (m.durus) {
            const d = el('div', 'mesaj ' + (m.durus.neden === 'Arıza' ? 'hata' : 'uyari'));
            d.appendChild(el('strong', null, 'Duruşta · ' + m.durus.neden + ' · ' + m.durus.sure));
            d.appendChild(el('span', null, ' ' + (m.durus.aciklama || '') + (m.durus.kisi ? ' (' + m.durus.kisi + ')' : '')));
            sag.appendChild(d);
        }
        if (m.bakim) {
            const b = el('div', 'esnek');
            b.appendChild(el('span', 'rozet ' + m.bakim.sinif, 'Bakım: ' + m.bakim.metin));
            b.appendChild(el('small', 'renk-soluk', m.bakim.kalanGun + ' gün / ' + m.bakim.kalanAdet.toLocaleString('tr-TR') + ' adet kaldı'));
            sag.appendChild(b);
        }
        const eylem = el('div', 'esnek');
        eylem.style.flexWrap = 'wrap';
        eylem.appendChild(link('/istasyon/ekran/' + m.id, 'İstasyon ekranı', 'bakir'));
        eylem.appendChild(link('/durus', 'Duruş kaydı'));
        eylem.appendChild(link('/bakim', 'Bakım planı'));
        eylem.appendChild(link('/rapor/vardiya', 'Vardiya OEE'));
        sag.appendChild(eylem);
        izgara.appendChild(sag);
        detay.appendChild(izgara);

        detay.classList.remove('vurgula-gel');
        void detay.offsetWidth;
        detay.classList.add('vurgula-gel');
        if (window.Efekt) Efekt.cal('tik');
    }

    svg.addEventListener('click', function (e) {
        const g = e.target.closest('.plan-makine');
        if (g) {
            goster(idIle[g.dataset.id]);
            if (window.innerWidth < 960 && detay) detay.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    });
    svg.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' && e.key !== ' ') return;
        const g = e.target.closest('.plan-makine');
        if (g) { e.preventDefault(); goster(idIle[g.dataset.id]); }
    });

    const secili = svg.dataset.secili;
    if (secili) {
        const m = makineler.find(function (x) { return x.kod === secili; });
        if (m) setTimeout(function () { goster(m); }, 300);
    }

    // ---------- Canlı işlemler ----------
    document.addEventListener('canli', function (e) {
        const m = e.detail;
        if (!m || m.tur !== 'islem') return;
        const g = svg.querySelector('.plan-makine[data-kod="' + (window.CSS && CSS.escape ? CSS.escape(m.makine) : m.makine) + '"]');
        if (!g) return;
        const sayi = g.querySelector('.sayi');
        if (sayi) {
            const yeni = (parseInt(sayi.dataset.adet || '0', 10) || 0) + 1;
            sayi.dataset.adet = yeni;
            sayi.textContent = yeni;
        }
        const toplamEl = document.getElementById('fabrikaToplam');
        if (toplamEl) toplamEl.textContent = ((parseInt(toplamEl.textContent.replace(/\D/g, ''), 10) || 0) + 1).toLocaleString('tr-TR');
        g.classList.remove('parla');
        void g.getBBox();
        g.classList.add('parla');
        const hedefId = parseInt(g.dataset.id, 10);
        kenarlar.filter(function (k) { return k.hedef === hedefId; }).forEach(function (k) { noktaGonder(k, true); });
        if (idIle[hedefId]) idIle[hedefId].adet += 1;
    });
})();
