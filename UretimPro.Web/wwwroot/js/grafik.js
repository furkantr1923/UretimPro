// Hafif, animasyonlu SVG grafikler (harici kütüphane yok, çevrimdışı çalışır)
// Kullanım: <div class="grafik" data-grafik="alan|halka|spc|cubuk" data-veri='{...}'></div>
window.Grafik = (function () {
    const NS = 'http://www.w3.org/2000/svg';
    let ipucu = null;

    function el(ad, oz, ebeveyn) {
        const e = document.createElementNS(NS, ad);
        Object.keys(oz || {}).forEach(function (k) { e.setAttribute(k, oz[k]); });
        if (ebeveyn) ebeveyn.appendChild(e);
        return e;
    }

    function renk(ad) {
        return getComputedStyle(document.documentElement).getPropertyValue(ad).trim() || '#888';
    }

    function sayiYaz(n, basamak) {
        return Number(n).toLocaleString('tr-TR', { minimumFractionDigits: basamak || 0, maximumFractionDigits: basamak || 0 });
    }

    function ipucuGoster(metin, olay) {
        if (!ipucu) {
            ipucu = document.createElement('div');
            ipucu.className = 'grafik-ipucu';
            document.body.appendChild(ipucu);
        }
        ipucu.innerHTML = '';
        metin.split('\n').forEach(function (satir, i) {
            const s = document.createElement(i === 0 ? 'strong' : 'div');
            s.textContent = satir;
            ipucu.appendChild(s);
        });
        ipucu.style.left = (olay.clientX + 14) + 'px';
        ipucu.style.top = (olay.clientY - 10) + 'px';
        ipucu.classList.add('gorunur');
    }

    function ipucuGizle() { if (ipucu) ipucu.classList.remove('gorunur'); }

    function etkilesim(hedef, metin) {
        hedef.style.cursor = 'pointer';
        hedef.addEventListener('mousemove', function (e) { ipucuGoster(metin, e); });
        hedef.addEventListener('mouseleave', ipucuGizle);
    }

    // ---------- Alan grafiği (günlük üretim) ----------
    function alan(kap, veri) {
        const G = 720, Y = 240, sol = 40, sag = 12, ust = 16, alt = 30;
        const svg = el('svg', { viewBox: '0 0 ' + G + ' ' + Y, role: 'img', 'aria-label': kap.dataset.etiket || 'Grafik' }, kap);
        const degerler = veri.map(function (d) { return d.deger; });
        const hedef = kap.dataset.hedef ? parseFloat(kap.dataset.hedef) : null;
        const enBuyuk = kap.dataset.birim === '%' ? 100 : Math.max(1, Math.max.apply(null, degerler)) * 1.15;
        const x = function (i) { return sol + i * (G - sol - sag) / Math.max(1, veri.length - 1); };
        const y = function (v) { return ust + (Y - ust - alt) * (1 - v / enBuyuk); };
        const ana = renk('--bakir');

        const defs = el('defs', {}, svg);
        const id = 'gr' + Math.random().toString(36).slice(2);
        const gr = el('linearGradient', { id: id, x1: 0, y1: 0, x2: 0, y2: 1 }, defs);
        el('stop', { offset: '0%', 'stop-color': ana, 'stop-opacity': 0.38 }, gr);
        el('stop', { offset: '100%', 'stop-color': ana, 'stop-opacity': 0 }, gr);

        for (let i = 0; i <= 4; i++) {
            const v = enBuyuk * i / 4;
            el('line', { x1: sol, x2: G - sag, y1: y(v), y2: y(v), stroke: renk('--cizgi'), 'stroke-dasharray': '3 5' }, svg);
            el('text', { x: sol - 8, y: y(v) + 4, 'text-anchor': 'end', 'font-size': 11, fill: renk('--soluk'), 'font-family': 'IBM Plex Mono, monospace' }, svg).textContent = Math.round(v);
        }

        if (hedef !== null) {
            el('line', { x1: sol, x2: G - sag, y1: y(hedef), y2: y(hedef), stroke: renk('--yesil'), 'stroke-width': 1.5, 'stroke-dasharray': '6 4' }, svg);
            el('text', { x: G - sag, y: y(hedef) - 6, 'text-anchor': 'end', 'font-size': 11, fill: renk('--yesil'), 'font-weight': 600 }, svg).textContent = 'Hedef %' + hedef;
        }

        let yol = '';
        veri.forEach(function (d, i) { yol += (i === 0 ? 'M' : 'L') + x(i) + ' ' + y(d.deger) + ' '; });
        el('path', { d: yol + 'L' + x(veri.length - 1) + ' ' + (Y - alt) + ' L' + x(0) + ' ' + (Y - alt) + ' Z', fill: 'url(#' + id + ')', class: 'alan-anim' }, svg);
        const cizgi = el('path', { d: yol, fill: 'none', stroke: ana, 'stroke-width': 3, 'stroke-linejoin': 'round', 'stroke-linecap': 'round', class: 'cizgi-anim' }, svg);
        cizgi.style.setProperty('--uzunluk', Math.ceil(cizgi.getTotalLength ? cizgi.getTotalLength() : 2000));

        veri.forEach(function (d, i) {
            const n = el('circle', { cx: x(i), cy: y(d.deger), r: 4.5, fill: renk('--yuzey'), stroke: ana, 'stroke-width': 2.5, class: 'nokta-anim' }, svg);
            n.style.animationDelay = (0.6 + i * 0.05) + 's';
            const birim = kap.dataset.birim || 'parça';
            etkilesim(n, d.etiket + '\n' + (birim === '%' ? '%' + sayiYaz(d.deger, 1) : sayiYaz(d.deger) + ' ' + birim));
            if (i % 2 === 0 || veri.length < 10)
                el('text', { x: x(i), y: Y - 8, 'text-anchor': 'middle', 'font-size': 11, fill: renk('--soluk') }, svg).textContent = d.etiket;
        });
    }

    // ---------- Halka grafik (dağılım) ----------
    function halka(kap, veri) {
        const palet = ['--bakir', '--yesil', '--mavi', '--sari', '--kirmizi', '--yazi-2'].map(renk);
        const toplam = veri.reduce(function (t, d) { return t + d.deger; }, 0) || 1;
        const kutu = document.createElement('div');
        kutu.style.cssText = 'display:flex;flex-wrap:wrap;align-items:center;gap:20px';
        kap.appendChild(kutu);

        const svg = el('svg', { viewBox: '0 0 200 200', role: 'img', 'aria-label': kap.dataset.etiket || 'Dağılım' }, kutu);
        svg.style.width = '180px';
        svg.style.flexShrink = '0';
        const r = 72, cevre = 2 * Math.PI * r;
        let ofset = 0;
        veri.forEach(function (d, i) {
            const pay = d.deger / toplam * cevre;
            const dilim = el('circle', {
                cx: 100, cy: 100, r: r, fill: 'none', stroke: palet[i % palet.length], 'stroke-width': 26,
                'stroke-dasharray': '0 ' + cevre, 'stroke-dashoffset': -ofset, transform: 'rotate(-90 100 100)'
            }, svg);
            dilim.style.transition = 'stroke-dasharray 1.1s cubic-bezier(.2,.8,.2,1) ' + (i * 0.12) + 's, stroke-width .2s';
            requestAnimationFrame(function () {
                requestAnimationFrame(function () { dilim.setAttribute('stroke-dasharray', Math.max(0, pay - 2) + ' ' + cevre); });
            });
            dilim.addEventListener('mouseenter', function () { dilim.setAttribute('stroke-width', 32); });
            dilim.addEventListener('mouseleave', function () { dilim.setAttribute('stroke-width', 26); });
            etkilesim(dilim, d.etiket + '\n' + sayiYaz(d.deger) + ' (%' + sayiYaz(d.deger / toplam * 100) + ')');
            ofset += pay;
        });
        el('text', { x: 100, y: 98, 'text-anchor': 'middle', 'font-size': 30, 'font-weight': 600, fill: renk('--yazi'), 'font-family': 'IBM Plex Mono, monospace' }, svg).textContent = sayiYaz(toplam);
        el('text', { x: 100, y: 120, 'text-anchor': 'middle', 'font-size': 12, fill: renk('--soluk') }, svg).textContent = kap.dataset.birim || 'toplam';

        const lejant = document.createElement('div');
        lejant.className = 'dikey';
        lejant.style.flex = '1 1 160px';
        veri.forEach(function (d, i) {
            const s = document.createElement('div');
            s.className = 'esnek arasi';
            s.style.fontSize = '14px';
            const sol = document.createElement('span');
            sol.className = 'esnek';
            const kare = document.createElement('i');
            kare.style.cssText = 'width:12px;height:12px;border-radius:4px;display:inline-block;background:' + palet[i % palet.length];
            sol.appendChild(kare);
            sol.appendChild(document.createTextNode(d.etiket));
            const sag = document.createElement('strong');
            sag.className = 'mono';
            sag.textContent = sayiYaz(d.deger);
            s.appendChild(sol);
            s.appendChild(sag);
            lejant.appendChild(s);
        });
        kutu.appendChild(lejant);
    }

    // ---------- SPC kontrol grafiği ----------
    function spc(kap, v) {
        const G = 820, Y = 320, sol = 64, sag = 56, ust = 20, alt = 34;
        const svg = el('svg', { viewBox: '0 0 ' + G + ' ' + Y, role: 'img', 'aria-label': 'SPC kontrol grafiği' }, kap);
        const tum = v.degerler.concat([v.uks, v.aks, v.ust, v.alt]);
        let enK = Math.min.apply(null, tum), enB = Math.max.apply(null, tum);
        const pay = (enB - enK) * 0.08 || 0.01;
        enK -= pay; enB += pay;
        const x = function (i) { return sol + i * (G - sol - sag) / Math.max(1, v.degerler.length - 1); };
        const y = function (d) { return ust + (Y - ust - alt) * (1 - (d - enK) / (enB - enK)); };
        const kir = renk('--kirmizi'), yes = renk('--yesil'), sar = renk('--sari'), bak = renk('--bakir'), sol2 = renk('--soluk');

        // ±1σ ve ±2σ bölgeleri
        const s = v.sigma;
        if (s > 0) {
            el('rect', { x: sol, width: G - sol - sag, y: y(v.ort + 3 * s), height: Math.max(0, y(v.ort - 3 * s) - y(v.ort + 3 * s)), fill: sar, opacity: 0.07 }, svg);
            el('rect', { x: sol, width: G - sol - sag, y: y(v.ort + 2 * s), height: Math.max(0, y(v.ort - 2 * s) - y(v.ort + 2 * s)), fill: yes, opacity: 0.06 }, svg);
            el('rect', { x: sol, width: G - sol - sag, y: y(v.ort + s), height: Math.max(0, y(v.ort - s) - y(v.ort + s)), fill: yes, opacity: 0.08 }, svg);
        }

        function yatay(deger, renk1, kesik, etiket, kalin) {
            el('line', { x1: sol, x2: G - sag, y1: y(deger), y2: y(deger), stroke: renk1, 'stroke-width': kalin || 1.5, 'stroke-dasharray': kesik ? '7 5' : 'none' }, svg);
            el('text', { x: G - sag + 6, y: y(deger) + 4, 'font-size': 11, fill: renk1, 'font-weight': 600 }, svg).textContent = etiket;
            el('text', { x: sol - 8, y: y(deger) + 4, 'text-anchor': 'end', 'font-size': 11, fill: sol2, 'font-family': 'IBM Plex Mono, monospace' }, svg).textContent = sayiYaz(deger, 3);
        }
        yatay(v.ust, kir, false, 'ÜTS', 2);
        yatay(v.alt, kir, false, 'ATS', 2);
        yatay(v.uks, sar, true, 'ÜKS');
        yatay(v.aks, sar, true, 'AKS');
        yatay(v.ort, sol2, true, 'X̄');

        let yol = '';
        v.degerler.forEach(function (d, i) { yol += (i === 0 ? 'M' : 'L') + x(i) + ' ' + y(d) + ' '; });
        const cizgi = el('path', { d: yol, fill: 'none', stroke: renk('--mavi'), 'stroke-width': 2.2, 'stroke-linejoin': 'round', class: 'cizgi-anim' }, svg);
        cizgi.style.setProperty('--uzunluk', Math.ceil(cizgi.getTotalLength ? cizgi.getTotalLength() : 3000));

        const ihlal = new Set(v.ihlaller || []);
        v.degerler.forEach(function (d, i) {
            const kotu = ihlal.has(i);
            const disarda = d > v.ust || d < v.alt;
            const r = kotu || disarda ? 6 : 4;
            if (kotu || disarda) {
                const h = el('circle', { cx: x(i), cy: y(d), r: r, fill: 'none', stroke: disarda ? kir : bak, 'stroke-width': 2, class: 'ihlal-halka' }, svg);
                h.style.animationDelay = (i * 0.04) + 's';
            }
            const n = el('circle', { cx: x(i), cy: y(d), r: r, fill: disarda ? kir : (kotu ? bak : renk('--yuzey')), stroke: disarda ? kir : (kotu ? bak : renk('--mavi')), 'stroke-width': 2.2, class: 'nokta-anim' }, svg);
            n.style.animationDelay = (0.4 + i * 0.03) + 's';
            const etiket = (v.etiketler && v.etiketler[i]) || ('#' + (i + 1));
            etkilesim(n, etiket + '\n' + sayiYaz(d, 3) + ' ' + (v.birim || '') + (disarda ? '\nTOLERANS DIŞI' : (kotu ? '\nSPC kuralı ihlali' : '')));
        });
    }

    // ---------- Çubuk grafik ----------
    function cubuk(kap, veri) {
        const G = 720, Y = 240, sol = 40, sag = 12, ust = 16, alt = 30;
        const svg = el('svg', { viewBox: '0 0 ' + G + ' ' + Y, role: 'img', 'aria-label': kap.dataset.etiket || 'Grafik' }, kap);
        const enBuyuk = Math.max(1, Math.max.apply(null, veri.map(function (d) { return d.deger; }))) * 1.15;
        const genislik = (G - sol - sag) / veri.length;
        const y = function (v) { return ust + (Y - ust - alt) * (1 - v / enBuyuk); };
        const palet = ['--bakir', '--yesil', '--mavi', '--sari', '--kirmizi'].map(renk);
        for (let i = 0; i <= 4; i++) {
            const v = enBuyuk * i / 4;
            el('line', { x1: sol, x2: G - sag, y1: y(v), y2: y(v), stroke: renk('--cizgi'), 'stroke-dasharray': '3 5' }, svg);
        }
        veri.forEach(function (d, i) {
            const bx = sol + i * genislik + genislik * 0.2;
            const c = el('rect', { x: bx, width: genislik * 0.6, y: y(d.deger), height: Math.max(1, Y - alt - y(d.deger)), rx: 6, fill: palet[i % palet.length], class: 'cubuk-anim' }, svg);
            c.style.animationDelay = (i * 0.08) + 's';
            etkilesim(c, d.etiket + '\n' + sayiYaz(d.deger, d.deger % 1 ? 1 : 0) + ' ' + (kap.dataset.birim || ''));
            el('text', { x: bx + genislik * 0.3, y: Y - 10, 'text-anchor': 'middle', 'font-size': 11, fill: renk('--soluk') }, svg).textContent = d.etiket;
        });
    }

    // ---------- Gösterge (OEE yarım daire) ----------
    function gosterge(kap, v) {
        const deger = Math.max(0, Math.min(1, v.deger || 0));
        const svg = el('svg', { viewBox: '0 0 240 150', role: 'img', 'aria-label': (v.etiket || 'Gösterge') + ' %' + sayiYaz(deger * 100, 1) }, kap);
        svg.style.maxWidth = '300px';
        svg.style.margin = '0 auto';
        const r = 92, cx = 120, cy = 124;
        const yay = function (bas, bit) {
            const a1 = Math.PI * (1 - bas), a2 = Math.PI * (1 - bit);
            return 'M' + (cx + r * Math.cos(a1)) + ' ' + (cy - r * Math.sin(a1)) + ' A' + r + ' ' + r + ' 0 0 1 ' + (cx + r * Math.cos(a2)) + ' ' + (cy - r * Math.sin(a2));
        };
        // Bölgeler: <%60 düşük, %60-85 tipik, >%85 dünya standardı
        el('path', { d: yay(0, 0.6), fill: 'none', stroke: renk('--kirmizi-acik'), 'stroke-width': 18 }, svg);
        el('path', { d: yay(0.6, 0.85), fill: 'none', stroke: renk('--sari-acik'), 'stroke-width': 18 }, svg);
        el('path', { d: yay(0.85, 1), fill: 'none', stroke: renk('--yesil-acik'), 'stroke-width': 18 }, svg);
        const ana = deger >= 0.85 ? renk('--yesil') : deger >= 0.6 ? renk('--sari') : renk('--kirmizi');
        const dolu = el('path', { d: yay(0, Math.max(0.001, deger)), fill: 'none', stroke: ana, 'stroke-width': 18, 'stroke-linecap': 'round' }, svg);
        const uz = Math.PI * r * Math.max(0.001, deger);
        dolu.style.strokeDasharray = uz + ' ' + (uz + 10);
        dolu.style.strokeDashoffset = uz;
        dolu.style.transition = 'stroke-dashoffset 1.4s cubic-bezier(.2,.8,.2,1)';
        requestAnimationFrame(function () { requestAnimationFrame(function () { dolu.style.strokeDashoffset = 0; }); });

        // Hedef çizgisi (%85)
        const h = Math.PI * (1 - 0.85);
        el('line', { x1: cx + (r - 14) * Math.cos(h), y1: cy - (r - 14) * Math.sin(h), x2: cx + (r + 14) * Math.cos(h), y2: cy - (r + 14) * Math.sin(h), stroke: renk('--yesil'), 'stroke-width': 2.5 }, svg);

        const metin = el('text', { x: cx, y: cy - 10, 'text-anchor': 'middle', 'font-size': 34, 'font-weight': 700, fill: ana, 'font-family': 'IBM Plex Mono, monospace' }, svg);
        const bas = performance.now();
        (function say(t) {
            const o = Math.min(1, (t - bas) / 1400);
            metin.textContent = '%' + sayiYaz(deger * 100 * (1 - Math.pow(1 - o, 3)), 1);
            if (o < 1) requestAnimationFrame(say);
        })(bas);
        el('text', { x: cx - r, y: cy + 20, 'text-anchor': 'middle', 'font-size': 10, fill: renk('--soluk') }, svg).textContent = '0';
        el('text', { x: cx + r, y: cy + 20, 'text-anchor': 'middle', 'font-size': 10, fill: renk('--soluk') }, svg).textContent = '100';
        el('text', { x: cx, y: cy + 18, 'text-anchor': 'middle', 'font-size': 12, fill: renk('--soluk') }, svg).textContent = (v.etiket || '') + ' · hedef %85';
    }

    // ---------- Pareto (sıralı çubuk + kümülatif çizgi + %80 çizgisi) ----------
    function pareto(kap, veri) {
        const kompakt = kap.dataset.kompakt === '1';
        const G = kompakt ? 420 : 720, Y = kompakt ? 290 : 300, sol = 42, sag = 40, ust = 18, alt = kompakt ? 62 : 64;
        const svg = el('svg', { viewBox: '0 0 ' + G + ' ' + Y, role: 'img', 'aria-label': 'Pareto analizi' }, kap);
        if (!veri.length) return;
        const enBuyuk = Math.max.apply(null, veri.map(function (d) { return d.deger; })) * 1.1;
        const genislik = (G - sol - sag) / veri.length;
        const y = function (v) { return ust + (Y - ust - alt) * (1 - v / enBuyuk); };
        const yy = function (p) { return ust + (Y - ust - alt) * (1 - p / 100); };
        const bak = renk('--bakir'), sar = renk('--sari'), kir = renk('--kirmizi'), sol2 = renk('--soluk');
        const birim = kap.dataset.birim || '';

        for (let i = 0; i <= 4; i++) {
            const p = i * 25;
            el('line', { x1: sol, x2: G - sag, y1: yy(p), y2: yy(p), stroke: renk('--cizgi'), 'stroke-dasharray': '3 5' }, svg);
            el('text', { x: G - sag + 6, y: yy(p) + 4, 'font-size': 10, fill: sol2, 'font-family': 'IBM Plex Mono, monospace' }, svg).textContent = '%' + p;
            el('text', { x: sol - 6, y: y(enBuyuk * i / 4) + 4, 'text-anchor': 'end', 'font-size': 10, fill: sol2, 'font-family': 'IBM Plex Mono, monospace' }, svg).textContent = Math.round(enBuyuk * i / 4);
        }
        el('line', { x1: sol, x2: G - sag, y1: yy(80), y2: yy(80), stroke: kir, 'stroke-width': 1.5, 'stroke-dasharray': '7 5' }, svg);
        el('text', { x: G - sag - 4, y: yy(80) - 6, 'text-anchor': 'end', 'font-size': 11, fill: kir, 'font-weight': 600 }, svg).textContent = '%80 sınırı';

        let hayati = true;
        veri.forEach(function (d, i) {
            const bx = sol + i * genislik + genislik * 0.16;
            const c = el('rect', { x: bx, width: genislik * 0.68, y: y(d.deger), height: Math.max(1, Y - alt - y(d.deger)), rx: 6, fill: hayati ? bak : renk('--cizgi'), class: 'cubuk-anim' }, svg);
            c.style.animationDelay = (i * 0.08) + 's';
            etkilesim(c, d.etiket + '\n' + sayiYaz(d.deger) + ' ' + birim + '\nKümülatif %' + sayiYaz(d.kumulatif, 1));
            if (d.kumulatif >= 80) hayati = false;

            const etiket = el('text', { x: bx + genislik * 0.34, y: Y - alt + 16, 'text-anchor': 'middle', 'font-size': 11, fill: renk('--yazi-2') }, svg);
            const kelimeler = String(d.etiket).split(/\s+/);
            const satirlar = [];
            kelimeler.forEach(function (k) {
                const son = satirlar.length ? satirlar[satirlar.length - 1] : null;
                if (son !== null && (son + ' ' + k).length <= Math.max(7, Math.floor(genislik / 6.5))) satirlar[satirlar.length - 1] = son + ' ' + k;
                else satirlar.push(k);
            });
            satirlar.slice(0, 3).forEach(function (s, j) {
                el('tspan', { x: bx + genislik * 0.34, dy: j === 0 ? 0 : 13 }, etiket).textContent = s;
            });
        });

        let yolD = '';
        veri.forEach(function (d, i) { yolD += (i === 0 ? 'M' : 'L') + (sol + i * genislik + genislik / 2) + ' ' + yy(d.kumulatif) + ' '; });
        const cizgi = el('path', { d: yolD, fill: 'none', stroke: sar, 'stroke-width': 2.5, 'stroke-linejoin': 'round', class: 'cizgi-anim' }, svg);
        cizgi.style.setProperty('--uzunluk', Math.ceil(cizgi.getTotalLength ? cizgi.getTotalLength() : 2000));
        veri.forEach(function (d, i) {
            const n = el('circle', { cx: sol + i * genislik + genislik / 2, cy: yy(d.kumulatif), r: 4.5, fill: renk('--yuzey'), stroke: sar, 'stroke-width': 2.5, class: 'nokta-anim' }, svg);
            n.style.animationDelay = (0.5 + i * 0.07) + 's';
            etkilesim(n, d.etiket + '\nKümülatif %' + sayiYaz(d.kumulatif, 1));
        });
    }

    const turler = { alan: alan, halka: halka, spc: spc, cubuk: cubuk, gosterge: gosterge, pareto: pareto };

    function hepsiniCiz() {
        document.querySelectorAll('[data-grafik]').forEach(function (kap) {
            const tur = turler[kap.dataset.grafik];
            if (!tur) return;
            try {
                kap.innerHTML = '';
                tur(kap, JSON.parse(kap.dataset.veri || '[]'));
                kap.dataset.cizildi = '1';
            } catch (e) { console.error('Grafik çizilemedi', e); }
        });
    }

    document.addEventListener('DOMContentLoaded', hepsiniCiz);
    return { yenile: hepsiniCiz };
})();
